using LogiMarket.Domain.Catalogo;
using LogiMarket.Domain.Common;

namespace LogiMarket.Domain.Pedidos;

/// <summary>Instantanea de un SKU final dentro de una linea (cantidad por unidad vendida).</summary>
public sealed class PedidoDetalleComponente
{
    public int Id { get; private set; }
    public int PedidoDetalleId { get; private set; }
    public string Sku { get; private set; } = string.Empty;
    public decimal PrecioUnitario { get; private set; }
    public int CantidadPorUnidad { get; private set; }

    private PedidoDetalleComponente() { }

    internal PedidoDetalleComponente(string sku, decimal precioUnitario, int cantidadPorUnidad)
    {
        Sku = sku;
        PrecioUnitario = precioUnitario;
        CantidadPorUnidad = cantidadPorUnidad;
    }

    internal PedidoDetalleComponente Copiar() => new(Sku, PrecioUnitario, CantidadPorUnidad);
}

public sealed class PedidoDetalle
{
    private readonly List<PedidoDetalleComponente> _componentes = new();

    public int Id { get; private set; }
    public int PedidoId { get; private set; }
    public string Sku { get; private set; } = string.Empty;
    public string Nombre { get; private set; } = string.Empty;
    public bool EsKit { get; private set; }
    public decimal PrecioUnitario { get; private set; }
    public int Cantidad { get; private set; }
    public IReadOnlyList<PedidoDetalleComponente> Componentes => _componentes;

    public decimal Subtotal => _componentes.Sum(c => c.PrecioUnitario * c.CantidadPorUnidad) * Cantidad;
    public int UnidadesFisicas => _componentes.Sum(c => c.CantidadPorUnidad) * Cantidad;

    private PedidoDetalle() { }

    public static PedidoDetalle Desde(ElementoCatalogo elemento, int cantidad)
    {
        ArgumentNullException.ThrowIfNull(elemento);
        if (cantidad <= 0) throw new DomainException("La cantidad debe ser positiva.");

        var detalle = new PedidoDetalle
        {
            Sku = elemento.Sku,
            Nombre = elemento.Nombre,
            EsKit = elemento is KitCompuesto,
            PrecioUnitario = elemento.Precio,
            Cantidad = cantidad
        };

        var agregados = new SortedDictionary<string, (decimal Precio, int Cantidad)>(StringComparer.Ordinal);
        using var iterador = elemento.CrearIterador();
        while (iterador.MoveNext())
        {
            var c = iterador.Current;
            var previo = agregados.GetValueOrDefault(c.Producto.Sku);
            agregados[c.Producto.Sku] = (c.Producto.Precio, previo.Cantidad + c.Cantidad);
        }
        foreach (var (sku, dato) in agregados)
            detalle._componentes.Add(new PedidoDetalleComponente(sku, dato.Precio, dato.Cantidad));
        return detalle;
    }

    internal void CambiarCantidad(int cantidad)
    {
        if (cantidad <= 0) throw new DomainException("La cantidad debe ser positiva.");
        Cantidad = cantidad;
    }

    internal PedidoDetalle Copiar()
    {
        var copia = new PedidoDetalle
        {
            Sku = Sku,
            Nombre = Nombre,
            EsKit = EsKit,
            PrecioUnitario = PrecioUnitario,
            Cantidad = Cantidad
        };
        copia._componentes.AddRange(_componentes.Select(c => c.Copiar()));
        return copia;
    }
}
