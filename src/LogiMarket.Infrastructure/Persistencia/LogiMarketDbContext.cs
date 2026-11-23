using LogiMarket.Domain.Auditoria;
using LogiMarket.Domain.Catalogo;
using LogiMarket.Domain.Clientes;
using LogiMarket.Domain.Despachos;
using LogiMarket.Domain.Devoluciones;
using LogiMarket.Domain.Eventos;
using LogiMarket.Domain.Inventario;
using LogiMarket.Domain.Pagos;
using LogiMarket.Domain.Pedidos;
using LogiMarket.Domain.Usuarios;
using Microsoft.EntityFrameworkCore;

namespace LogiMarket.Infrastructure.Persistencia;

public sealed class LogiMarketDbContext : DbContext
{
    public LogiMarketDbContext(DbContextOptions<LogiMarketDbContext> options) : base(options) { }

    public DbSet<ElementoCatalogo> Elementos => Set<ElementoCatalogo>();
    public DbSet<KitComponente> KitComponentes => Set<KitComponente>();
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Pedido> Pedidos => Set<Pedido>();
    public DbSet<Pago> Pagos => Set<Pago>();
    public DbSet<MovimientoInventario> Movimientos => Set<MovimientoInventario>();
    public DbSet<Despacho> Despachos => Set<Despacho>();
    public DbSet<Devolucion> Devoluciones => Set<Devolucion>();
    public DbSet<EventoOutbox> Outbox => Set<EventoOutbox>();
    public DbSet<RegistroAuditoria> Auditoria => Set<RegistroAuditoria>();

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        // SQLite guarda decimal como TEXT: se persiste en centavos (long) para ordenar y comparar bien.
        builder.Properties<decimal>().HaveConversion<DecimalACentavosConverter>();
        builder.Properties<DateTime>().HaveConversion<FechaUtcConverter>();
        builder.Properties<CategoriaCliente>().HaveConversion<string>();
        builder.Properties<Rol>().HaveConversion<string>();
        builder.Properties<EstadoPedido>().HaveConversion<string>();
        builder.Properties<TipoEntrega>().HaveConversion<string>();
        builder.Properties<EstadoPago>().HaveConversion<string>();
        builder.Properties<MotivoMovimiento>().HaveConversion<string>();
        builder.Properties<MotivoDevolucion>().HaveConversion<string>();
        builder.Properties<CategoriaAuditoria>().HaveConversion<string>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LogiMarketDbContext).Assembly);
}
