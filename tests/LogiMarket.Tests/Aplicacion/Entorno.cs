using LogiMarket.Application.Puertos;
using LogiMarket.Application.Seguridad;
using LogiMarket.Application.Servicios;
using LogiMarket.Domain.Catalogo;
using LogiMarket.Domain.Pedidos;
using LogiMarket.Infrastructure;
using LogiMarket.Infrastructure.Persistencia;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LogiMarket.Tests.Aplicacion;

public sealed class RelojFalso : IClock
{
    public DateTime UtcAhora { get; set; } = new(2026, 11, 21, 12, 0, 0, DateTimeKind.Utc);
}

/// <summary>Una "sesion de la aplicacion": proveedor DI propio sobre una base SQLite temporal.</summary>
public sealed class Entorno : IDisposable
{
    private readonly ServiceProvider _proveedor;
    private readonly IServiceScope _alcance;
    private readonly bool _propietario;

    public string Ruta { get; }
    public RelojFalso Reloj { get; } = new();

    private Entorno(string ruta, bool propietario, Action<IServiceCollection>? ajustes)
    {
        Ruta = ruta;
        _propietario = propietario;
        var servicios = new ServiceCollection();
        servicios.AddInfrastructure($"Data Source={ruta}");
        servicios.AddAplicacion();
        servicios.AddSingleton<IClock>(Reloj);
        ajustes?.Invoke(servicios);
        _proveedor = servicios.BuildServiceProvider();
        _alcance = _proveedor.CreateScope();
    }

    public static async Task<Entorno> CrearAsync(Action<IServiceCollection>? ajustes = null)
    {
        var ruta = Path.Combine(Path.GetTempPath(), $"logimarket_app_{Guid.NewGuid():N}.db");
        var entorno = new Entorno(ruta, true, ajustes);
        await entorno.Obtener<InicializadorBaseDatos>().InicializarAsync();
        return entorno;
    }

    /// <summary>Otra sesion (otro proveedor DI) sobre la misma base: simula reinicio o un segundo usuario.</summary>
    public Entorno AbrirOtraSesion(Action<IServiceCollection>? ajustes = null) => new(Ruta, false, ajustes);

    public T Obtener<T>() where T : notnull => _alcance.ServiceProvider.GetRequiredService<T>();

    /// <summary>Otro alcance del MISMO proveedor (otra vista/consumidor dentro de la misma aplicacion).</summary>
    public IServiceScope NuevoAlcance() => _proveedor.CreateScope();

    public async Task IniciarSesionAsync(string usuario)
    {
        var clave = char.ToUpperInvariant(usuario[0]) + usuario[1..] + "123*";
        Assert.True(await Obtener<ServicioAutenticacion>().IniciarSesionAsync(usuario, clave));
    }

    public async Task<int> CrearPedidoAsync(string cliente, TipoEntrega tipo, params (string Sku, int Cantidad)[] lineas)
    {
        var id = (await Obtener<IRepositorioClientes>().BuscarAsync(cliente)).Single().Id;
        return await Obtener<ServicioPedidos>().CrearBorradorAsync(id, tipo, lineas.Select(l => new LineaSolicitada(l.Sku, l.Cantidad)).ToList());
    }

    public async Task<Dictionary<string, int>> StockAsync() =>
        await Obtener<LogiMarketDbContext>().Elementos.OfType<ProductoSimple>().AsNoTracking().ToDictionaryAsync(p => p.Sku, p => p.Stock);

    public void Dispose()
    {
        _alcance.Dispose();
        _proveedor.Dispose();
        if (!_propietario) return;
        SqliteConnection.ClearAllPools();
        foreach (var sufijo in new[] { "", "-wal", "-shm", "-journal" })
            if (File.Exists(Ruta + sufijo)) File.Delete(Ruta + sufijo);
    }
}