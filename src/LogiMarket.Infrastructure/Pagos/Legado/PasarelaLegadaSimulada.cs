namespace LogiMarket.Infrastructure.Pagos.Legado;

/// <summary>
/// Componente "heredado" con una API incompatible con IPasarelaPago (centavos enteros y texto de respuesta).
/// No se modifica: el Adapter se encarga de traducir.
/// </summary>
public sealed class PasarelaLegadaSimulada(Func<bool> rechazar)
{
    public int Llamadas { get; private set; }
    public long UltimosCentavos { get; private set; }
    public string UltimaTransaccion { get; private set; } = string.Empty;

    /// <returns>"APROBADO|codigo" o "RECHAZADO|motivo".</returns>
    public string CobrarCentavos(long centavos, string idTransaccion)
    {
        Llamadas++;
        UltimosCentavos = centavos;
        UltimaTransaccion = idTransaccion;
        return rechazar() ? "RECHAZADO|Fondos no disponibles (legado)" : $"APROBADO|LEG-{Guid.NewGuid():N}";
    }
}
