using LogiMarket.Application.Consultas;
using LogiMarket.Application.Eventos;
using LogiMarket.Application.Puertos;
using LogiMarket.Domain.Auditoria;
using LogiMarket.Domain.Despachos;
using LogiMarket.Domain.Devoluciones;
using LogiMarket.Domain.Eventos;
using LogiMarket.Domain.Inventario;
using LogiMarket.Domain.Pagos;
using Microsoft.EntityFrameworkCore;

namespace LogiMarket.Infrastructure.Persistencia;

internal sealed class RepositorioPagos(LogiMarketDbContext db) : IRepositorioPagos
{
    public Task<Pago?> ObtenerPorClaveAsync(string claveIdempotencia, CancellationToken ct = default) =>
        db.Pagos.FirstOrDefaultAsync(p => p.ClaveIdempotencia == claveIdempotencia, ct);

    public async Task AgregarAsync(Pago pago, CancellationToken ct = default) => await db.Pagos.AddAsync(pago, ct);
}

internal sealed class RepositorioInventario(LogiMarketDbContext db) : IRepositorioInventario
{
    public async Task AgregarMovimientoAsync(MovimientoInventario movimiento, CancellationToken ct = default) =>
        await db.Movimientos.AddAsync(movimiento, ct);

    public Task<bool> ExistenMovimientosAsync(int productoId, CancellationToken ct = default) =>
        db.Movimientos.AnyAsync(m => m.ProductoId == productoId, ct);

    public async Task<IReadOnlyList<MovimientoDto>> ListarMovimientosAsync(string? sku, int maximo, CancellationToken ct = default)
    {
        var filas = await (from m in db.Movimientos
                           join p in db.Elementos on m.ProductoId equals p.Id
                           where sku == null || p.Sku == sku
                           orderby m.Id descending
                           select new { m.Fecha, p.Sku, m.Cantidad, m.Motivo, m.Referencia, m.StockResultante })
            .Take(maximo).ToListAsync(ct);
        return filas.Select(x => new MovimientoDto(x.Fecha, x.Sku, x.Cantidad, x.Motivo.ToString(), x.Referencia, x.StockResultante)).ToList();
    }
}

internal sealed class RepositorioDespachos(LogiMarketDbContext db) : IRepositorioDespachos
{
    public Task<Despacho?> ObtenerPorPedidoAsync(int pedidoId, CancellationToken ct = default) =>
        db.Despachos.FirstOrDefaultAsync(d => d.PedidoId == pedidoId, ct);

    public async Task AgregarAsync(Despacho despacho, CancellationToken ct = default) => await db.Despachos.AddAsync(despacho, ct);
}

internal sealed class RepositorioDevoluciones(LogiMarketDbContext db) : IRepositorioDevoluciones
{
    public Task<Devolucion?> ObtenerPorPedidoAsync(int pedidoId, CancellationToken ct = default) =>
        db.Devoluciones.FirstOrDefaultAsync(d => d.PedidoId == pedidoId, ct);

    public async Task AgregarAsync(Devolucion devolucion, CancellationToken ct = default) => await db.Devoluciones.AddAsync(devolucion, ct);
}

internal sealed class RepositorioOutbox(LogiMarketDbContext db) : IRepositorioOutbox
{
    public async Task AgregarAsync(EventoOutbox evento, CancellationToken ct = default) => await db.Outbox.AddAsync(evento, ct);

    public async Task<IReadOnlyList<int>> IdsPendientesAsync(int maximo, CancellationToken ct = default) =>
        await db.Outbox.Where(e => e.ProcesadoEn == null && e.Intentos < ProcesadorOutbox.MaximoIntentos)
            .OrderBy(e => e.Id).Select(e => e.Id).Take(maximo).ToListAsync(ct);

    public Task<EventoOutbox?> ObtenerAsync(int id, CancellationToken ct = default) =>
        db.Outbox.FirstOrDefaultAsync(e => e.Id == id, ct);

    public async Task<ResumenOutbox> ResumenAsync(CancellationToken ct = default) => new(
        await db.Outbox.CountAsync(e => e.ProcesadoEn == null && e.Intentos == 0, ct),
        await db.Outbox.CountAsync(e => e.ProcesadoEn != null, ct),
        await db.Outbox.CountAsync(e => e.ProcesadoEn == null && e.Intentos > 0, ct));
}

internal sealed class RepositorioAuditoria(LogiMarketDbContext db) : IRepositorioAuditoria
{
    public async Task AgregarAsync(RegistroAuditoria registro, CancellationToken ct = default) => await db.Auditoria.AddAsync(registro, ct);

    public async Task<IReadOnlyList<RegistroAuditoria>> ListarAsync(int maximo, CancellationToken ct = default) =>
        await db.Auditoria.OrderByDescending(a => a.Id).Take(maximo).ToListAsync(ct);
}
