namespace LogiMarket.Domain.Catalogo;

/// <summary>Visitor: nuevos reportes sobre el catalogo sin tocar sus clases.</summary>
public interface ICatalogoVisitor
{
    void Visitar(ProductoSimple producto, int cantidad);
    void Visitar(KitCompuesto kit, int cantidad);
}
