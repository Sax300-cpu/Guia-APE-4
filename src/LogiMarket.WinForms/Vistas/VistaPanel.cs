using LogiMarket.Application.Consultas;
using LogiMarket.Application.Coordinacion;
using LogiMarket.Application.Seguridad;
using LogiMarket.Application.Servicios;
using LogiMarket.WinForms.Infraestructura;
using LogiMarket.WinForms.Ui;

namespace LogiMarket.WinForms.Vistas;

/// <summary>Pantalla 02 - Panel general: resumen operativo de pedidos, existencias y alertas.</summary>
internal sealed class VistaPanel : VistaBase
{
    private readonly Label _total, _pagados, _pendientes, _importe, _metricas = new() { AutoSize = true, Font = Estilo.Negrita };
    private readonly DataGridView _recientes = Ui.Ui.Grid("Pedidos recientes",
        new Col("N.", nameof(PedidoResumenDto.Id), 30), new Col("Cliente", nameof(PedidoResumenDto.Cliente), 80),
        new Col("Estado", nameof(PedidoResumenDto.Estado), 60), new Col("Total (USD)", nameof(PedidoResumenDto.Total), 60, "0.00", true),
        new Col("Fecha", nameof(PedidoResumenDto.Fecha), 80, "g"));
    private readonly DataGridView _alertas = Ui.Ui.Grid("Alertas de stock",
        new Col("SKU", nameof(StockDto.Sku), 40), new Col("Producto", nameof(StockDto.Nombre), 100), new Col("Stock", nameof(StockDto.Stock), 40, null, true));
    private readonly Label _sinAlertas = new() { AutoSize = true, ForeColor = Estilo.Ok, Font = Estilo.Negrita };

    public override string Titulo => "Panel general";
    public override TipoVista Tipo => TipoVista.Panel;
    public override Permiso PermisoAcceso => Permiso.Consultar;

    public VistaPanel(Aplicacion app) : base(app)
    {
        var tarjetas = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = true };
        tarjetas.Controls.Add(Tarjeta("Pedidos registrados", out _total));
        tarjetas.Controls.Add(Tarjeta("Pedidos pagados", out _pagados));
        tarjetas.Controls.Add(Tarjeta("Pendientes de despacho", out _pendientes));
        tarjetas.Controls.Add(Tarjeta("Importe cobrado (USD)", out _importe));

        var alertas = Ui.Ui.Pila((Ui.Ui.Etiqueta("Alertas de existencias (stock bajo)", true), 0), (_sinAlertas, 0), (_alertas, 1));
        var recientes = Ui.Ui.Pila((Ui.Ui.Etiqueta("Pedidos recientes", true), 0), (_recientes, 1));
        var cuerpo = new TableLayoutPanel { ColumnCount = 2, RowCount = 1 };
        cuerpo.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
        cuerpo.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
        cuerpo.Controls.Add(recientes, 0, 0);
        cuerpo.Controls.Add(alertas, 1, 0);
        recientes.Margin = new Padding(0, 0, 12, 0);

        Controls.Add(Ui.Ui.Pila((Ui.Ui.Titulo(Titulo), 0), (tarjetas, 0), (_metricas, 0), (cuerpo, 1)));
    }

    private static Panel Tarjeta(string titulo, out Label valor)
    {
        valor = new Label { Text = "0", Font = Estilo.Cifra, ForeColor = Estilo.Principal, AutoSize = true, Location = new Point(12, 28) };
        var panel = new Panel { Size = new Size(240, 84), BackColor = Color.White, BorderStyle = BorderStyle.FixedSingle, Margin = new Padding(0, 0, 12, 12) };
        panel.Controls.Add(new Label { Text = titulo, AutoSize = false, Size = new Size(224, 20), Location = new Point(12, 6), ForeColor = Color.DimGray });
        panel.Controls.Add(valor);
        return panel;
    }

    public override async Task RefrescarAsync(CancellationToken ct = default)
    {
        var pedidos = await App.EjecutarAsync<ConsultasPedidos, IReadOnlyList<PedidoResumenDto>>(c => c.ListarAsync(ct));
        var stock = await App.EjecutarAsync<ConsultasInventario, IReadOnlyList<StockDto>>(c => c.StockAsync(ct));
        var cobrados = pedidos.Where(p => p.Estado is EstadosPedidoTexto.Pagado or EstadosPedidoTexto.Despachado or EstadosPedidoTexto.Entregado).ToList();

        _total.Text = pedidos.Count.ToString();
        _pagados.Text = cobrados.Count.ToString();
        _pendientes.Text = pedidos.Count(p => p.Estado == EstadosPedidoTexto.Pagado).ToString();
        _importe.Text = cobrados.Sum(p => p.Total).ToString("0.00");
        var m = App.Metricas;
        _metricas.Text = $"Indicadores de la sesion:  {m.PedidosPagados} pagados  |  {m.Despachos} despachos  |  {m.Devoluciones} devoluciones  |  {m.Rechazos} rechazos";

        Ui.Ui.Cargar(_recientes, pedidos.Take(12).ToList(), p => p.Id);
        var enAlerta = stock.Where(s => s.Alerta).ToList();
        Ui.Ui.Cargar(_alertas, enAlerta, s => s.Sku);
        _sinAlertas.Text = enAlerta.Count == 0 ? "\u2714 Sin alertas: todas las existencias estan por encima del umbral." : $"\u26A0 {enAlerta.Count} producto(s) con stock bajo (umbral {ConsultasInventario.UmbralAlerta})";
        _sinAlertas.ForeColor = enAlerta.Count == 0 ? Estilo.Ok : Estilo.Error;
    }
}
