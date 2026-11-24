namespace LogiMarket.Application.Consultas;

public sealed record ClienteDto(int Id, string Nombre, string Email, string Categoria, bool Activo);

public sealed record CatalogoFilaDto(string Sku, string Nombre, string Categoria, string Tipo, decimal Precio, int? Stock, decimal PesoKg, string Composicion, string DesgloseFisico);

public sealed record LineaPedidoDto(string Sku, string Nombre, int Cantidad, decimal PrecioUnitario, decimal Subtotal, int UnidadesFisicas);

public sealed record HistorialEstadoDto(DateTime Fecha, string Desde, string Hacia);

public sealed record PedidoDetalleDto(int Id, string Cliente, string Estado, string Entrega, int PlazoDias, decimal Subtotal,
    decimal Descuento, string Politica, decimal Tarifa, decimal Total, bool PermiteEdicion,
    IReadOnlyList<LineaPedidoDto> Lineas, IReadOnlyList<HistorialEstadoDto> Historial);

public sealed record MovimientoDto(DateTime Fecha, string Sku, int Cantidad, string Motivo, string Referencia, int StockResultante);

public sealed record StockDto(string Sku, string Nombre, int Stock, bool Alerta);

public sealed record AuditoriaDto(DateTime Fecha, string Usuario, string Accion, string Detalle, string Categoria);

public sealed record ResumenOutbox(int Pendientes, int Procesados, int Fallidos);

/// <summary>Nombres de estado de pedido tal como los exponen los DTO; derivados del enum para no repetir cadenas en la interfaz.</summary>
public static class EstadosPedidoTexto
{
    public const string Borrador = nameof(Domain.Pedidos.EstadoPedido.Borrador);
    public const string Validado = nameof(Domain.Pedidos.EstadoPedido.Validado);
    public const string Pagado = nameof(Domain.Pedidos.EstadoPedido.Pagado);
    public const string Despachado = nameof(Domain.Pedidos.EstadoPedido.Despachado);
    public const string Entregado = nameof(Domain.Pedidos.EstadoPedido.Entregado);
    public const string Cancelado = nameof(Domain.Pedidos.EstadoPedido.Cancelado);
    public static readonly string[] Todos = [Borrador, Validado, Pagado, Despachado, Entregado, Cancelado];
}
