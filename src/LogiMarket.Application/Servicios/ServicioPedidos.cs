using LogiMarket.Application.Comun;
using LogiMarket.Application.Entrega;
using LogiMarket.Application.Puertos;
using LogiMarket.Application.Seguridad;
using LogiMarket.Application.Validacion;
using LogiMarket.Domain.Common;
using LogiMarket.Domain.Pedidos;
using LogiMarket.Domain.Politicas;

namespace LogiMarket.Application.Servicios;

public sealed record LineaSolicitada(string Sku, int Cantidad);

public sealed class ServicioPedidos(IRepositorioPedidos pedidos, IRepositorioClientes clientes, IRepositorioCatalogo catalogo,
    ServicioEntrega entrega, CalculadoraDescuento calculadora, IAutorizador autorizador, IUnitOfWork uow, IClock reloj)
{
    public async Task<int> CrearBorradorAsync(int clienteId, TipoEntrega tipo, IReadOnlyList<LineaSolicitada> lineas, CancellationToken ct = default)
    {
        await autorizador.ExigirAsync(Permiso.GestionarPedidos, ct);
        var cliente = await clientes.ObtenerPorIdAsync(clienteId, ct) ?? throw new DomainException("El cliente no existe.");
        var todos = await catalogo.ObtenerTodosAsync(ct);

        var builder = new PedidoBuilder().ParaCliente(cliente).ConEntrega(entrega.CrearModalidad(tipo)).ConCalculadora(calculadora);
        foreach (var linea in lineas)
        {
            var elemento = todos.FirstOrDefault(e => e.Sku == linea.Sku.Trim().ToUpperInvariant())
                ?? throw new DomainException($"El SKU {linea.Sku} no existe.");
            builder.AgregarLinea(elemento, linea.Cantidad);
        }
        var pedido = builder.Construir(reloj.UtcAhora);
        await pedidos.AgregarAsync(pedido, ct);
        await uow.GuardarCambiosAsync(ct);
        return pedido.Id;
    }

    public Task<ResultadoOperacion> CambiarCantidadAsync(int pedidoId, string sku, int cantidad, CancellationToken ct = default) =>
        EjecutarAsync(pedidoId, p => p.CambiarCantidad(sku, cantidad, calculadora), ct);

    public Task<ResultadoOperacion> CancelarAsync(int pedidoId, CancellationToken ct = default) =>
        EjecutarAsync(pedidoId, p => p.Cancelar(reloj.UtcAhora), ct);

    /// <summary>BORRADOR -> VALIDADO tras recorrer la cadena de validacion.</summary>
    public async Task<ResultadoOperacion> ValidarAsync(int pedidoId, CancellationToken ct = default)
    {
        await autorizador.ExigirAsync(Permiso.GestionarPedidos, ct);
        var pedido = await pedidos.ObtenerPorIdAsync(pedidoId, ct);
        if (pedido is null) return ResultadoOperacion.Rechazo("NO_ENCONTRADO", "El pedido no existe.");
        var productos = (await catalogo.ObtenerTodosAsync(ct)).OfType<Domain.Catalogo.ProductoSimple>().ToDictionary(p => p.Sku);
        var resultado = PipelineValidacion.Crear().Validar(new ContextoValidacion(pedido, productos, autorizador.Tiene(Permiso.GestionarPedidos)));
        if (!resultado.Valido) return ResultadoOperacion.Rechazo(resultado.Eslabon!, resultado.Mensaje!);
        try
        {
            pedido.Validar(reloj.UtcAhora);
        }
        catch (DomainException ex)
        {
            return ResultadoOperacion.Rechazo("ESTADO_INVALIDO", ex.Message);
        }
        await uow.GuardarCambiosAsync(ct);
        return ResultadoOperacion.Ok("Pedido validado.");
    }

    private async Task<ResultadoOperacion> EjecutarAsync(int pedidoId, Action<Pedido> accion, CancellationToken ct)
    {
        await autorizador.ExigirAsync(Permiso.GestionarPedidos, ct);
        var pedido = await pedidos.ObtenerPorIdAsync(pedidoId, ct);
        if (pedido is null) return ResultadoOperacion.Rechazo("NO_ENCONTRADO", "El pedido no existe.");
        try
        {
            accion(pedido);
        }
        catch (DomainException ex)
        {
            return ResultadoOperacion.Rechazo("REGLA_DE_NEGOCIO", ex.Message);
        }
        await uow.GuardarCambiosAsync(ct);
        return ResultadoOperacion.Ok();
    }
}