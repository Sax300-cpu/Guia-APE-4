using LogiMarket.Application.Consultas;
using LogiMarket.Domain.Auditoria;
using LogiMarket.Domain.Despachos;
using LogiMarket.Domain.Devoluciones;
using LogiMarket.Domain.Eventos;
using LogiMarket.Domain.Inventario;
using LogiMarket.Domain.Pagos;

namespace LogiMarket.Application.Puertos;

public sealed record ResultadoCobro(bool Aprobado, string Referencia, string? Motivo);

/// <summary>Puerto del cobro simulado: la aplicacion no conoce a ningun proveedor concreto.</summary>
public interface IPasarelaPago
{
    string Proveedor { get; }
    Task<ResultadoCobro> CobrarAsync(decimal monto, string claveIdempotencia, CancellationToken ct = default);
}

public interface IRepositorioPagos
{
    Task<Pago?> ObtenerPorClaveAsync(string claveIdempotencia, CancellationToken ct = default);
    Task AgregarAsync(Pago pago, CancellationToken ct = default);
}

public interface IRepositorioInventario
{
    Task AgregarMovimientoAsync(MovimientoInventario movimiento, CancellationToken ct = default);
    Task<IReadOnlyList<MovimientoDto>> ListarMovimientosAsync(string? sku, int maximo, CancellationToken ct = default);
    Task<bool> ExistenMovimientosAsync(int productoId, CancellationToken ct = default);
}

public interface IRepositorioDespachos
{
    Task<Despacho?> ObtenerPorPedidoAsync(int pedidoId, CancellationToken ct = default);
    Task AgregarAsync(Despacho despacho, CancellationToken ct = default);
}

public interface IRepositorioDevoluciones
{
    Task<Devolucion?> ObtenerPorPedidoAsync(int pedidoId, CancellationToken ct = default);
    Task AgregarAsync(Devolucion devolucion, CancellationToken ct = default);
}

public interface IRepositorioOutbox
{
    Task AgregarAsync(EventoOutbox evento, CancellationToken ct = default);
    Task<IReadOnlyList<int>> IdsPendientesAsync(int maximo, CancellationToken ct = default);
    Task<EventoOutbox?> ObtenerAsync(int id, CancellationToken ct = default);
    Task<ResumenOutbox> ResumenAsync(CancellationToken ct = default);
}

public interface IRepositorioAuditoria
{
    Task AgregarAsync(RegistroAuditoria registro, CancellationToken ct = default);
    Task<IReadOnlyList<RegistroAuditoria>> ListarAsync(int maximo, CancellationToken ct = default);
}
