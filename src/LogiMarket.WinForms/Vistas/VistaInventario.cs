using LogiMarket.Application.Consultas;
using LogiMarket.Application.Coordinacion;
using LogiMarket.Application.Seguridad;
using LogiMarket.Application.Servicios;
using LogiMarket.WinForms.Infraestructura;
using LogiMarket.WinForms.Ui;

namespace LogiMarket.WinForms.Vistas;

/// <summary>Pantalla 10 - Inventario y movimientos: existencias, alertas y registros trazables.</summary>
internal sealed class VistaInventario : VistaBase
{
    private readonly DataGridView _stock = Ui.Ui.Grid("Existencias",
        new Col("SKU", nameof(StockDto.Sku), 40), new Col("Producto", nameof(StockDto.Nombre), 100),
        new Col("Stock", nameof(StockDto.Stock), 40, null, true), new Col("Alerta", nameof(StockDto.Alerta), 40));
    private readonly DataGridView _movimientos = Ui.Ui.Grid("Movimientos de inventario",
        new Col("Fecha", nameof(MovimientoDto.Fecha), 80, "g"), new Col("SKU", nameof(MovimientoDto.Sku), 40),
        new Col("Cantidad", nameof(MovimientoDto.Cantidad), 40, "+0;-0", true), new Col("Motivo", nameof(MovimientoDto.Motivo), 60),
        new Col("Referencia", nameof(MovimientoDto.Referencia), 80), new Col("Stock resultante", nameof(MovimientoDto.StockResultante), 50, null, true));
    private readonly TextBox _sku = Ui.Ui.Caja("Filtrar movimientos por SKU", 120);
    private readonly Label _resumen = new() { AutoSize = true, Font = Estilo.Negrita };

    public override string Titulo => "Inventario y movimientos";
    public override TipoVista Tipo => TipoVista.Inventario;
    public override Permiso PermisoAcceso => Permiso.Consultar;

    public VistaInventario(Aplicacion app) : base(app)
    {
        _stock.CellFormatting += (_, e) =>
        {
            if (e.RowIndex >= 0 && _stock.Rows[e.RowIndex].DataBoundItem is StockDto { Alerta: true })
                _stock.Rows[e.RowIndex].DefaultCellStyle.BackColor = Estilo.Alerta;
        };
        _sku.KeyDown += async (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await Seguro(() => RefrescarAsync()); } };
        Controls.Add(Ui.Ui.Pila(
            (Ui.Ui.Titulo(Titulo), 0), (_resumen, 0), (_stock, 1),
            (Ui.Ui.Fila(Ui.Ui.Campo("Mo&vimientos del SKU (vacio = todos)", _sku), Boton("&Filtrar", () => RefrescarAsync())), 0),
            (_movimientos, 2)));
    }

    public override async Task RefrescarAsync(CancellationToken ct = default)
    {
        var stock = await App.EjecutarAsync<ConsultasInventario, IReadOnlyList<StockDto>>(c => c.StockAsync(ct));
        var movimientos = await App.EjecutarAsync<ConsultasInventario, IReadOnlyList<MovimientoDto>>(c => c.MovimientosAsync(_sku.Text, 200, ct));
        Ui.Ui.Cargar(_stock, stock, s => s.Sku);
        Ui.Ui.Cargar(_movimientos, movimientos);
        var alertas = stock.Count(s => s.Alerta);
        _resumen.Text = alertas == 0 ? "Sin alertas de existencias." : $"\u26A0 {alertas} producto(s) con stock bajo (umbral {ConsultasInventario.UmbralAlerta}), resaltados en rojo.";
        _resumen.ForeColor = alertas == 0 ? Estilo.Ok : Estilo.Error;
    }
}
