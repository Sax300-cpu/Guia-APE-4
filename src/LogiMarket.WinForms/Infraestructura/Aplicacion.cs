using LogiMarket.Application.Coordinacion;
using LogiMarket.Application.Demo;
using LogiMarket.Application.Metricas;
using LogiMarket.Application.Seguridad;
using Microsoft.Extensions.DependencyInjection;

namespace LogiMarket.WinForms.Infraestructura;

/// <summary>
/// Puente de la presentacion hacia Application: cada operacion corre en su propio alcance de DI
/// (DbContext nuevo), de modo que las pantallas nunca comparten estado de persistencia.
/// </summary>
internal sealed class Aplicacion(IServiceScopeFactory alcances, ISesionUsuario sesion, IMediadorVistas mediador,
    MetricasSesion metricas, ConfiguracionDemo demo, ConfiguracionNotificacion notificacion)
{
    public ISesionUsuario Sesion => sesion;
    public IMediadorVistas Mediador => mediador;
    public MetricasSesion Metricas => metricas;
    public ConfiguracionDemo Demo => demo;
    public ConfiguracionNotificacion Notificacion => notificacion;

    public bool Tiene(Permiso permiso) => sesion.Rol is { } rol && MatrizPermisos.Tiene(rol, permiso);

    public async Task<TResultado> EjecutarAsync<TServicio, TResultado>(Func<TServicio, Task<TResultado>> accion) where TServicio : notnull
    {
        using var alcance = alcances.CreateScope();
        return await accion(alcance.ServiceProvider.GetRequiredService<TServicio>());
    }

    public async Task EjecutarAsync<TServicio>(Func<TServicio, Task> accion) where TServicio : notnull
    {
        using var alcance = alcances.CreateScope();
        await accion(alcance.ServiceProvider.GetRequiredService<TServicio>());
    }

    /// <summary>Para operaciones que combinan varios servicios en el mismo alcance (por ejemplo, comandos).</summary>
    public async Task<TResultado> EjecutarAsync<TResultado>(Func<IServiceProvider, Task<TResultado>> accion)
    {
        using var alcance = alcances.CreateScope();
        return await accion(alcance.ServiceProvider);
    }
}