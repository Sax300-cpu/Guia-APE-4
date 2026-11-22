using LogiMarket.Application.Comun;
using LogiMarket.Application.Entrega;
using LogiMarket.Application.Eventos;
using LogiMarket.Application.Puertos;
using LogiMarket.Application.Seguridad;
using LogiMarket.Domain.Common;
using LogiMarket.Domain.Despachos;
using LogiMarket.Domain.Eventos;

namespace LogiMarket.Application.Servicios;

public sealed class ServicioDespacho(IRepositorioPedidos pedidos, IRepositorioDespachos despachos, IRepositorioOutbox outbox,
    ServicioEntrega entrega, IAutorizador autorizador, IUnitOfWork uow, IClock reloj)
{
    public async Task<ResultadoOperacion> DespacharAsync(int pedidoId, CancellationToken ct = default)
    {
        await autorizador.ExigirAsync(Permiso.DespacharPedido, ct);
        var pedido = await pedidos.ObtenerPorIdAsync(pedidoId, ct);
        if (pedido is null) return ResultadoOperacion.Rechazo("NO_ENCONTRADO", "El pedido no existe.");
        try
        {
            pedido.Despachar(reloj.UtcAhora); // el State rechaza si no esta PAGADO
        }
        catch (DomainException ex)
        {
            return ResultadoOperacion.Rechazo("ESTADO_INVALIDO", ex.Message);
        }

        var guia = entrega.GenerarGuia(pedido.Entrega, pedido.Id);
        await despachos.AgregarAsync(new Despacho(pedido.Id, guia, reloj.UtcAhora), ct);
        var (tipo, payload) = SerializadorEventos.Serializar(new DespachoRegistrado(pedido.Id, guia));
        await outbox.AgregarAsync(new EventoOutbox(tipo, payload, reloj.UtcAhora), ct);
        await uow.GuardarCambiosAsync(ct);
        return ResultadoOperacion.Ok($"Despachado con guia {guia}.");
    }

    public async Task<ResultadoOperacion> EntregarAsync(int pedidoId, CancellationToken ct = default)
    {
        await autorizador.ExigirAsync(Permiso.EntregarPedido, ct);
        var pedido = await pedidos.ObtenerPorIdAsync(pedidoId, ct);
        if (pedido is null) return ResultadoOperacion.Rechazo("NO_ENCONTRADO", "El pedido no existe.");
        var despacho = await despachos.ObtenerPorPedidoAsync(pedidoId, ct);
        try
        {
            pedido.Entregar(reloj.UtcAhora);
            despacho?.RegistrarEntrega(reloj.UtcAhora);
        }
        catch (DomainException ex)
        {
            return ResultadoOperacion.Rechazo("ESTADO_INVALIDO", ex.Message);
        }
        await uow.GuardarCambiosAsync(ct);
        return ResultadoOperacion.Ok("Pedido entregado.");
    }
}