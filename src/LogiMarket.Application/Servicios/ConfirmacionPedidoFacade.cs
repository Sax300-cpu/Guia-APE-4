using LogiMarket.Application.Comun;
using LogiMarket.Application.Eventos;
using LogiMarket.Application.Pedidos;
using LogiMarket.Application.Puertos;
using LogiMarket.Application.Seguridad;
using LogiMarket.Application.Validacion;
using LogiMarket.Domain.Catalogo;
using LogiMarket.Domain.Common;
using LogiMarket.Domain.Eventos;
using LogiMarket.Domain.Inventario;
using LogiMarket.Domain.Pagos;
using LogiMarket.Domain.Pedidos;

namespace LogiMarket.Application.Servicios;

public sealed record ResultadoConfirmacion(bool Exito, string Codigo, string Mensaje, int PedidoId, decimal Total,
    string? Eslabon = null, bool EsRepeticion = false);

/// <summary>
/// Facade: una sola operacion para confirmar un pedido. Coordina autorizacion, idempotencia, validacion, cobro,
/// stock, persistencia y Outbox. No contiene SQL ni reglas de descuento: delega en sus colaboradores.
/// </summary>
public sealed class ConfirmacionPedidoFacade(IAutorizador autorizador, IRepositorioPedidos pedidos, ValidadorPedido validador,
    IRepositorioPagos pagos, IRepositorioInventario inventario, IRepositorioOutbox outbox, IPasarelaPago pasarela,
    IUnitOfWork uow, IClock reloj, RegistroHistorialesBorrador historiales)
{
    public async Task<ResultadoConfirmacion> ConfirmarAsync(int pedidoId, string claveIdempotencia, CancellationToken ct = default)
    {
        await autorizador.ExigirAsync(Permiso.CobrarPedido, ct);
        if (string.IsNullOrWhiteSpace(claveIdempotencia))
            return Rechazo(pedidoId, 0m, CodigosResultado.ClaveObligatoria, "La clave de idempotencia es obligatoria.");

        var pedido = await pedidos.ObtenerPorIdAsync(pedidoId, ct);
        if (pedido is null) return Rechazo(pedidoId, 0m, CodigosResultado.NoEncontrado, CodigosResultado.MensajePedidoInexistente);

        var previo = await pagos.ObtenerPorClaveAsync(claveIdempotencia, ct);
        if (previo is not null) return ResultadoOriginal(previo, pedido);

        var (validacion, productos) = await validador.ValidarAsync(pedido, Permiso.CobrarPedido, ct);
        if (!validacion.Valido)
            return Rechazo(pedidoId, pedido.Total, CodigosResultado.Validacion, validacion.Mensaje!, validacion.Eslabon);

        try
        {
            await uow.EjecutarEnTransaccionAsync(ct2 => ConfirmarEnTransaccionAsync(pedido, productos, claveIdempotencia, ct2), ct);
            historiales.Bloquear(pedido.Id); // confirmado: ya no se puede deshacer ni rehacer
            return new ResultadoConfirmacion(true, "OK", "Pedido pagado.", pedido.Id, pedido.Total);
        }
        catch (CobroRechazadoException ex)
        {
            return Rechazo(pedidoId, pedido.Total, CodigosResultado.CobroRechazado, ex.Message);
        }
        catch (ConflictoConcurrenciaException)
        {
            return Rechazo(pedidoId, pedido.Total, CodigosResultado.Conflicto, "Otro cobro modifico el inventario al mismo tiempo; la operacion se cancelo.");
        }
        catch (ViolacionUnicidadException)
        {
            var original = await pagos.ObtenerPorClaveAsync(claveIdempotencia, ct);
            return original is null
                ? Rechazo(pedidoId, pedido.Total, CodigosResultado.Duplicado, "La operacion ya fue registrada.")
                : ResultadoOriginal(original, pedido);
        }
        catch (DomainException ex)
        {
            return Rechazo(pedidoId, pedido.Total, CodigosResultado.ReglaDeNegocio, ex.Message);
        }
    }

    private async Task ConfirmarEnTransaccionAsync(Pedido pedido, IReadOnlyDictionary<string, ProductoSimple> productos,
        string clave, CancellationToken ct)
    {
        var ahora = reloj.UtcAhora;
        var descontados = new List<(ProductoSimple Producto, int Cantidad)>();
        foreach (var (sku, cantidad) in Requerimientos.PorSku(pedido))
        {
            productos[sku].Descontar(cantidad);
            descontados.Add((productos[sku], cantidad));
        }
        // Se vuelca el stock antes de cobrar: la concurrencia optimista falla aqui y no despues del cobro.
        await uow.GuardarCambiosAsync(ct);

        var cobro = await pasarela.CobrarAsync(pedido.Total, clave, ct);
        if (!cobro.Aprobado) throw new CobroRechazadoException(cobro.Motivo ?? "Cobro rechazado.");

        foreach (var (producto, cantidad) in descontados)
            await inventario.AgregarMovimientoAsync(new MovimientoInventario(producto.Id, -cantidad, MotivoMovimiento.Venta,
                $"PEDIDO-{pedido.Id}", producto.Stock, ahora), ct);

        if (pedido.Estado == EstadoPedido.Borrador) pedido.Validar(ahora);
        pedido.MarcarPagado(clave, ahora);
        await pagos.AgregarAsync(new Pago(pedido.Id, cobro.Referencia, clave, pedido.Total, EstadoPago.Aprobado, pasarela.Proveedor, ahora), ct);

        var (tipo, payload) = SerializadorEventos.Serializar(new PedidoPagado(pedido.Id, pedido.Cliente.Nombre, pedido.Total));
        await outbox.AgregarAsync(new EventoOutbox(tipo, payload, ahora), ct);
    }

    private static ResultadoConfirmacion ResultadoOriginal(Pago previo, Pedido pedido) =>
        previo.PedidoId == pedido.Id
            ? new ResultadoConfirmacion(true, "OK", "Operacion ya confirmada: se devuelve el resultado original.", pedido.Id, previo.Monto, EsRepeticion: true)
            : Rechazo(pedido.Id, pedido.Total, CodigosResultado.ClaveEnUso, "La clave de idempotencia pertenece a otro pedido.");

    private static ResultadoConfirmacion Rechazo(int pedidoId, decimal total, string codigo, string mensaje, string? eslabon = null) =>
        new(false, codigo, mensaje, pedidoId, total, eslabon);
}
