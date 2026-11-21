using LogiMarket.Application.Puertos;
using LogiMarket.Domain.Catalogo;
using LogiMarket.Domain.Common;
using LogiMarket.Domain.Devoluciones;
using LogiMarket.Domain.Pagos;
using LogiMarket.Domain.Pedidos;
using LogiMarket.Domain.Usuarios;
using LogiMarket.Infrastructure.Persistencia;
using LogiMarket.Tests.Dominio;
using Microsoft.EntityFrameworkCore;
using static LogiMarket.Tests.Dominio.CatalogoPrueba;

namespace LogiMarket.Tests.Integracion;

public class PersistenciaTests : IDisposable
{
    private readonly BaseDatosTemporal _bd = new();

    public void Dispose() => _bd.Dispose();

    private async Task InicializarAsync()
    {
        using var a = _bd.Abrir();
        await a.Obtener<InicializadorBaseDatos>().InicializarAsync();
    }

    private async Task<int> CrearPedidoValidadoAsync()
    {
        using var a = _bd.Abrir();
        var ana = (await a.Obtener<IRepositorioClientes>().BuscarAsync("Ana")).Single();
        var kit = await a.Obtener<IRepositorioCatalogo>().ObtenerPorSkuAsync("KIT_TELETRABAJO");
        var pedido = new PedidoBuilder().ParaCliente(ana).ConEntrega(Estandar).AgregarLinea(kit!, 1).Construir(Ahora);
        pedido.Validar(Ahora);
        await a.Obtener<IRepositorioPedidos>().AgregarAsync(pedido);
        await a.Obtener<IUnitOfWork>().GuardarCambiosAsync();
        return pedido.Id;
    }

    [Fact] // P01: base desde archivo inexistente, migraciones, seed idempotente
    public async Task P01_Crea_base_desde_cero_y_el_seed_es_idempotente()
    {
        Assert.False(File.Exists(_bd.Ruta));
        await InicializarAsync();
        await InicializarAsync(); // segundo arranque: sin duplicados

        using var a = _bd.Abrir();
        var db = a.Obtener<LogiMarketDbContext>();
        Assert.Equal(5, await db.Elementos.OfType<ProductoSimple>().CountAsync());
        Assert.Equal(2, await db.Elementos.OfType<KitCompuesto>().CountAsync());
        Assert.Equal(4, await db.Usuarios.CountAsync());
        Assert.Equal(5, await db.Movimientos.CountAsync());
        var stock = await db.Elementos.OfType<ProductoSimple>().ToDictionaryAsync(p => p.Sku, p => p.Stock);
        Assert.Equal((8, 5, 20, 4, 30), (stock["P01"], stock["P02"], stock["P03"], stock["P04"], stock["P05"]));
    }

    [Fact]
    public async Task Las_contrasenas_se_guardan_con_hash_PBKDF2()
    {
        await InicializarAsync();
        using var a = _bd.Abrir();
        var admin = (await a.Obtener<IRepositorioUsuarios>().ObtenerPorNombreAsync("admin"))!;
        Assert.Equal(Rol.Admin, admin.Rol);
        Assert.StartsWith("pbkdf2$", admin.PasswordHash);
        Assert.DoesNotContain("Admin123*", admin.PasswordHash);
        var hasher = a.Obtener<IPasswordHasher>();
        Assert.True(hasher.Verificar("Admin123*", admin.PasswordHash));
        Assert.False(hasher.Verificar("otra", admin.PasswordHash));
    }

    [Fact] // P10 (capa de persistencia): los datos sobreviven al cierre y reapertura
    public async Task P10_Los_datos_persisten_tras_reabrir()
    {
        await InicializarAsync();
        var id = await CrearPedidoValidadoAsync();

        using var a = _bd.Abrir(); // nueva conexion = "reinicio"
        var pedido = (await a.Obtener<IRepositorioPedidos>().ObtenerPorIdAsync(id))!;
        Assert.Equal((EstadoPedido.Validado, 59.00m), (pedido.Estado, pedido.Total));
        Assert.Equal("Ana", pedido.Cliente.Nombre);
        Assert.Equal(3, pedido.Detalles.Single().Componentes.Count);
        Assert.Equal(2, pedido.Historial.Count);

        var kit = await a.Obtener<IRepositorioCatalogo>().ObtenerPorSkuAsync("KIT_TELETRABAJO");
        Assert.Equal(60.00m, kit!.Precio); // Composite reconstruido desde la base
        var desglose = new DesgloseFisicoVisitor();
        kit.Aceptar(desglose);
        Assert.Equal(3, desglose.PorSku.Count);
    }

