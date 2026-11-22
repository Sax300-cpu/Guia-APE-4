using LogiMarket.Application.Puertos;
using LogiMarket.Domain.Catalogo;
using LogiMarket.Domain.Clientes;
using LogiMarket.Domain.Pedidos;
using LogiMarket.Domain.Usuarios;
using Microsoft.EntityFrameworkCore;

namespace LogiMarket.Infrastructure.Persistencia;

internal sealed class RepositorioCatalogo(LogiMarketDbContext db) : IRepositorioCatalogo
{
    public async Task<IReadOnlyList<ElementoCatalogo>> ObtenerTodosAsync(CancellationToken ct = default)
    {
        // Con seguimiento activo, EF enlaza los componentes con sus hijos y resuelve kits anidados.
        await db.KitComponentes.ToListAsync(ct);
        return await db.Elementos.OrderBy(e => e.Sku).ToListAsync(ct);
    }

    public async Task<ElementoCatalogo?> ObtenerPorSkuAsync(string sku, CancellationToken ct = default)
    {
        // El catalogo es pequeno: se carga completo para que los kits anidados queden resueltos.
        var normalizado = sku.Trim().ToUpperInvariant();
        return (await ObtenerTodosAsync(ct)).FirstOrDefault(e => e.Sku == normalizado);
    }

    public async Task AgregarAsync(ElementoCatalogo elemento, CancellationToken ct = default) =>
        await db.Elementos.AddAsync(elemento, ct);

    public Task EliminarAsync(ElementoCatalogo elemento, CancellationToken ct = default)
    {
        db.Elementos.Remove(elemento);
        return Task.CompletedTask;
    }
}

internal sealed class RepositorioClientes(LogiMarketDbContext db) : IRepositorioClientes
{
    public Task<Cliente?> ObtenerPorIdAsync(int id, CancellationToken ct = default) =>
        db.Clientes.FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<IReadOnlyList<Cliente>> BuscarAsync(string? texto, CancellationToken ct = default)
    {
        var consulta = db.Clientes.AsQueryable();
        if (!string.IsNullOrWhiteSpace(texto))
            consulta = consulta.Where(c => EF.Functions.Like(c.Nombre, $"%{texto.Trim()}%"));
        return await consulta.OrderBy(c => c.Nombre).ToListAsync(ct);
    }

    public async Task AgregarAsync(Cliente cliente, CancellationToken ct = default) =>
        await db.Clientes.AddAsync(cliente, ct);
}

internal sealed class RepositorioPedidos(LogiMarketDbContext db) : IRepositorioPedidos
{
    private IQueryable<Pedido> Completos => db.Pedidos
        .Include(p => p.Cliente)
        .Include(p => p.Detalles).ThenInclude(d => d.Componentes)
        .Include(p => p.Historial);

    public Task<Pedido?> ObtenerPorIdAsync(int id, CancellationToken ct = default) =>
        Completos.FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<IReadOnlyList<Pedido>> ListarAsync(CancellationToken ct = default) =>
        await Completos.OrderByDescending(p => p.Id).ToListAsync(ct);

    public async Task AgregarAsync(Pedido pedido, CancellationToken ct = default) =>
        await db.Pedidos.AddAsync(pedido, ct);
}

internal sealed class RepositorioUsuarios(LogiMarketDbContext db) : IRepositorioUsuarios
{
    public Task<Usuario?> ObtenerPorNombreAsync(string nombreUsuario, CancellationToken ct = default)
    {
        var normalizado = nombreUsuario.Trim().ToLowerInvariant();
        return db.Usuarios.FirstOrDefaultAsync(u => u.NombreUsuario == normalizado, ct);
    }
}
