using LogiMarket.Domain.Common;

namespace LogiMarket.Domain.Pagos;

public enum EstadoPago { Aprobado, Rechazado }

public sealed class Pago
{
    public int Id { get; private set; }
    public int PedidoId { get; private set; }
    public string Referencia { get; private set; } = string.Empty;
    public string ClaveIdempotencia { get; private set; } = string.Empty;
    public decimal Monto { get; private set; }
    public EstadoPago Estado { get; private set; }
    public string Proveedor { get; private set; } = string.Empty;
    public DateTime Fecha { get; private set; }

    private Pago() { }

    public Pago(int pedidoId, string referencia, string claveIdempotencia, decimal monto, EstadoPago estado,
        string proveedor, DateTime fecha)
    {
        if (string.IsNullOrWhiteSpace(referencia)) throw new DomainException("La referencia de cobro es obligatoria.");
        if (string.IsNullOrWhiteSpace(claveIdempotencia)) throw new DomainException("La clave de idempotencia es obligatoria.");
        if (monto <= 0) throw new DomainException("El monto del pago debe ser positivo.");
        PedidoId = pedidoId;
        Referencia = referencia;
        ClaveIdempotencia = claveIdempotencia;
        Monto = Dinero.Redondear(monto);
        Estado = estado;
        Proveedor = proveedor;
        Fecha = fecha;
    }
}
