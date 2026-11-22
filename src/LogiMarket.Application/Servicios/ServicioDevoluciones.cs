using LogiMarket.Application.Comun;
using LogiMarket.Application.Eventos;
using LogiMarket.Application.Pedidos;
using LogiMarket.Application.Puertos;
using LogiMarket.Application.Seguridad;
using LogiMarket.Domain.Catalogo;
using LogiMarket.Domain.Common;
using LogiMarket.Domain.Devoluciones;
using LogiMarket.Domain.Eventos;
using LogiMarket.Domain.Inventario;
using LogiMarket.Domain.Pedidos;

namespace LogiMarket.Application.Servicios;

public sealed record SolicitudDevolucion(int PedidoId, MotivoDevolucion Motivo, bool SinUso);
public sealed record ResultadoDevolucion(bool Exito, string Codigo, string Mensaje, decimal Reembolso);

public sealed record DependenciasDevolucion(IRepositorioDespachos Despachos, IRepositorioDevoluciones Devoluciones,
    IRepositorioCatalogo Catalogo, IRepositorioInventario Inventario, IRepositorioOutbox Outbox, IUnitOfWork Uow, IClock Reloj);

/// <summary>
/// Template Method: la secuencia es invariante (elegibilidad, inspeccion, reembolso, registro y stock, evento);
/// las variantes personalizan los pasos permitidos.
/// </summary>
public abstract class ProcesarDevolucion(DependenciasDevolucion deps)
{
    public abstract MotivoDevolucion Motivo { get; }

    public async Task<ResultadoDevolucion> EjecutarAsync(Pedido pedido, SolicitudDevolucion solicitud, CancellationToken ct = default)
    {
        if (await deps.Devoluciones.ObtenerPorPedidoAsync(pedido.Id, ct) is not null)
            return Rechazo("YA_DEVUELTO", "El pedido ya tiene una devolucion registrada.");

        var elegibilidad = await VerificarElegibilidadAsync(pedido, ct);
        if (!elegibilidad.Ok) return Rechazo("NO_ELEGIBLE", elegibilidad.Mensaje);

        var inspeccion = InspeccionarCondicion(solicitud);
        if (!inspeccion.Ok) return Rechazo("CONDICION_NO_CUMPLIDA", inspeccion.Mensaje);

        var reembolso = CalcularReembolso(pedido);
        try
        {
            await deps.Uow.EjecutarEnTransaccionAsync(async c =>
            {
                await RegistrarDevolucionYStockAsync(pedido, c);
                await GenerarEventoAsync(pedido, reembolso, c);
            }, ct);
        }
        catch (ViolacionUnicidadException)
        {
            return Rechazo("YA_DEVUELTO", "El pedido ya tiene una devolucion registrada.");
        }
        catch (ConflictoConcurrenciaException)
        {
            return Rechazo("CONFLICTO", "El inventario cambio durante la operacion; se cancelo sin efectos.");
        }
        return new ResultadoDevolucion(true, "OK", "Devolucion aceptada.", reembolso);
    }

    protected virtual async Task<(bool Ok, string Mensaje)> VerificarElegibilidadAsync(Pedido pedido, CancellationToken ct)
    {
        if (pedido.Estado != EstadoPedido.Entregado) return (false, "Solo se puede devolver un pedido ENTREGADO.");
        var entregadoEn = (await deps.Despachos.ObtenerPorPedidoAsync(pedido.Id, ct))?.EntregadoEn;
        if (entregadoEn is null) return (false, "No hay fecha de entrega registrada.");
        // Aqui solo se evalua el plazo; la condicion del producto se inspecciona en la variante.
        var (elegible, motivo) = ReglasDevolucion.Evaluar(Motivo, entregadoEn.Value, deps.Reloj.UtcAhora, sinUso: true);
        return (elegible, motivo);
    }

    protected abstract (bool Ok, string Mensaje) InspeccionarCondicion(SolicitudDevolucion solicitud);

    /// <summary>Importe de productos con descuento; la tarifa de envio no se reembolsa.</summary>
    protected virtual decimal CalcularReembolso(Pedido pedido) => pedido.Subtotal - pedido.Descuento;

    protected virtual async Task RegistrarDevolucionYStockAsync(Pedido pedido, CancellationToken ct)
    {
        var productos = (await deps.Catalogo.ObtenerTodosAsync(ct)).OfType<ProductoSimple>().ToDictionary(p => p.Sku);
        var ahora = deps.Reloj.UtcAhora;
        foreach (var (sku, cantidad) in Requerimientos.PorSku(pedido))
        {
            var producto = productos[sku];
            producto.Reponer(cantidad);
            await deps.Inventario.AgregarMovimientoAsync(new MovimientoInventario(producto.Id, cantidad, MotivoMovimiento.Devolucion,
                $"DEVOLUCION-{pedido.Id}", producto.Stock, ahora), ct);
        }
        await deps.Devoluciones.AgregarAsync(Devolucion.Para(pedido, Motivo, ahora), ct);
    }

    protected virtual async Task GenerarEventoAsync(Pedido pedido, decimal reembolso, CancellationToken ct)
    {
        var (tipo, payload) = SerializadorEventos.Serializar(new DevolucionAceptada(pedido.Id, reembolso));
        await deps.Outbox.AgregarAsync(new EventoOutbox(tipo, payload, deps.Reloj.UtcAhora), ct);
    }

    private static ResultadoDevolucion Rechazo(string codigo, string mensaje) => new(false, codigo, mensaje, 0m);
}

public sealed class ProcesarDevolucionPorDefecto(DependenciasDevolucion deps) : ProcesarDevolucion(deps)
{
    public override MotivoDevolucion Motivo => MotivoDevolucion.Defecto;

    protected override (bool Ok, string Mensaje) InspeccionarCondicion(SolicitudDevolucion solicitud) =>
        (true, "Defecto declarado.");
}

public sealed class ProcesarDevolucionPorArrepentimiento(DependenciasDevolucion deps) : ProcesarDevolucion(deps)
{
    public override MotivoDevolucion Motivo => MotivoDevolucion.Arrepentimiento;

    protected override (bool Ok, string Mensaje) InspeccionarCondicion(SolicitudDevolucion solicitud) =>
        solicitud.SinUso ? (true, "Producto sin uso.") : (false, "El producto debe estar sin uso.");
}

public sealed class ServicioDevoluciones(IRepositorioPedidos pedidos, IEnumerable<ProcesarDevolucion> procesos, IAutorizador autorizador)
{
    private readonly IReadOnlyDictionary<MotivoDevolucion, ProcesarDevolucion> _procesos = procesos.ToDictionary(p => p.Motivo);

    public async Task<ResultadoDevolucion> SolicitarAsync(SolicitudDevolucion solicitud, CancellationToken ct = default)
    {
        await autorizador.ExigirAsync(Permiso.ProcesarDevolucion, ct);
        var pedido = await pedidos.ObtenerPorIdAsync(solicitud.PedidoId, ct);
        if (pedido is null) return new ResultadoDevolucion(false, "NO_ENCONTRADO", "El pedido no existe.", 0m);
        return await _procesos[solicitud.Motivo].EjecutarAsync(pedido, solicitud, ct);
    }
}