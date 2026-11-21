using System.Globalization;

namespace LogiMarket.Domain.Common;

/// <summary>Reglas monetarias oficiales: decimal, 2 decimales, AwayFromZero.</summary>
public static class Dinero
{
    public static decimal Redondear(decimal valor) => Math.Round(valor, 2, MidpointRounding.AwayFromZero);

    public static string Formato(decimal valor) => "USD " + valor.ToString("0.00", CultureInfo.InvariantCulture);
}