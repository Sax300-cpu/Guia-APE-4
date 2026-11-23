using LogiMarket.Application.Comun;
using LogiMarket.Application.Consultas;
using LogiMarket.Application.Coordinacion;
using LogiMarket.Application.Seguridad;
using LogiMarket.Application.Servicios;
using LogiMarket.WinForms.Infraestructura;
using LogiMarket.WinForms.Ui;

namespace LogiMarket.WinForms.Vistas;

/// <summary>Pantalla 06 - Pedidos e historial: consulta de estados y trazabilidad de cambios.</summary>
internal sealed class VistaPedidos : VistaBase
{
    private readonly ComboBox _estado = Ui.Ui.Combo("Filtrar por estado", 150, "(todos)", "Borrador", "Validado", "Pagado", "Despachado", "Entregado", "Cancelado");
    private readonly TextBox _cliente = Ui.Ui.Caja("Filtrar por cliente", 180);
    private readonly DataGridView _pedidos = Ui.Ui.Grid("Pedidos",
        new Col("N.", nameof(PedidoResumenDto.Id), 30), new Col("Cliente", nameof(PedidoResumenDto.Cliente), 90), new Col("Estado", nameof(PedidoResumenDto.Estado), 60),
        new Col("Total (USD)", nameof(PedidoResumenDto.Total), 60, "0.00", true), new Col("Fecha", nameof(PedidoResumenDto.Fecha), 90, "g"));
    private readonly DataGridView _lineas = Ui.Ui.Grid("Lineas del pedido",
        new Col("SKU", nameof(LineaPedidoDto.Sku), 80), new Col("Cant.", nameof(LineaPedidoDto.Cantidad), 35, null, true),
        new Col("Precio unit. (USD)", nameof(LineaPedidoDto.PrecioUnitario), 60, "0.00", true), new Col("Subtotal (USD)", nameof(LineaPedidoDto.Subtotal), 60, "0.00", true));
    private readonly DataGridView _historial = Ui.Ui.Grid("Historial de estados",
        new Col("Fecha", nameof(HistorialEstadoDto.Fecha), 90, "g"), new Col("Desde", nameof(HistorialEstadoDto.Desde), 60), new Col("Hacia", nameof(HistorialEstadoDto.Hacia), 60));
    private readonly Label _detalle = new() { AutoSize = true, Font = Estilo.Negrita };
    private readonly Label _resultado = Ui.Ui.Mensaje("Resultado de pedidos");
    private bool _cargando;

    public override string Titulo => "Pedidos e historial";
    public override TipoVista Tipo => TipoVista.Pedidos;
    public override Permiso PermisoAcceso => Permiso.Consultar;

    public VistaPedidos(Aplicacion app) : base(app)
    {
        _estado.SelectedIndexChanged += async (_, _) => await Seguro(() => RefrescarAsync());
        _cliente.KeyDown += async (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await Seguro(() => RefrescarAsync()); } };
        _pedidos.SelectionChanged += async (_, _) => { if (!_cargando) await Seguro(MostrarDetalleAsync); };

        var detalle = new TableLayoutPanel { ColumnCount = 2, RowCount = 1 };
        detalle.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        detalle.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        var izq = Ui.Ui.Pila((Ui.Ui.Etiqueta("Lineas del pedido (instantanea de precios)", true), 0), (_lineas, 1));
        var der = Ui.Ui.Pila((Ui.Ui.Etiqueta("Historial de estados", true), 0), (_historial, 1));
        izq.Margin = new Padding(0, 0, 12, 0);
        detalle.Controls.Add(izq, 0, 0);
        detalle.Controls.Add(der, 1, 0);

        Controls.Add(Ui.Ui.Pila(
            (Ui.Ui.Titulo(Titulo), 0),
            (Ui.Ui.Fila(Ui.Ui.Campo("&Estado", _estado), Ui.Ui.Campo("&Cliente", _cliente), Boton("&Aplicar filtros", () => RefrescarAsync()),
                Boton("Cancelar pedido &seleccionado", CancelarAsync, Permiso.GestionarPedidos)), 0),
            (_pedidos, 1), (_detalle, 0), (detalle, 1), (_resultado, 0)));
    }

    public override async Task RefrescarAsync(CancellationToken ct = default)
    {
        var todos = await App.EjecutarAsync<ConsultasPedidos, IReadOnlyList<PedidoResumenDto>>(c => c.ListarAsync(ct));
        var estado = _estado.SelectedItem as string;
        var cliente = _cliente.Text.Trim();
        var filtrados = todos.Where(p => (estado is null or "(todos)" || p.Estado == estado)
                                         && (cliente.Length == 0 || p.Cliente.Contains(cliente, StringComparison.OrdinalIgnoreCase))).ToList();
        _cargando = true;
        Ui.Ui.Cargar(_pedidos, filtrados, p => p.Id);
        _cargando = false;
        await MostrarDetalleAsync();
    }

    private async Task MostrarDetalleAsync()
    {
        if (Ui.Ui.Seleccion<PedidoResumenDto>(_pedidos) is not { } p)
        {
            _detalle.Text = "Selecciona un pedido para ver su detalle.";
            _lineas.DataSource = null;
            _historial.DataSource = null;
            return;
        }
        var d = await App.EjecutarAsync<ConsultasPedidos, PedidoDetalleDto?>(c => c.ObtenerAsync(p.Id));
        if (d is null) return;
        _detalle.Text = $"Pedido #{d.Id} - {d.Cliente} - {d.Estado.ToUpperInvariant()}  |  Subtotal {Ui.Ui.Usd(d.Subtotal)}  |  Descuento {Ui.Ui.Usd(d.Descuento)} ({d.Politica})  |  Envio {Ui.Ui.Usd(d.Tarifa)}  |  TOTAL {Ui.Ui.Usd(d.Total)}";
        Ui.Ui.Cargar(_lineas, d.Lineas);
        Ui.Ui.Cargar(_historial, d.Historial);
    }

    private async Task CancelarAsync()
    {
        if (Ui.Ui.Seleccion<PedidoResumenDto>(_pedidos) is not { } p) { Ui.Ui.MostrarResultado(_resultado, false, "Selecciona un pedido."); return; }
        if (!Ui.Ui.Confirmar(FindForm(), $"Cancelar el pedido #{p.Id}? Solo es posible desde BORRADOR o VALIDADO y no se puede deshacer.")) return;
        var r = await App.EjecutarAsync<ServicioPedidos, ResultadoOperacion>(s => s.CancelarAsync(p.Id));
        Ui.Ui.MostrarResultado(_resultado, r.Exito, r.Exito ? $"Pedido #{p.Id} cancelado." : r.Mensaje);
        if (r.Exito) await Notificar(OperacionUI.PedidoCancelado);
        await RefrescarAsync();
    }
}
