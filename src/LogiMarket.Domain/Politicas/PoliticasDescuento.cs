using LogiMarket.Domain.Clientes;
using LogiMarket.Domain.Common;

namespace LogiMarket.Domain.Politicas;

public readonly record struct ContextoDescuento(CategoriaCliente Categoria, int UnidadesFisicas);

/// <summary>Strategy: cada promocion es una politica intercambiable con prioridad explicita.</summary>
public interface IPoliticaDescuento
{
    string Nombre { get; }
    decimal Tasa { get; }
    int Prioridad { get; }
    bool Aplica(ContextoDescuento contexto);
}

public sealed class SinDescuento : IPoliticaDescuento
{
    public string Nombre => "NORMAL";
    public decimal Tasa => 0m;
    public int Prioridad => 0;
    public bool Aplica(ContextoDescuento contexto) => true;
}

public sealed class Vip10 : IPoliticaDescuento
{
    public string Nombre => "VIP";
    public decimal Tasa => 0.10m;
    public int Prioridad => 50;
    public bool Aplica(ContextoDescuento contexto) => contexto.Categoria == CategoriaCliente.Vip;
}

public sealed class Mayorista15 : IPoliticaDescuento
{
    public const int UnidadesMinimas = 10;
    public string Nombre => "MAYORISTA";
    public decimal Tasa => 0.15m;
    public int Prioridad => 100;
    public bool Aplica(ContextoDescuento contexto) => contexto.UnidadesFisicas >= UnidadesMinimas;
}

public readonly record struct ResultadoDescuento(string Politica, decimal Tasa, decimal Monto);

/// <summary>Elige UNA politica (la de mayor prioridad que aplique): nunca acumula.</summary>
public sealed class CalculadoraDescuento
{
    private readonly IReadOnlyList<IPoliticaDescuento> _politicas;

    public CalculadoraDescuento(IEnumerable<IPoliticaDescuento> politicas)
    {
        _politicas = politicas.OrderByDescending(p => p.Prioridad).ToList();
        if (_politicas.Count == 0) throw new DomainException("Se requiere al menos una politica de descuento.");
    }

    public static CalculadoraDescuento Predeterminada() =>
        new(new IPoliticaDescuento[] { new SinDescuento(), new Vip10(), new Mayorista15() });

    public ResultadoDescuento Calcular(decimal subtotal, ContextoDescuento contexto)
    {
        var politica = _politicas.FirstOrDefault(p => p.Aplica(contexto)) ?? _politicas[^1];
        return new ResultadoDescuento(politica.Nombre, politica.Tasa, Dinero.Redondear(subtotal * politica.Tasa));
    }
}