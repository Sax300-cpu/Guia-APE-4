using System.Collections.Concurrent;
using LogiMarket.Application.Pagos;

namespace LogiMarket.Infrastructure.Sistema;

public sealed record EntradaBitacora(DateTime Fecha, string Correlacion, string Etapa, string Detalle);

/// <summary>Bitacora tecnica en memoria (segura entre hilos). No participa en las transacciones de la base.</summary>
public sealed class BitacoraOperaciones : IBitacoraOperaciones
{
    private readonly ConcurrentQueue<EntradaBitacora> _entradas = new();

    public IReadOnlyList<EntradaBitacora> Entradas => _entradas.ToArray();

    public void Registrar(string correlacion, string etapa, string detalle) =>
        _entradas.Enqueue(new EntradaBitacora(DateTime.UtcNow, correlacion, etapa, detalle));
}
