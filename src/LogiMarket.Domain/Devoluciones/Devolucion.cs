using LogiMarket.Domain.Common;
using LogiMarket.Domain.Pedidos;

namespace LogiMarket.Domain.Devoluciones;

public enum MotivoDevolucion { Defecto, Arrepentimiento }

/// <summary>Reglas puras de elegibilidad: DEFECTO hasta 30 dias, ARREPENTIMIENTO hasta 7 y sin uso.</summary>
public static class ReglasDevolucion
{
    public const int PlazoDefectoDias = 30;
    public const int PlazoArrepentimientoDias = 7;

    public static (bool Elegible, string Motivo) Evaluar(MotivoDevolucion motivo, DateTime entregadoEn, DateTime ahora, bool sinUso)
    {
        var dias = (ahora.Date - entregadoEn.Date).Days;
        if (dias < 0) return (false, "La fecha de entrega es posterior a la solicitud.");
        return motivo switch
        {
            MotivoDevolucion.Defecto when dias <= PlazoDefectoDias => (true, "Defecto dentro de plazo."),
            MotivoDevolucion.Defecto => (false, $"Defecto fuera de plazo ({dias} dias > {PlazoDefectoDias})."),
            MotivoDevolucion.Arrepentimiento when !sinUso => (false, "El producto debe estar sin uso."),
            MotivoDevolucion.Arrepentimiento when dias <= PlazoArrepentimientoDias => (true, "Arrepentimiento dentro de plazo."),
            _ => (false, $"Arrepentimiento fuera de plazo ({dias} dias > {PlazoArrepentimientoDias}).")
        };
    }
}

public sealed class Devolucion
{
    public int Id { get; private set; }
    public int PedidoId { get; private set; }
    public MotivoDevolucion Motivo { get; private set; }
    /// <summary>Subtotal menos descuento; la tarifa de envio no se reembolsa.</summary>
    public decimal Reembolso { get; private set; }
    public DateTime Fecha { get; private set; }

    private Devolucion() { }

    public static Devolucion Para(Pedido pedido, MotivoDevolucion motivo, DateTime ahora)
    {
        if (pedido.Estado != EstadoPedido.Entregado)
            throw new DomainException("Solo se puede devolver un pedido ENTREGADO.");
        return new Devolucion
        {
            PedidoId = pedido.Id,
            Motivo = motivo,
            Reembolso = pedido.Subtotal - pedido.Descuento,
            Fecha = ahora
        };
    }
}
