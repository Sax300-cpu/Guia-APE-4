using LogiMarket.Domain.Common;

namespace LogiMarket.Domain.Catalogo;

public sealed class KitComponente
{
    public int Id { get; private set; }
    public int KitId { get; private set; }
    public int HijoId { get; private set; }
    public ElementoCatalogo Hijo { get; private set; } = null!;
    public int Cantidad { get; private set; }

    private KitComponente() { }

    internal KitComponente(ElementoCatalogo hijo, int cantidad)
    {
        if (cantidad <= 0) throw new DomainException("La cantidad del componente debe ser positiva.");
        Hijo = hijo;
        Cantidad = cantidad;
    }
}