    [Fact] // La instantanea del pedido no cambia si cambia el catalogo
    public async Task La_instantanea_de_precios_no_cambia_con_el_catalogo()
    {
        await InicializarAsync();
        var id = await CrearPedidoValidadoAsync();
        using (var a = _bd.Abrir())
        {
            var p01 = (ProductoSimple)(await a.Obtener<IRepositorioCatalogo>().ObtenerPorSkuAsync("P01"))!;
            p01.CambiarPrecio(99.00m);
            await a.Obtener<IUnitOfWork>().GuardarCambiosAsync();
        }
        using var b = _bd.Abrir();
        var pedido = (await b.Obtener<IRepositorioPedidos>().ObtenerPorIdAsync(id))!;
        Assert.Equal(60.00m, pedido.Subtotal);
        Assert.Equal(20.00m, pedido.Detalles.Single().Componentes.Single(c => c.Sku == "P01").PrecioUnitario);
    }

    [Fact] // Integridad: SKU unico
    public async Task Sku_duplicado_es_rechazado_por_la_base()
    {
        await InicializarAsync();
        using var a = _bd.Abrir();
        a.Obtener<LogiMarketDbContext>().Elementos.Add(new ProductoSimple("P01", "Duplicado", 1m, 0.1m, 1));
        await Assert.ThrowsAsync<DbUpdateException>(() => a.Obtener<IUnitOfWork>().GuardarCambiosAsync());
    }

    [Fact] // Integridad: un solo pago aprobado por pedido, referencia/clave unicas y FK reales
    public async Task Pagos_respetan_unicidad_y_claves_foraneas()
    {
        await InicializarAsync();
        var id = await CrearPedidoValidadoAsync();

        using (var a = _bd.Abrir())
        {
            var db = a.Obtener<LogiMarketDbContext>();
            db.Pagos.Add(new Pago(id, "REF-1", "K-1", 59m, EstadoPago.Aprobado, "Simulada", Ahora));
            await db.SaveChangesAsync();
        }
        using (var b = _bd.Abrir())
        {
            var db = b.Obtener<LogiMarketDbContext>();
            db.Pagos.Add(new Pago(id, "REF-2", "K-2", 59m, EstadoPago.Aprobado, "Simulada", Ahora));
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }
        using (var c = _bd.Abrir())
        {
            var db = c.Obtener<LogiMarketDbContext>();
            db.Pagos.Add(new Pago(9999, "REF-3", "K-3", 59m, EstadoPago.Aprobado, "Simulada", Ahora));
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }
    }

    [Fact] // Integridad: devolucion unica por pedido
    public async Task Solo_se_admite_una_devolucion_por_pedido()
    {
        await InicializarAsync();
        var id = await CrearPedidoValidadoAsync();
        using var a = _bd.Abrir();
        var db = a.Obtener<LogiMarketDbContext>();
        var pedido = (await a.Obtener<IRepositorioPedidos>().ObtenerPorIdAsync(id))!;
        pedido.MarcarPagado("k", Ahora); pedido.Despachar(Ahora); pedido.Entregar(Ahora);
        db.Devoluciones.Add(Devolucion.Para(pedido, MotivoDevolucion.Defecto, Ahora));
        await db.SaveChangesAsync();
        db.Devoluciones.Add(Devolucion.Para(pedido, MotivoDevolucion.Defecto, Ahora));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact] // Concurrencia optimista sobre stock (base de P36)
    public async Task Dos_contextos_no_pueden_descontar_el_mismo_stock_sin_conflicto()
    {
        await InicializarAsync();
        using var a = _bd.Abrir();
        using var b = _bd.Abrir();
        var enA = (ProductoSimple)(await a.Obtener<IRepositorioCatalogo>().ObtenerPorSkuAsync("P02"))!;
        var enB = (ProductoSimple)(await b.Obtener<IRepositorioCatalogo>().ObtenerPorSkuAsync("P02"))!;
        enA.Descontar(1);
        await a.Obtener<IUnitOfWork>().GuardarCambiosAsync();
        enB.Descontar(1);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => b.Obtener<IUnitOfWork>().GuardarCambiosAsync());
    }

    [Fact] // Transaccion: un fallo revierte todo lo hecho dentro
    public async Task La_transaccion_revierte_ante_un_error()
    {
        await InicializarAsync();
        using (var a = _bd.Abrir())
        {
            var uow = a.Obtener<IUnitOfWork>();
            var p03 = (ProductoSimple)(await a.Obtener<IRepositorioCatalogo>().ObtenerPorSkuAsync("P03"))!;
            await Assert.ThrowsAsync<DomainException>(() => uow.EjecutarEnTransaccionAsync(async _ =>
            {
                p03.Descontar(1);
                await uow.GuardarCambiosAsync();
                throw new DomainException("fallo simulado");
            }));
        }
        using var b = _bd.Abrir();
        var stock = (await b.Obtener<LogiMarketDbContext>().Elementos.OfType<ProductoSimple>().SingleAsync(p => p.Sku == "P03")).Stock;
        Assert.Equal(20, stock);
    }
}