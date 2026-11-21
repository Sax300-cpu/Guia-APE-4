using LogiMarket.Domain.Catalogo;
using LogiMarket.Domain.Clientes;
using LogiMarket.Domain.Common;
using LogiMarket.Domain.Politicas;

namespace LogiMarket.Domain.Pedidos;

/// <summary>Builder: ensambla un Pedido valido; nunca entrega objetos incompletos.</summary>
public sealed class PedidoBuilder
{
    private Cliente? _cliente;
    private ModalidadEntrega? _entrega;
    private CalculadoraDescuento _calculadora = CalculadoraDescuento.Predeterminada();
    private readonly List<PedidoDetalle> _lineas = new();

    public PedidoBuilder ParaCliente(Cliente cliente) { _cliente = cliente; return this; }

    public PedidoBuilder ConEntrega(ModalidadEntrega entrega) { _entrega = entrega; return this; }

    public PedidoBuilder ConCalculadora(CalculadoraDescuento calculadora) { _calculadora = calculadora; return this; }

    public PedidoBuilder AgregarLinea(ElementoCatalogo elemento, int cantidad)
    {
        _lineas.Add(PedidoDetalle.Desde(elemento, cantidad));
        return this;
    }

    public Pedido Construir(DateTime ahora)
    {
        if (_cliente is null) throw new DomainException("El pedido requiere un cliente.");
        if (!_cliente.Activo) throw new DomainException("El cliente esta inactivo.");
        if (_entrega is null) throw new DomainException("El pedido requiere una modalidad de entrega.");
        if (_lineas.Count == 0) throw new DomainException("El pedido requiere al menos una linea.");
        return Pedido.Crear(_cliente, _entrega, _lineas.ToList(), _calculadora, ahora);
    }
}