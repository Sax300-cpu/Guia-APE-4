namespace LogiMarket.Domain.Catalogo;

/// <summary>Visitor: desglose fisico por SKU final.</summary>
public sealed class DesgloseFisicoVisitor : ICatalogoVisitor
{
    private readonly SortedDictionary<string, int> _porSku = new(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> PorSku => _porSku;

    public void Visitar(ProductoSimple producto, int cantidad) =>
        _porSku[producto.Sku] = _porSku.GetValueOrDefault(producto.Sku) + cantidad;

    public void Visitar(KitCompuesto kit, int cantidad) { }
}
