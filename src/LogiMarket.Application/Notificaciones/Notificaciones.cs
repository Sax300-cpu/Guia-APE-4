using LogiMarket.Application.Demo;
using LogiMarket.Application.Eventos;
using LogiMarket.Application.Metricas;
using LogiMarket.Application.Puertos;
using LogiMarket.Domain.Auditoria;

namespace LogiMarket.Application.Notificaciones;

public sealed record MensajeNotificacion(string Destinatario, string Asunto, string Cuerpo);

public interface INotificador
{
    string Canal { get; }
    Task EnviarAsync(MensajeNotificacion mensaje, CancellationToken ct = default);
}

/// <summary>Factory Method: cada creador decide que notificador concreto fabrica.</summary>
public abstract class CreadorNotificador
{
    public abstract string Canal { get; }
    protected abstract INotificador CrearNotificador();
    public INotificador Crear() => CrearNotificador();
}

internal abstract class NotificadorSimulado(IRepositorioAuditoria auditoria, IClock reloj, ConfiguracionDemo demo) : INotificador
{
    public abstract string Canal { get; }

    public async Task EnviarAsync(MensajeNotificacion mensaje, CancellationToken ct = default)
    {
        if (demo.FallarNotificador) throw new InvalidOperationException($"El notificador {Canal} no esta disponible.");
        await auditoria.AgregarAsync(new RegistroAuditoria(reloj.UtcAhora, "sistema", $"NOTIFICACION_{Canal}",
            $"{mensaje.Destinatario} | {mensaje.Asunto}", CategoriaAuditoria.Negocio), ct);
    }
}

internal sealed class NotificadorEmail(IRepositorioAuditoria a, IClock r, ConfiguracionDemo d) : NotificadorSimulado(a, r, d)
{
    public override string Canal => Demo.CanalesNotificacion.Email;
}

internal sealed class NotificadorSms(IRepositorioAuditoria a, IClock r, ConfiguracionDemo d) : NotificadorSimulado(a, r, d)
{
    public override string Canal => Demo.CanalesNotificacion.Sms;
}

public sealed class CreadorEmail(IRepositorioAuditoria a, IClock r, ConfiguracionDemo d) : CreadorNotificador
{
    public override string Canal => Demo.CanalesNotificacion.Email;
    protected override INotificador CrearNotificador() => new NotificadorEmail(a, r, d);
}

public sealed class CreadorSms(IRepositorioAuditoria a, IClock r, ConfiguracionDemo d) : CreadorNotificador
{
    public override string Canal => Demo.CanalesNotificacion.Sms;
    protected override INotificador CrearNotificador() => new NotificadorSms(a, r, d);
}

public sealed class ObservadorNotificacion(IEnumerable<CreadorNotificador> creadores, ConfiguracionNotificacion configuracion) : IObservadorEventos
{
    public string Nombre => "Notificacion";

    public async Task ManejarAsync(IEventoNegocio evento, int eventoId, CancellationToken ct)
    {
        var creador = creadores.FirstOrDefault(c => c.Canal == configuracion.Canal)
            ?? throw new InvalidOperationException($"Canal de notificacion desconocido: {configuracion.Canal}");
        var mensaje = evento switch
        {
            PedidoPagado p => new MensajeNotificacion(p.Cliente, $"Pedido #{p.PedidoId} pagado", $"Total cobrado: {p.Total:0.00}"),
            DespachoRegistrado d => new MensajeNotificacion($"pedido {d.PedidoId}", $"Pedido #{d.PedidoId} despachado", $"Guia {d.Guia}"),
            DevolucionAceptada v => new MensajeNotificacion($"pedido {v.PedidoId}", $"Devolucion del pedido #{v.PedidoId}", $"Reembolso: {v.Reembolso:0.00}"),
            _ => throw new InvalidOperationException("Evento sin plantilla de notificacion.")
        };
        await creador.Crear().EnviarAsync(mensaje, ct);
    }
}

public sealed class ObservadorAuditoriaEventos(IRepositorioAuditoria auditoria, IClock reloj) : IObservadorEventos
{
    public string Nombre => "Auditoria";

    public Task ManejarAsync(IEventoNegocio evento, int eventoId, CancellationToken ct) =>
        auditoria.AgregarAsync(new RegistroAuditoria(reloj.UtcAhora, "sistema", $"EVENTO_{evento.GetType().Name}",
            $"outbox:{eventoId}", CategoriaAuditoria.Negocio), ct);
}

public sealed class ObservadorMetricas(MetricasSesion metricas) : IObservadorEventos
{
    public string Nombre => "Metricas";

    public Task ManejarAsync(IEventoNegocio evento, int eventoId, CancellationToken ct)
    {
        switch (evento)
        {
            case PedidoPagado: metricas.RegistrarPedidoPagado(); break;
            case DespachoRegistrado: metricas.RegistrarDespacho(); break;
            case DevolucionAceptada: metricas.RegistrarDevolucion(); break;
        }
        return Task.CompletedTask;
    }
}
