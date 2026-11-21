using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace LogiMarket.Infrastructure.Persistencia;

/// <summary>Solo para herramientas dotnet-ef (migraciones); no se usa en ejecucion.</summary>
internal sealed class FabricaDisenoDbContext : IDesignTimeDbContextFactory<LogiMarketDbContext>
{
    public LogiMarketDbContext CreateDbContext(string[] args)
    {
        var opciones = new DbContextOptionsBuilder<LogiMarketDbContext>()
            .UseSqlite("Data Source=disenio.db")
            .Options;
        return new LogiMarketDbContext(opciones);
    }
}