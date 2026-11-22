using LogiMarket.Application.Catalogo;
using LogiMarket.Application.Comandos;
using LogiMarket.Application.Comprobantes;
using LogiMarket.Application.Demo;
using LogiMarket.Application.Entrega;
using LogiMarket.Application.Eventos;
using LogiMarket.Application.Metricas;
using LogiMarket.Application.Notificaciones;
using LogiMarket.Application.Puertos;
using LogiMarket.Application.Seguridad;
using LogiMarket.Application.Servicios;
using LogiMarket.Domain.Politicas;
using Microsoft.Extensions.DependencyInjection;

namespace LogiMarket.Infrastructure;

public static class ServiciosAplicacion
{
    /// <summary>Registra los servicios de Application (la capa no depende del contenedor de DI).</summary>
    public static IServiceCollection AddAplicacion(this IServiceCollection services)
    {
        // Singletons: estado compartido de la sesion de la aplicacion.
        services.AddSingleton<SesionUsuario>();
        services.AddSingleton<ISesionUsuario>(sp => sp.GetRequiredService<SesionUsuario>());
        services.AddSingleton<MetricasSesion>();
        services.AddSingleton<ConfiguracionDemo>();
        services.AddSingleton<ConfiguracionNotificacion>();
        services.AddSingleton(CalculadoraDescuento.Predeterminada());
        services.AddSingleton<IFabricaEntrega, FabricaEntregaEstandar>();
        services.AddSingleton<IFabricaEntrega, FabricaEntregaExpres>();
        services.AddSingleton<ServicioEntrega>();

        services.AddSingleton<CatalogoVisualFlyweightFactory>();
        services.AddScoped<ServicioComprobantes>();
        services.AddScoped<IAutorizador, Autorizador>();
        services.AddScoped<ServicioAutenticacion>();
        services.AddScoped<ServicioPedidos>();
        services.AddScoped<ServicioClientes>();
        services.AddScoped<ServicioCatalogo>();
        services.AddScoped<ConsultasPedidos>();
        services.AddScoped<ConfirmacionPedidoFacade>();
        services.AddScoped<ServicioDespacho>();
        services.AddScoped<ServicioDevoluciones>();
        services.AddScoped<DependenciasDevolucion>();
        services.AddScoped<ProcesarDevolucion, ProcesarDevolucionPorDefecto>();
        services.AddScoped<ProcesarDevolucion, ProcesarDevolucionPorArrepentimiento>();
        services.AddScoped<InvocadorComandos>();

        services.AddScoped<CreadorNotificador, CreadorEmail>();
        services.AddScoped<CreadorNotificador, CreadorSms>();
        // El orden importa: los suscriptores con efectos externos van antes que las metricas en memoria.
        services.AddScoped<IObservadorEventos, ObservadorNotificacion>();
        services.AddScoped<IObservadorEventos, ObservadorAuditoriaEventos>();
        services.AddScoped<IObservadorEventos, ObservadorMetricas>();
        services.AddScoped<PublicadorEventos>();
        services.AddScoped<ProcesadorOutbox>();
        return services;
    }
}