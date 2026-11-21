using LogiMarket.Domain.Common;

namespace LogiMarket.Domain.Pedidos;

public enum TipoEntrega { Estandar, Expres }

/// <summary>Valor producido por la familia de entrega (Abstract Factory, fase 5).</summary>
public sealed record ModalidadEntrega
{
    public TipoEntrega Tipo { get; }
    public decimal Tarifa { get; }
    public int PlazoDias { get; }

    public ModalidadEntrega(TipoEntrega tipo, decimal tarifa, int plazoDias)
    {
        if (tarifa < 0) throw new DomainException("La tarifa no puede ser negativa.");
        if (plazoDias <= 0) throw new DomainException("El plazo debe ser positivo.");
        Tipo = tipo;
        Tarifa = Dinero.Redondear(tarifa);
        PlazoDias = plazoDias;
    }
}