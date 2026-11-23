using LogiMarket.Domain.Common;

namespace LogiMarket.Domain.Despachos;

public sealed class Despacho
{
    public int Id { get; private set; }
    public int PedidoId { get; private set; }
    public string Guia { get; private set; } = string.Empty;
    public DateTime DespachadoEn { get; private set; }
    public DateTime? EntregadoEn { get; private set; }

    private Despacho() { }

    public Despacho(int pedidoId, string guia, DateTime despachadoEn)
    {
        if (string.IsNullOrWhiteSpace(guia)) throw new DomainException("La guia es obligatoria.");
        PedidoId = pedidoId;
        Guia = guia;
        DespachadoEn = despachadoEn;
    }

    public void RegistrarEntrega(DateTime fecha)
    {
        if (EntregadoEn is not null) throw new DomainException("El despacho ya fue entregado.");
        EntregadoEn = fecha;
    }
}
