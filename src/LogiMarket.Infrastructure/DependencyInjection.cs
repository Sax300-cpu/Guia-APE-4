using LogiMarket.Application.Puertos;
using LogiMarket.Infrastructure.Persistencia;
using LogiMarket.Infrastructure.Seguridad;
using LogiMarket.Infrastructure.Sistema;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LogiMarket.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string cadenaConexion)
    {
        services.AddDbContext<LogiMarketDbContext>(o => o.UseSqlite(cadenaConexion));
        services.AddScoped<IRepositorioCatalogo, RepositorioCatalogo>();
        services.AddScoped<IRepositorioClientes, RepositorioClientes>();
        services.AddScoped<IRepositorioPedidos, RepositorioPedidos>();
        services.AddScoped<IRepositorioUsuarios, RepositorioUsuarios>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<InicializadorBaseDatos>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<IClock, RelojSistema>();
        return services;
    }
}