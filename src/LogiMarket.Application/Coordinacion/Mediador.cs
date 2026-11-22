namespace LogiMarket.Application.Coordinacion;

public enum TipoVista { Panel, Catalogo, Clientes, Carrito, Pedidos, Cobro, Despacho, Devoluciones, Inventario, Reportes, Auditoria, Metricas }

public enum OperacionUI
{
    BorradorEditado, PedidoConfirmado, PedidoCancelado, PedidoDespachado, PedidoEntregado,
    DevolucionProcesada, CatalogoCambiado, ClienteCambiado, OutboxProcesado
}

/// <summary>Contrato que cumplen las vistas (formularios): solo conocen al mediador, nunca a otras vistas.</summary>
public interface IVistaActualizable
{
    TipoVista Tipo { get; }
    Task RefrescarAsync(CancellationToken ct = default);
}

public interface IMediadorVistas
{
    void Registrar(IVistaActualizable vista);
    void Desregistrar(IVistaActualizable vista);
    /// <summary>Informa una operacion terminada; el mediador refresca las demas vistas afectadas.</summary>
    Task NotificarAsync(TipoVista origen, OperacionUI operacion, CancellationToken ct = default);
}

/// <summary>Mediator: decide que vistas se actualizan tras cada operacion, con una tabla de reglas (sin switch).</summary>
public sealed class FormularioPrincipalMediator : IMediadorVistas
{
    private static readonly IReadOnlyDictionary<OperacionUI, TipoVista[]> Afectadas = new Dictionary<OperacionUI, TipoVista[]>
    {
        [OperacionUI.BorradorEditado] = new[] { TipoVista.Carrito, TipoVista.Panel },
        [OperacionUI.PedidoConfirmado] = new[] { TipoVista.Catalogo, TipoVista.Carrito, TipoVista.Pedidos, TipoVista.Cobro, TipoVista.Despacho,
            TipoVista.Reportes, TipoVista.Metricas, TipoVista.Inventario, TipoVista.Auditoria, TipoVista.Panel },
        [OperacionUI.PedidoCancelado] = new[] { TipoVista.Carrito, TipoVista.Pedidos, TipoVista.Cobro, TipoVista.Reportes, TipoVista.Metricas, TipoVista.Auditoria, TipoVista.Panel },
        [OperacionUI.PedidoDespachado] = new[] { TipoVista.Pedidos, TipoVista.Despacho, TipoVista.Devoluciones, TipoVista.Reportes, TipoVista.Metricas, TipoVista.Auditoria, TipoVista.Panel },
        [OperacionUI.PedidoEntregado] = new[] { TipoVista.Pedidos, TipoVista.Despacho, TipoVista.Devoluciones, TipoVista.Reportes, TipoVista.Metricas, TipoVista.Auditoria, TipoVista.Panel },
        [OperacionUI.DevolucionProcesada] = new[] { TipoVista.Catalogo, TipoVista.Pedidos, TipoVista.Devoluciones, TipoVista.Reportes, TipoVista.Metricas,
            TipoVista.Inventario, TipoVista.Auditoria, TipoVista.Panel },
        [OperacionUI.CatalogoCambiado] = new[] { TipoVista.Catalogo, TipoVista.Carrito, TipoVista.Inventario, TipoVista.Panel },
        [OperacionUI.ClienteCambiado] = new[] { TipoVista.Clientes, TipoVista.Carrito, TipoVista.Panel },
        [OperacionUI.OutboxProcesado] = new[] { TipoVista.Metricas, TipoVista.Auditoria, TipoVista.Panel }
    };
    private readonly object _candado = new();
    private readonly List<IVistaActualizable> _vistas = new();

    public void Registrar(IVistaActualizable vista)
    {
        lock (_candado)
            if (!_vistas.Contains(vista)) _vistas.Add(vista);
    }

    public void Desregistrar(IVistaActualizable vista)
    {
        lock (_candado) _vistas.Remove(vista);
    }

    public async Task NotificarAsync(TipoVista origen, OperacionUI operacion, CancellationToken ct = default)
    {
        IVistaActualizable[] destinos;
        lock (_candado)
            destinos = _vistas.Where(v => v.Tipo != origen && Afectadas[operacion].Contains(v.Tipo)).ToArray();
        foreach (var vista in destinos)
            await vista.RefrescarAsync(ct);
    }
}