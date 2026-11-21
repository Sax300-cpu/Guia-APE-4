using LogiMarket.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace LogiMarket.Tests.Integracion;

/// <summary>Archivo SQLite aislado por prueba (no existe al inicio: valida creacion desde cero).</summary>
public sealed class BaseDatosTemporal : IDisposable
{
    public string Ruta { get; } = Path.Combine(Path.GetTempPath(), $"logimarket_test_{Guid.NewGuid():N}.db");

    public Alcance Abrir()
    {
        var servicios = new ServiceCollection();
        servicios.AddInfrastructure($"Data Source={Ruta}");
        var proveedor = servicios.BuildServiceProvider();
        return new Alcance(proveedor, proveedor.CreateScope());
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        foreach (var sufijo in new[] { "", "-wal", "-shm" })
            if (File.Exists(Ruta + sufijo)) File.Delete(Ruta + sufijo);
    }

    public sealed class Alcance(ServiceProvider proveedor, IServiceScope scope) : IDisposable
    {
        public T Obtener<T>() where T : notnull => scope.ServiceProvider.GetRequiredService<T>();

        public void Dispose()
        {
            scope.Dispose();
            proveedor.Dispose();
        }
    }
}