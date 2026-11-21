using LogiMarket.Domain.Catalogo;
using LogiMarket.Domain.Clientes;
using LogiMarket.Domain.Common;
using LogiMarket.Domain.Politicas;

namespace LogiMarket.Domain.Pedidos;

public sealed class HistorialEstadoPedido
{
    public int Id { get; private set; }
    public int PedidoId { get; private set; }
    public EstadoPedido? Desde { get; private set; }
    public EstadoPedido Hacia { get; private set; }
    public DateTime Fecha { get; private set; }

    private HistorialEstadoPedido() { }

    internal HistorialEstadoPedido(EstadoPedido? desde, EstadoPedido hacia, DateTime fecha)
    {
        Desde = desde;
        Hacia = hacia;
        Fecha = fecha;
    }
}

/// <summary>Prototype: copia profunda sin identidad ni estado de pago.</summary>
public interface IPrototipo<out T>
{
    T Clonar(DateTime ahora);
}

/// <summary>Agregado raiz. No expone setters: todo cambio pasa por operaciones con invariantes.</summary>
public sealed class Pedido : IPrototipo<Pedido>
{
    private readonly List<PedidoDetalle> _detalles = new();
    private readonly List<HistorialEstadoPedido> _historial = new();

    public int Id { get; private set; }
    public int ClienteId { get; private set; }
    public Cliente Cliente { get; private set; } = null!;
    public EstadoPedido Estado { get; private set; }
    public TipoEntrega Entrega { get; private set; }
    public decimal TarifaEnvio { get; private set; }
    public int PlazoDias { get; private set; }
    public decimal Subtotal { get; private set; }
    public decimal Descuento { get; private set; }
    public decimal Total { get; private set; }
    public string PoliticaDescuento { get; private set; } = string.Empty;
    public DateTime CreadoEn { get; private set; }
    public string? ClaveIdempotencia { get; private set; }

    public IReadOnlyList<PedidoDetalle> Detalles => _detalles;
    public IReadOnlyList<HistorialEstadoPedido> Historial => _historial;
    public int UnidadesFisicas => _detalles.Sum(d => d.UnidadesFisicas);
    public bool PermiteEdicion => EstadosPedido.Para(Estado).PermiteEdicion;

    private Pedido() { }

    internal static Pedido Crear(Cliente cliente, ModalidadEntrega entrega, IEnumerable<PedidoDetalle> detalles,
        CalculadoraDescuento calculadora, DateTime ahora)
    {
        var pedido = new Pedido
        {
            Cliente = cliente,
            Estado = EstadoPedido.Borrador,
            Entrega = entrega.Tipo,
            TarifaEnvio = entrega.Tarifa,
            PlazoDias = entrega.PlazoDias,
            CreadoEn = ahora
        };
        pedido._detalles.AddRange(detalles);
        pedido.Valorar(calculadora);
        pedido._historial.Add(new HistorialEstadoPedido(null, EstadoPedido.Borrador, ahora));
        return pedido;
    }

    public void AgregarLinea(ElementoCatalogo elemento, int cantidad, CalculadoraDescuento calculadora)
    {
        ExigirEdicion();
        _detalles.Add(PedidoDetalle.Desde(elemento, cantidad));
        Valorar(calculadora);
    }

    public void CambiarCantidad(string sku, int cantidad, CalculadoraDescuento calculadora)
    {
        ExigirEdicion();
        var detalle = _detalles.FirstOrDefault(d => d.Sku == sku)
            ?? throw new DomainException($"El pedido no contiene {sku}.");
        detalle.CambiarCantidad(cantidad);
        Valorar(calculadora);
    }

    public void Validar(DateTime ahora)
    {
        if (_detalles.Count == 0) throw new DomainException("No se puede validar un pedido sin lineas.");
        Transicionar(e => e.Validar(), ahora);
    }

    public void MarcarPagado(string claveIdempotencia, DateTime ahora)
    {
        if (string.IsNullOrWhiteSpace(claveIdempotencia)) throw new DomainException("La clave de idempotencia es obligatoria.");
        Transicionar(e => e.Pagar(), ahora);
        ClaveIdempotencia = claveIdempotencia;
    }

    public void Despachar(DateTime ahora) => Transicionar(e => e.Despachar(), ahora);
    public void Entregar(DateTime ahora) => Transicionar(e => e.Entregar(), ahora);
    public void Cancelar(DateTime ahora) => Transicionar(e => e.Cancelar(), ahora);

    public Pedido Clonar(DateTime ahora)
    {
        if (Estado != EstadoPedido.Borrador)
            throw new DomainException("Solo se puede clonar un pedido en BORRADOR.");
        var copia = new Pedido
        {
            Cliente = Cliente, ClienteId = ClienteId, Estado = EstadoPedido.Borrador, Entrega = Entrega,
            TarifaEnvio = TarifaEnvio, PlazoDias = PlazoDias, Subtotal = Subtotal, Descuento = Descuento,
            Total = Total, PoliticaDescuento = PoliticaDescuento, CreadoEn = ahora
        };
        copia._detalles.AddRange(_detalles.Select(d => d.Copiar()));
        copia._historial.Add(new HistorialEstadoPedido(null, EstadoPedido.Borrador, ahora));
        return copia;
    }

    private void Valorar(CalculadoraDescuento calculadora)
    {
        Subtotal = _detalles.Sum(d => d.Subtotal);
        var resultado = calculadora.Calcular(Subtotal, new ContextoDescuento(Cliente.Categoria, UnidadesFisicas));
        Descuento = resultado.Monto;
        PoliticaDescuento = resultado.Politica;
        Total = Subtotal - Descuento + TarifaEnvio;
    }

    private void ExigirEdicion()
    {
        if (!PermiteEdicion)
            throw new DomainException($"Un pedido en estado {Estado} no puede editarse.");
    }

    private void Transicionar(Func<IEstadoPedido, EstadoPedido> accion, DateTime ahora)
    {
        var destino = accion(EstadosPedido.Para(Estado));
        _historial.Add(new HistorialEstadoPedido(Estado, destino, ahora));
        Estado = destino;
    }
}