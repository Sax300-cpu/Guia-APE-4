using LogiMarket.Domain.Common;

namespace LogiMarket.Domain.Inventario;

public enum MotivoMovimiento { CargaInicial, Venta, Devolucion, Ajuste }

/// <summary>Movimiento positivo o negativo con motivo y referencia (trazabilidad de stock).</summary>
public sealed class MovimientoInventario
{
    public int Id { get; private set; }
    public int ProductoId { get; private set; }
    public int Cantidad { get; private set; }
    public MotivoMovimiento Motivo { get; private set; }
    public string Referencia { get; private set; } = string.Empty;
    public int StockResultante { get; private set; }
    public DateTime Fecha { get; private set; }

    private MovimientoInventario() { }

    public MovimientoInventario(int productoId, int cantidad, MotivoMovimiento motivo, string referencia,
        int stockResultante, DateTime fecha)
    {
        if (cantidad == 0) throw new DomainException("El movimiento no puede ser cero.");
        if (stockResultante < 0) throw new DomainException("El stock resultante no puede ser negativo.");
        ProductoId = productoId;
        Cantidad = cantidad;
        Motivo = motivo;
        Referencia = referencia;
        StockResultante = stockResultante;
        Fecha = fecha;
    }
}
