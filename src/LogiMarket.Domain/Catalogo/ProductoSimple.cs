using LogiMarket.Domain.Common;

namespace LogiMarket.Domain.Catalogo;

public sealed class ProductoSimple : ElementoCatalogo
{
    public decimal PrecioUnitario { get; private set; }
    public decimal PesoKg { get; private set; }
    public int Stock { get; private set; }
    /// <summary>Token de concurrencia optimista contra sobreventa.</summary>
    public int Version { get; private set; }

    public override decimal Precio => PrecioUnitario;
    public override decimal Peso => PesoKg;

    private ProductoSimple() { }

    public ProductoSimple(string sku, string nombre, decimal precio, decimal pesoKg, int stock,
        string categoria = "", string descripcion = "") : base(sku, nombre, categoria, descripcion)
    {
        ValidarPrecio(precio);
        if (pesoKg < 0) throw new DomainException("El peso no puede ser negativo.");
        if (stock < 0) throw new DomainException("El stock no puede ser negativo.");
        PrecioUnitario = Dinero.Redondear(precio);
        PesoKg = pesoKg;
        Stock = stock;
    }

    public void CambiarPrecio(decimal precio)
    {
        ValidarPrecio(precio);
        PrecioUnitario = Dinero.Redondear(precio);
    }

    public void Descontar(int cantidad)
    {
        if (cantidad <= 0) throw new DomainException("La cantidad a descontar debe ser positiva.");
        if (cantidad > Stock) throw new DomainException($"Stock insuficiente de {Sku}: requiere {cantidad}, disponible {Stock}.");
        Stock -= cantidad;
        Version++;
    }

    public void Reponer(int cantidad)
    {
        if (cantidad <= 0) throw new DomainException("La cantidad a reponer debe ser positiva.");
        Stock += cantidad;
        Version++;
    }

    public override void Aceptar(ICatalogoVisitor visitor, int cantidad = 1) => visitor.Visitar(this, cantidad);

    public override IteradorComponentesFisicos CrearIterador(int cantidad = 1) => new(this, cantidad);

    public override bool Contiene(ElementoCatalogo otro) => ReferenceEquals(this, otro);

    private static void ValidarPrecio(decimal precio)
    {
        if (precio <= 0) throw new DomainException("El precio debe ser positivo.");
    }
}