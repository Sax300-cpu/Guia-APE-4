using LogiMarket.Domain.Common;

namespace LogiMarket.Domain.Eventos;

public sealed class EventoOutbox
{
    public int Id { get; private set; }
    public string Tipo { get; private set; } = string.Empty;
    public string Payload { get; private set; } = string.Empty;
    public DateTime CreadoEn { get; private set; }
    public DateTime? ProcesadoEn { get; private set; }
    public int Intentos { get; private set; }
    public string? UltimoError { get; private set; }

    private EventoOutbox() { }

    public EventoOutbox(string tipo, string payload, DateTime creadoEn)
    {
        if (string.IsNullOrWhiteSpace(tipo)) throw new DomainException("El tipo de evento es obligatorio.");
        Tipo = tipo;
        Payload = payload;
        CreadoEn = creadoEn;
    }

    public void MarcarProcesado(DateTime ahora)
    {
        if (ProcesadoEn is not null) throw new DomainException("El evento ya fue procesado.");
        ProcesadoEn = ahora;
        UltimoError = null;
    }

    public void RegistrarFallo(string error)
    {
        Intentos++;
        UltimoError = error;
    }
}