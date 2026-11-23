using System.Collections.Concurrent;
using LogiMarket.Domain.Catalogo;

namespace LogiMarket.Application.Catalogo;

/// <summary>Estado intrinseco compartido (inmutable) de un SKU.</summary>
public sealed record MetadatosSku(string Sku, string Nombre, string Categoria, string Descripcion);

/// <summary>Fila de vista: referencia metadatos compartidos y guarda solo lo extrinseco (stock, precio, cantidad).</summary>
public sealed record FilaCatalogoVista(MetadatosSku Metadatos, int Stock, decimal Precio, int Cantidad);

/// <summary>Flyweight factory: un unico objeto de metadatos por SKU, seguro entre hilos.</summary>
public sealed class CatalogoVisualFlyweightFactory
{
    private readonly ConcurrentDictionary<string, MetadatosSku> _cache = new();

    public int InstanciasCompartidas => _cache.Count;

    public MetadatosSku Obtener(ElementoCatalogo elemento) =>
        _cache.GetOrAdd(elemento.Sku, _ => new MetadatosSku(elemento.Sku, elemento.Nombre, elemento.Categoria, elemento.Descripcion));

    public FilaCatalogoVista CrearFila(ElementoCatalogo elemento, int cantidad = 0) =>
        new(Obtener(elemento), elemento is ProductoSimple p ? p.Stock : 0, elemento.Precio, cantidad);

    /// <summary>Invalida el SKU cuando cambian sus metadatos (por ejemplo, al renombrar un producto).</summary>
    public void Invalidar(string sku) => _cache.TryRemove(sku, out _);
}
