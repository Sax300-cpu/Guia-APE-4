using LogiMarket.Application.Comun;
using LogiMarket.Application.Pedidos;
using LogiMarket.Application.Puertos;
using LogiMarket.Application.Seguridad;
using LogiMarket.Domain.Common;
using LogiMarket.Domain.Pedidos;
using LogiMarket.Domain.Politicas;

namespace LogiMarket.Application.Servicios;

/// <summary>Edicion de borradores con deshacer/rehacer (Memento) y persistencia de cada paso.</summary>
public sealed class ServicioEdicionBorrador(IRepositorioPedidos pedidos, IRepositorioCatalogo catalogo, CalculadoraDescuento calculadora,
    RegistroHistorialesBorrador historiales, IAutorizador autorizador, IUnitOfWork uow)
{
    public Task<ResultadoOperacion> AgregarLineaAsync(int pedidoId, string sku, int cantidad, CancellationToken ct = default) =>
        EditarAsync(pedidoId, async (pedido, c) =>
        {
            var elemento = (await catalogo.ObtenerPorSkuAsync(sku, c)) ?? throw new DomainException($"El SKU {sku} no existe.");
            pedido.AgregarLinea(elemento, cantidad, calculadora);
        }, registrar: true, ct);

    public Task<ResultadoOperacion> CambiarCantidadAsync(int pedidoId, string sku, int cantidad, CancellationToken ct = default) =>
        EditarAsync(pedidoId, (pedido, _) =>
        {
            pedido.CambiarCantidad(sku, cantidad, calculadora);
            return Task.CompletedTask;
        }, registrar: true, ct);

    public Task<ResultadoOperacion> DeshacerAsync(int pedidoId, CancellationToken ct = default) =>
        EditarAsync(pedidoId, (pedido, _) =>
        {
            historiales.Obtener(pedidoId).Deshacer(pedido);
            return Task.CompletedTask;
        }, registrar: false, ct);

    public Task<ResultadoOperacion> RehacerAsync(int pedidoId, CancellationToken ct = default) =>
        EditarAsync(pedidoId, (pedido, _) =>
        {
            historiales.Obtener(pedidoId).Rehacer(pedido);
            return Task.CompletedTask;
        }, registrar: false, ct);

    private async Task<ResultadoOperacion> EditarAsync(int pedidoId, Func<Pedido, CancellationToken, Task> accion, bool registrar, CancellationToken ct)
    {
        await autorizador.ExigirAsync(Permiso.GestionarPedidos, ct);
        var pedido = await pedidos.ObtenerPorIdAsync(pedidoId, ct);
        if (pedido is null) return ResultadoOperacion.Rechazo(CodigosResultado.NoEncontrado, CodigosResultado.MensajePedidoInexistente);
        var historial = historiales.Obtener(pedidoId);
        var registrado = false;
        try
        {
            if (registrar && pedido.PermiteEdicion)
            {
                historial.Registrar(pedido);
                registrado = true;
            }
            await accion(pedido, ct);
        }
        catch (DomainException ex)
        {
            if (registrado) historial.DescartarUltimoRegistro(); // la edicion fallida no debe ocupar un paso
            return ResultadoOperacion.Rechazo(CodigosResultado.ReglaDeNegocio, ex.Message);
        }
        await uow.GuardarCambiosAsync(ct);
        return ResultadoOperacion.Ok();
    }
}
