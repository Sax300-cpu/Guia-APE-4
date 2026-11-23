using LogiMarket.Application.Puertos;
using LogiMarket.Application.Seguridad;

namespace LogiMarket.Application.Pagos;

/// <summary>Puerto de la bitacora tecnica de operaciones (secuencia INICIO -> PASARELA -> FIN/ERROR).</summary>
public interface IBitacoraOperaciones
{
    void Registrar(string correlacion, string etapa, string detalle);
}

/// <summary>Identificador de correlacion que viaja con la operacion de cobro en curso.</summary>
public static class ContextoCorrelacion
{
    private static readonly AsyncLocal<string?> Valor = new();

    public static string? Actual
    {
        get => Valor.Value;
        set => Valor.Value = value;
    }
}

/// <summary>
/// Decorator: registra inicio y resultado (o error) del cobro con un identificador de correlacion,
/// sin alterar el resultado ni las excepciones del cobro concreto.
/// </summary>
public sealed class AuditoriaPagoDecorator(IPasarelaPago interna, IBitacoraOperaciones bitacora) : IPasarelaPago
{
    public string Proveedor => interna.Proveedor;

    public async Task<ResultadoCobro> CobrarAsync(decimal monto, string claveIdempotencia, CancellationToken ct = default)
    {
        var correlacion = Guid.NewGuid().ToString("N")[..8];
        var previa = ContextoCorrelacion.Actual;
        ContextoCorrelacion.Actual = correlacion;
        bitacora.Registrar(correlacion, "INICIO", $"monto={monto:0.00} clave={claveIdempotencia}");
        try
        {
            var resultado = await interna.CobrarAsync(monto, claveIdempotencia, ct);
            bitacora.Registrar(correlacion, "FIN", resultado.Aprobado ? $"APROBADO ref={resultado.Referencia}" : $"RECHAZADO {resultado.Motivo}");
            return resultado;
        }
        catch (Exception ex)
        {
            bitacora.Registrar(correlacion, "ERROR", ex.Message);
            throw;
        }
        finally
        {
            ContextoCorrelacion.Actual = previa;
        }
    }
}

/// <summary>
/// Proxy: verifica el rol (VENTAS/ADMIN) ANTES de delegar. Una denegacion no alcanza al Decorator ni a la
/// pasarela, de modo que no hay LOG de cobro. Funciona sin WinForms.
/// </summary>
public sealed class PagoAutorizadoProxy(IAutorizador autorizador, IPasarelaPago interna) : IPasarelaPago
{
    public string Proveedor => interna.Proveedor;

    public async Task<ResultadoCobro> CobrarAsync(decimal monto, string claveIdempotencia, CancellationToken ct = default)
    {
        await autorizador.ExigirAsync(Permiso.CobrarPedido, ct);
        return await interna.CobrarAsync(monto, claveIdempotencia, ct);
    }
}
