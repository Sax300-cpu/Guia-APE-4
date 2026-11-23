using System.Text.Json;
using LogiMarket.Application.Comun;
using LogiMarket.Application.Puertos;

namespace LogiMarket.Application.Eventos;

public interface IEventoNegocio { }

public sealed record PedidoPagado(int PedidoId, string Cliente, decimal Total) : IEventoNegocio;
public sealed record DespachoRegistrado(int PedidoId, string Guia) : IEventoNegocio;
public sealed record DevolucionAceptada(int PedidoId, decimal Reembolso) : IEventoNegocio;

public static class SerializadorEventos
{
    // Los tipos de evento se descubren por la interfaz marcadora: un evento nuevo no exige editar este serializador.
    private static readonly IReadOnlyDictionary<string, Type> Tipos = typeof(IEventoNegocio).Assembly.GetTypes()
        .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IEventoNegocio).IsAssignableFrom(t))
        .ToDictionary(t => t.Name);

    public static (string Tipo, string Payload) Serializar(IEventoNegocio evento) =>
        (evento.GetType().Name, JsonSerializer.Serialize(evento, evento.GetType()));

    public static IEventoNegocio Deserializar(string tipo, string payload) =>
        Tipos.TryGetValue(tipo, out var t)
            ? (IEventoNegocio)JsonSerializer.Deserialize(payload, t)!
            : throw new InvalidOperationException($"Tipo de evento desconocido: {tipo}");
}

/// <summary>Observer: cada suscriptor reacciona a los eventos publicados.</summary>
public interface IObservadorEventos
{
    string Nombre { get; }
    Task ManejarAsync(IEventoNegocio evento, int eventoId, CancellationToken ct);
}

/// <summary>Subject del patron Observer. Notifica en orden de registro.</summary>
public sealed class PublicadorEventos(IEnumerable<IObservadorEventos> observadores)
{
    public async Task PublicarAsync(IEventoNegocio evento, int eventoId, CancellationToken ct = default)
    {
        foreach (var observador in observadores)
            await observador.ManejarAsync(evento, eventoId, ct);
    }
}

/// <summary>
/// Procesa el Outbox despues de confirmar la transaccion de negocio. Cada evento se entrega dentro de su propia
/// transaccion: si un suscriptor falla se revierte todo y el evento queda pendiente para reintento (sin duplicar).
/// </summary>
public sealed class ProcesadorOutbox(IRepositorioOutbox outbox, PublicadorEventos publicador, IUnitOfWork uow, IClock reloj)
{
    public const int MaximoIntentos = 5;

    public async Task<int> ProcesarPendientesAsync(CancellationToken ct = default)
    {
        var procesados = 0;
        foreach (var id in await outbox.IdsPendientesAsync(50, ct))
        {
            try
            {
                await uow.EjecutarEnTransaccionAsync(async c =>
                {
                    var evento = await outbox.ObtenerAsync(id, c) ?? throw new InvalidOperationException($"Evento {id} inexistente.");
                    await publicador.PublicarAsync(SerializadorEventos.Deserializar(evento.Tipo, evento.Payload), id, c);
                    evento.MarcarProcesado(reloj.UtcAhora);
                }, ct);
                procesados++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // El contexto quedo limpio tras el rollback: se relee el evento y se registra el fallo.
                var evento = await outbox.ObtenerAsync(id, ct);
                evento?.RegistrarFallo(ex.Message);
                await uow.GuardarCambiosAsync(ct);
            }
        }
        return procesados;
    }
}
