using LogiMarket.Application.Pagos;
using LogiMarket.Application.Puertos;
using LogiMarket.Infrastructure.Pagos.Legado;

namespace LogiMarket.Infrastructure.Pagos;

/// <summary>Adapter: expone la pasarela legada (CobrarCentavos) como IPasarelaPago.CobrarAsync.</summary>
public sealed class AdaptadorPasarelaLegada(PasarelaLegadaSimulada legado, IBitacoraOperaciones bitacora) : IPasarelaPago
{
    public string Proveedor => "LEGADA";

    public Task<ResultadoCobro> CobrarAsync(decimal monto, string claveIdempotencia, CancellationToken ct = default)
    {
        if (monto <= 0) throw new ArgumentOutOfRangeException(nameof(monto));
        var centavos = (long)Math.Round(monto * 100m, 0, MidpointRounding.AwayFromZero);
        bitacora.Registrar(ContextoCorrelacion.Actual ?? "-", "PASARELA", $"{Proveedor} centavos={centavos}");

        var partes = legado.CobrarCentavos(centavos, claveIdempotencia).Split('|', 2);
        return Task.FromResult(partes[0] == "APROBADO"
            ? new ResultadoCobro(true, partes[1], null)
            : new ResultadoCobro(false, string.Empty, partes[1]));
    }
}