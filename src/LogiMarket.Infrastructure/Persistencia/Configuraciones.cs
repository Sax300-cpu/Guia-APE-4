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
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LogiMarket.Infrastructure.Persistencia;

internal sealed class ElementoCatalogoConfig : IEntityTypeConfiguration<ElementoCatalogo>
{
    public void Configure(EntityTypeBuilder<ElementoCatalogo> b)
    {
        b.ToTable("Catalogo", t => t.HasCheckConstraint("CK_Catalogo_Producto",
            "Tipo <> 'Producto' OR (PrecioUnitario > 0 AND PesoKg >= 0 AND Stock >= 0)"));
        b.HasKey(e => e.Id);
        b.Property(e => e.Sku).IsRequired().HasMaxLength(40);
        b.HasIndex(e => e.Sku).IsUnique();
        b.HasIndex(e => e.Nombre);
        b.Property(e => e.Nombre).IsRequired().HasMaxLength(120);
        b.Ignore(e => e.Precio);
        b.Ignore(e => e.Peso);
        b.HasDiscriminator<string>("Tipo")
            .HasValue<ProductoSimple>("Producto")
            .HasValue<KitCompuesto>("Kit");
    }
}

internal sealed class ProductoSimpleConfig : IEntityTypeConfiguration<ProductoSimple>
{
    public void Configure(EntityTypeBuilder<ProductoSimple> b)
    {
        b.Property(p => p.PesoKg).HasConversion<DecimalAGramosConverter>();
        b.Property(p => p.Version).IsConcurrencyToken();
    }
}

internal sealed class KitCompuestoConfig : IEntityTypeConfiguration<KitCompuesto>
{
    public void Configure(EntityTypeBuilder<KitCompuesto> b)
    {
        b.HasMany(k => k.Componentes).WithOne().HasForeignKey(c => c.KitId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(k => k.Componentes).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class KitComponenteConfig : IEntityTypeConfiguration<KitComponente>
{
    public void Configure(EntityTypeBuilder<KitComponente> b)
    {
        b.ToTable("KitComponentes", t => t.HasCheckConstraint("CK_KitComponente_Cantidad", "Cantidad > 0"));
        b.HasOne(c => c.Hijo).WithMany().HasForeignKey(c => c.HijoId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(c => new { c.KitId, c.HijoId }).IsUnique();
    }
}

internal sealed class ClienteConfig : IEntityTypeConfiguration<Cliente>
{
    public void Configure(EntityTypeBuilder<Cliente> b)
    {
        b.ToTable("Clientes");
        b.Property(c => c.Nombre).IsRequired().HasMaxLength(120);
        b.HasIndex(c => c.Nombre);
    }
}

internal sealed class UsuarioConfig : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> b)
    {
        b.ToTable("Usuarios");
        b.Property(u => u.NombreUsuario).IsRequired().HasMaxLength(60);
        b.HasIndex(u => u.NombreUsuario).IsUnique();
        b.Property(u => u.PasswordHash).IsRequired();
    }
}

internal sealed class PedidoConfig : IEntityTypeConfiguration<Pedido>
{
    public void Configure(EntityTypeBuilder<Pedido> b)
    {
        b.ToTable("Pedidos");
        b.HasOne(p => p.Cliente).WithMany().HasForeignKey(p => p.ClienteId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(p => p.Estado);
        b.HasIndex(p => p.ClaveIdempotencia).IsUnique().HasFilter("ClaveIdempotencia IS NOT NULL");
        b.HasMany(p => p.Detalles).WithOne().HasForeignKey(d => d.PedidoId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(p => p.Detalles).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.HasMany(p => p.Historial).WithOne().HasForeignKey(h => h.PedidoId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(p => p.Historial).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class PedidoDetalleConfig : IEntityTypeConfiguration<PedidoDetalle>
{
    public void Configure(EntityTypeBuilder<PedidoDetalle> b)
    {
        b.ToTable("PedidoDetalles", t => t.HasCheckConstraint("CK_PedidoDetalle_Cantidad", "Cantidad > 0"));
        b.HasMany(d => d.Componentes).WithOne().HasForeignKey(c => c.PedidoDetalleId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(d => d.Componentes).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.Ignore(d => d.Subtotal);
        b.Ignore(d => d.UnidadesFisicas);
    }
}

internal sealed class PedidoDetalleComponenteConfig : IEntityTypeConfiguration<PedidoDetalleComponente>
{
    public void Configure(EntityTypeBuilder<PedidoDetalleComponente> b) => b.ToTable("PedidoDetalleComponentes");
}

internal sealed class HistorialEstadoConfig : IEntityTypeConfiguration<HistorialEstadoPedido>
{
    public void Configure(EntityTypeBuilder<HistorialEstadoPedido> b) => b.ToTable("HistorialEstadosPedido");
}

internal sealed class PagoConfig : IEntityTypeConfiguration<Pago>
{
    public void Configure(EntityTypeBuilder<Pago> b)
    {
        b.ToTable("Pagos");
        b.HasOne<Pedido>().WithMany().HasForeignKey(p => p.PedidoId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(p => p.Referencia).IsUnique();
        b.HasIndex(p => p.ClaveIdempotencia).IsUnique();
        // Maximo un pago aprobado por pedido.
        b.HasIndex(p => p.PedidoId).IsUnique().HasFilter("Estado = 'Aprobado'");
    }
}

internal sealed class MovimientoConfig : IEntityTypeConfiguration<MovimientoInventario>
{
    public void Configure(EntityTypeBuilder<MovimientoInventario> b)
    {
        b.ToTable("MovimientosInventario", t => t.HasCheckConstraint("CK_Movimiento_Cantidad", "Cantidad <> 0"));
        b.HasOne<ProductoSimple>().WithMany().HasForeignKey(m => m.ProductoId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(m => m.Referencia);
    }
}

internal sealed class DespachoConfig : IEntityTypeConfiguration<Despacho>
{
    public void Configure(EntityTypeBuilder<Despacho> b)
    {
        b.ToTable("Despachos");
        b.HasOne<Pedido>().WithMany().HasForeignKey(d => d.PedidoId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(d => d.PedidoId).IsUnique();
        b.HasIndex(d => d.Guia).IsUnique();
    }
}

internal sealed class DevolucionConfig : IEntityTypeConfiguration<Devolucion>
{
    public void Configure(EntityTypeBuilder<Devolucion> b)
    {
        b.ToTable("Devoluciones");
        b.HasOne<Pedido>().WithMany().HasForeignKey(d => d.PedidoId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(d => d.PedidoId).IsUnique(); // devolucion unica por pedido
    }
}

internal sealed class EventoOutboxConfig : IEntityTypeConfiguration<EventoOutbox>
{
    public void Configure(EntityTypeBuilder<EventoOutbox> b)
    {
        b.ToTable("EventosOutbox");
        b.HasIndex(e => e.ProcesadoEn);
    }
}

internal sealed class AuditoriaConfig : IEntityTypeConfiguration<RegistroAuditoria>
{
    public void Configure(EntityTypeBuilder<RegistroAuditoria> b)
    {
        b.ToTable("Auditoria");
        b.HasIndex(a => a.Fecha);
    }
}