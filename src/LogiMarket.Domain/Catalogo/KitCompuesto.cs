using LogiMarket.Domain.Common;

namespace LogiMarket.Domain.Catalogo;

public sealed class KitCompuesto : ElementoCatalogo
{
    private readonly List<KitComponente> _componentes = new();

    public IReadOnlyList<KitComponente> Componentes => _componentes;

    public override decimal Precio => _componentes.Sum(c => c.Hijo.Precio * c.Cantidad);
    public override decimal Peso => _componentes.Sum(c => c.Hijo.Peso * c.Cantidad);

    private KitCompuesto() { }

    public KitCompuesto(string sku, string nombre, string categoria = "Kit", string descripcion = "")
        : base(sku, nombre, categoria, descripcion) { }

    public void Agregar(ElementoCatalogo hijo, int cantidad)
    {
        ArgumentNullException.ThrowIfNull(hijo);
        if (hijo.Contiene(this))
            throw new DomainException($"Referencia circular: {Sku} no puede contenerse a si mismo directa ni indirectamente.");
        if (_componentes.Any(c => ReferenceEquals(c.Hijo, hijo)))
            throw new DomainException($"{hijo.Sku} ya es componente de {Sku}.");
        _componentes.Add(new KitComponente(hijo, cantidad));
    }

    public override bool Contiene(ElementoCatalogo otro) =>
        ReferenceEquals(this, otro) || _componentes.Any(c => c.Hijo.Contiene(otro));

    public override void Aceptar(ICatalogoVisitor visitor, int cantidad = 1)
    {
        visitor.Visitar(this, cantidad);
        foreach (var c in _componentes)
            c.Hijo.Aceptar(visitor, cantidad * c.Cantidad);
    }

    public override IteradorComponentesFisicos CrearIterador(int cantidad = 1) => new(this, cantidad);
}