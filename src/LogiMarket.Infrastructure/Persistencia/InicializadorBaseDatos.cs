using LogiMarket.Application.Puertos;
using LogiMarket.Domain.Catalogo;
using LogiMarket.Domain.Clientes;
using LogiMarket.Domain.Inventario;
using LogiMarket.Domain.Usuarios;
using Microsoft.EntityFrameworkCore;

namespace LogiMarket.Infrastructure.Persistencia;

/// <summary>Aplica migraciones y siembra datos oficiales de forma idempotente (ficticios, sin secretos reales).</summary>
public sealed class InicializadorBaseDatos(LogiMarketDbContext db, IPasswordHasher hasher, IClock reloj)
{
    private static readonly (string Sku, string Nombre, decimal Precio, int Stock, decimal Peso)[] Productos =
    {
        ("P01", "Mouse USB", 20.00m, 8, 0.20m),
        ("P02", "Teclado", 30.00m, 5, 0.70m),
        ("P03", "Memoria USB", 10.00m, 20, 0.10m),
        ("P04", "Webcam", 40.00m, 4, 0.35m),
        ("P05", "Alfombrilla", 5.00m, 30, 0.10m)
    };

    private static readonly (string Usuario, Rol Rol, string Clave)[] UsuariosDemo =
    {
        ("admin", Rol.Admin, "Admin123*"),
        ("ventas", Rol.Ventas, "Ventas123*"),
        ("bodega", Rol.Bodega, "Bodega123*"),
        ("consulta", Rol.Consulta, "Consulta123*")
    };

    public async Task InicializarAsync(CancellationToken ct = default)
    {
        await db.Database.MigrateAsync(ct);
        await SembrarCatalogoAsync(ct);
        await SembrarUsuariosYClientesAsync(ct);
        await db.SaveChangesAsync(ct);
    }

    private async Task SembrarCatalogoAsync(CancellationToken ct)
    {
        var existentes = await db.Elementos.ToDictionaryAsync(e => e.Sku, ct);
        var ahora = reloj.UtcAhora;
        var nuevos = new List<ProductoSimple>();

        foreach (var (sku, nombre, precio, stock, peso) in Productos)
        {
            if (existentes.ContainsKey(sku)) continue;
            var producto = new ProductoSimple(sku, nombre, precio, peso, stock, "Accesorios");
            existentes[sku] = producto;
            nuevos.Add(producto);
            db.Elementos.Add(producto);
        }

        if (!existentes.ContainsKey("KIT_OFICINA"))
        {
            var oficina = new KitCompuesto("KIT_OFICINA", "Kit Oficina");
            oficina.Agregar(existentes["P01"], 1);
            oficina.Agregar(existentes["P02"], 1);
            existentes["KIT_OFICINA"] = oficina;
            db.Elementos.Add(oficina);
        }
        if (!existentes.ContainsKey("KIT_TELETRABAJO"))
        {
            var teletrabajo = new KitCompuesto("KIT_TELETRABAJO", "Kit Teletrabajo");
            teletrabajo.Agregar(existentes["KIT_OFICINA"], 1);
            teletrabajo.Agregar(existentes["P03"], 1);
            db.Elementos.Add(teletrabajo);
        }

        // Los movimientos necesitan el Id del producto: se guarda primero y luego se registra la carga inicial.
        if (nuevos.Count == 0) return;
        await db.SaveChangesAsync(ct);
        foreach (var p in nuevos)
            db.Movimientos.Add(new MovimientoInventario(p.Id, p.Stock, MotivoMovimiento.CargaInicial, "SEED", p.Stock, ahora));
    }

    private async Task SembrarUsuariosYClientesAsync(CancellationToken ct)
    {
        var usuarios = await db.Usuarios.Select(u => u.NombreUsuario).ToListAsync(ct);
        foreach (var (usuario, rol, clave) in UsuariosDemo.Where(u => !usuarios.Contains(u.Usuario)))
            db.Usuarios.Add(new Usuario(usuario, rol, hasher.Hash(clave)));

        var clientes = await db.Clientes.Select(c => c.Nombre).ToListAsync(ct);
        foreach (var (nombre, correo, categoria) in new[]
        {
            ("Ana", "ana@example.test", CategoriaCliente.Vip),
            ("Luis", "luis@example.test", CategoriaCliente.Normal),
            ("Carmen", "carmen@example.test", CategoriaCliente.Normal)
        })
            if (!clientes.Contains(nombre)) db.Clientes.Add(new Cliente(nombre, correo, categoria));
    }
}
