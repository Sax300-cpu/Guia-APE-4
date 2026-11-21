using LogiMarket.Domain.Common;

namespace LogiMarket.Domain.Catalogo;

/// <summary>Componente del patron Composite (productos y kits comparten contrato).</summary>
public abstract class ElementoCatalogo
{
    public int Id { get; private set; }
    public string Sku { get; private set; } = string.Empty;
    public string Nombre { get; private set; } = string.Empty;
    public string Categoria { get; private set; } = string.Empty;
    public string Descripcion { get; private set; } = string.Empty;

    public abstract decimal Precio { get; }
    public abstract decimal Peso { get; }

    protected ElementoCatalogo() { }

    protected ElementoCatalogo(string sku, string nombre, string categoria, string descripcion)
    {
        if (string.IsNullOrWhiteSpace(sku)) throw new DomainException("El SKU es obligatorio.");
        if (string.IsNullOrWhiteSpace(nombre)) throw new DomainException("El nombre es obligatorio.");
        Sku = sku.Trim().ToUpperInvariant();
        Nombre = nombre.Trim();
        Categoria = categoria?.Trim() ?? string.Empty;
        Descripcion = descripcion?.Trim() ?? string.Empty;
    }

    public void Renombrar(string nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre)) throw new DomainException("El nombre es obligatorio.");
        Nombre = nombre.Trim();
    }

    public abstract void Aceptar(ICatalogoVisitor visitor, int cantidad = 1);

    public abstract IteradorComponentesFisicos CrearIterador(int cantidad = 1);

    public abstract bool Contiene(ElementoCatalogo otro);
}