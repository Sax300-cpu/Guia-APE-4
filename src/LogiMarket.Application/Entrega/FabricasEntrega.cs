using LogiMarket.Domain.Common;
using LogiMarket.Domain.Pedidos;

namespace LogiMarket.Application.Entrega;

public interface ITarifaEntrega { TipoEntrega Familia { get; } decimal Monto { get; } }
public interface IPlazoEntrega { TipoEntrega Familia { get; } int Dias { get; } }
public interface IEtiquetaEntrega { TipoEntrega Familia { get; } string Generar(int pedidoId); }

/// <summary>Abstract Factory: cada fabrica produce una familia compatible de tarifa, plazo y etiqueta.</summary>
public interface IFabricaEntrega
{
    TipoEntrega Tipo { get; }
    ITarifaEntrega CrearTarifa();
    IPlazoEntrega CrearPlazo();
    IEtiquetaEntrega CrearEtiqueta();
}

public sealed class TarifaEstandar : ITarifaEntrega { public TipoEntrega Familia => TipoEntrega.Estandar; public decimal Monto => 5.00m; }
public sealed class PlazoEstandar : IPlazoEntrega { public TipoEntrega Familia => TipoEntrega.Estandar; public int Dias => 3; }
public sealed class EtiquetaEstandar : IEtiquetaEntrega
{
    public TipoEntrega Familia => TipoEntrega.Estandar;
    public string Generar(int pedidoId) => $"STD-{pedidoId:000000}";
}

public sealed class TarifaExpres : ITarifaEntrega { public TipoEntrega Familia => TipoEntrega.Expres; public decimal Monto => 12.00m; }
public sealed class PlazoExpres : IPlazoEntrega { public TipoEntrega Familia => TipoEntrega.Expres; public int Dias => 1; }
public sealed class EtiquetaExpres : IEtiquetaEntrega
{
    public TipoEntrega Familia => TipoEntrega.Expres;
    public string Generar(int pedidoId) => $"EXP-{pedidoId:000000}";
}

public sealed class FabricaEntregaEstandar : IFabricaEntrega
{
    public TipoEntrega Tipo => TipoEntrega.Estandar;
    public ITarifaEntrega CrearTarifa() => new TarifaEstandar();
    public IPlazoEntrega CrearPlazo() => new PlazoEstandar();
    public IEtiquetaEntrega CrearEtiqueta() => new EtiquetaEstandar();
}

public sealed class FabricaEntregaExpres : IFabricaEntrega
{
    public TipoEntrega Tipo => TipoEntrega.Expres;
    public ITarifaEntrega CrearTarifa() => new TarifaExpres();
    public IPlazoEntrega CrearPlazo() => new PlazoExpres();
    public IEtiquetaEntrega CrearEtiqueta() => new EtiquetaExpres();
}

/// <summary>Cliente de las fabricas: el caso de uso no cambia al agregar una familia nueva.</summary>
public sealed class ServicioEntrega(IEnumerable<IFabricaEntrega> fabricas)
{
    private readonly IReadOnlyDictionary<TipoEntrega, IFabricaEntrega> _fabricas = fabricas.ToDictionary(f => f.Tipo);

    public ModalidadEntrega CrearModalidad(TipoEntrega tipo)
    {
        var fabrica = Obtener(tipo);
        var tarifa = fabrica.CrearTarifa();
        var plazo = fabrica.CrearPlazo();
        VerificarFamilia(tipo, tarifa.Familia, plazo.Familia, fabrica.CrearEtiqueta().Familia);
        return new ModalidadEntrega(tipo, tarifa.Monto, plazo.Dias);
    }

    public string GenerarGuia(TipoEntrega tipo, int pedidoId) => Obtener(tipo).CrearEtiqueta().Generar(pedidoId);

    private IFabricaEntrega Obtener(TipoEntrega tipo) =>
        _fabricas.TryGetValue(tipo, out var f) ? f : throw new DomainException($"No hay fabrica de entrega para {tipo}.");

    private static void VerificarFamilia(TipoEntrega esperada, params TipoEntrega[] productos)
    {
        if (productos.Any(p => p != esperada))
            throw new DomainException("No se pueden mezclar productos de familias de entrega distintas.");
    }
}