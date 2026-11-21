using LogiMarket.Domain.Common;

namespace LogiMarket.Domain.Pedidos;

public enum EstadoPedido { Borrador, Validado, Pagado, Despachado, Entregado, Cancelado }

public sealed class TransicionInvalidaException : DomainException
{
    public TransicionInvalidaException(EstadoPedido actual, string accion)
        : base($"No se puede {accion} un pedido en estado {actual}.") { }
}

/// <summary>State: cada estado decide que acciones son validas y a donde conducen.</summary>
public interface IEstadoPedido
{
    EstadoPedido Estado { get; }
    bool PermiteEdicion { get; }
    EstadoPedido Validar();
    EstadoPedido Pagar();
    EstadoPedido Despachar();
    EstadoPedido Entregar();
    EstadoPedido Cancelar();
}

public abstract class EstadoPedidoBase : IEstadoPedido
{
    public abstract EstadoPedido Estado { get; }
    public virtual bool PermiteEdicion => false;
    public virtual EstadoPedido Validar() => throw new TransicionInvalidaException(Estado, "validar");
    public virtual EstadoPedido Pagar() => throw new TransicionInvalidaException(Estado, "pagar");
    public virtual EstadoPedido Despachar() => throw new TransicionInvalidaException(Estado, "despachar");
    public virtual EstadoPedido Entregar() => throw new TransicionInvalidaException(Estado, "entregar");
    public virtual EstadoPedido Cancelar() => throw new TransicionInvalidaException(Estado, "cancelar");
}

public sealed class EstadoBorrador : EstadoPedidoBase
{
    public override EstadoPedido Estado => EstadoPedido.Borrador;
    public override bool PermiteEdicion => true;
    public override EstadoPedido Validar() => EstadoPedido.Validado;
    public override EstadoPedido Cancelar() => EstadoPedido.Cancelado;
}

public sealed class EstadoValidado : EstadoPedidoBase
{
    public override EstadoPedido Estado => EstadoPedido.Validado;
    public override EstadoPedido Pagar() => EstadoPedido.Pagado;
    public override EstadoPedido Cancelar() => EstadoPedido.Cancelado;
}

public sealed class EstadoPagado : EstadoPedidoBase
{
    public override EstadoPedido Estado => EstadoPedido.Pagado;
    public override EstadoPedido Despachar() => EstadoPedido.Despachado;
}

public sealed class EstadoDespachado : EstadoPedidoBase
{
    public override EstadoPedido Estado => EstadoPedido.Despachado;
    public override EstadoPedido Entregar() => EstadoPedido.Entregado;
}

public sealed class EstadoEntregado : EstadoPedidoBase
{
    public override EstadoPedido Estado => EstadoPedido.Entregado;
}

public sealed class EstadoCancelado : EstadoPedidoBase
{
    public override EstadoPedido Estado => EstadoPedido.Cancelado;
}

public static class EstadosPedido
{
    private static readonly IReadOnlyDictionary<EstadoPedido, IEstadoPedido> Mapa =
        new IEstadoPedido[]
        {
            new EstadoBorrador(), new EstadoValidado(), new EstadoPagado(),
            new EstadoDespachado(), new EstadoEntregado(), new EstadoCancelado()
        }.ToDictionary(e => e.Estado);

    public static IEstadoPedido Para(EstadoPedido estado) => Mapa[estado];
}