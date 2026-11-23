using LogiMarket.Application.Demo;
using LogiMarket.Application.Pagos;
using LogiMarket.Application.Puertos;

namespace LogiMarket.Infrastructure.Pagos;

/// <summary>Cobro simulado: colaborador real que aprueba o rechaza segun la configuracion de demostracion.</summary>
internal sealed class PasarelaSimulada(ConfiguracionDemo demo, IBitacoraOperaciones bitacora) : IPasarelaPago
{
    public string Proveedor => Application.Demo.ProveedoresPago.Simulada;

    public Task<ResultadoCobro> CobrarAsync(decimal monto, string claveIdempotencia, CancellationToken ct = default)
    {
        if (monto <= 0) throw new ArgumentOutOfRangeException(nameof(monto));
        bitacora.Registrar(ContextoCorrelacion.Actual ?? "-", "PASARELA", $"{Proveedor} monto={monto:0.00}");
        return Task.FromResult(demo.FallarCobro
            ? new ResultadoCobro(false, string.Empty, "Cobro rechazado por la pasarela simulada.")
            : new ResultadoCobro(true, $"SIM-{Guid.NewGuid():N}", null));
    }
}
