using LogiMarket.Application.Comprobantes;
using LogiMarket.Application.Demo;
using LogiMarket.Application.Pagos;
using LogiMarket.Application.Puertos;
using LogiMarket.Application.Seguridad;
using LogiMarket.Infrastructure.Pagos;
using LogiMarket.Infrastructure.Pagos.Legado;
using LogiMarket.Infrastructure.Persistencia;
using LogiMarket.Infrastructure.Salidas;
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
        services.AddScoped<IRepositorioPagos, RepositorioPagos>();
        services.AddScoped<IRepositorioInventario, RepositorioInventario>();
        services.AddScoped<IRepositorioDespachos, RepositorioDespachos>();
        services.AddScoped<IRepositorioDevoluciones, RepositorioDevoluciones>();
        services.AddScoped<IRepositorioOutbox, RepositorioOutbox>();
        services.AddScoped<IRepositorioAuditoria, RepositorioAuditoria>();
        services.AddSingleton(OpcionesSalidaDocumento.Predeterminadas());
        services.AddSingleton<VistaPreviaSalida>();
        services.AddSingleton<ISalidaDocumento>(sp => sp.GetRequiredService<VistaPreviaSalida>());
        services.AddSingleton<ISalidaDocumento, ArchivoTxtSalida>();
        services.AddSingleton<BitacoraOperaciones>();
        services.AddSingleton<IBitacoraOperaciones>(sp => sp.GetRequiredService<BitacoraOperaciones>());
        services.AddScoped<PasarelaSimulada>();
        services.AddScoped(sp => new PasarelaLegadaSimulada(() => sp.GetRequiredService<ConfiguracionDemo>().FallarCobro));
        services.AddScoped<AdaptadorPasarelaLegada>();
        // Orden obligatorio: Proxy (autoriza) -> Decorator (bitacora) -> proveedor (Adapter o simulado).
        services.AddScoped<IPasarelaPago>(sp =>
        {
            IPasarelaPago proveedor = sp.GetRequiredService<ConfiguracionDemo>().ProveedorPago == ProveedoresPago.Legada
                ? sp.GetRequiredService<AdaptadorPasarelaLegada>()
                : sp.GetRequiredService<PasarelaSimulada>();
            return new PagoAutorizadoProxy(sp.GetRequiredService<IAutorizador>(),
                new AuditoriaPagoDecorator(proveedor, sp.GetRequiredService<IBitacoraOperaciones>()));
        });
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<InicializadorBaseDatos>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<IClock, RelojSistema>();
        return services;
    }
}
