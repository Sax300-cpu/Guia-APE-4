namespace LogiMarket.Domain.Catalogo;

/// <summary>Visitor: peso logistico total en kg.</summary>
public sealed class PesoLogisticoVisitor : ICatalogoVisitor
{
    public decimal PesoTotalKg { get; private set; }

    public void Visitar(ProductoSimple producto, int cantidad) => PesoTotalKg += producto.PesoKg * cantidad;

    public void Visitar(KitCompuesto kit, int cantidad) { }
}
