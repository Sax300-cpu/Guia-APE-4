using LogiMarket.Application.Comprobantes;
using LogiMarket.Application.Consultas;
using LogiMarket.Application.Coordinacion;
using LogiMarket.Application.Seguridad;
using LogiMarket.Application.Servicios;
using LogiMarket.WinForms.Infraestructura;
using LogiMarket.WinForms.Ui;

namespace LogiMarket.WinForms.Vistas;

/// <summary>Pantalla 11 - Reportes y comprobantes: filtros (Interpreter) y combinacion de tipo de comprobante y medio de salida (Bridge).</summary>
internal sealed class VistaReportes : VistaBase
{
    private readonly TextBox _filtro = Ui.Ui.Caja("Filtro de consulta", 460);
    private readonly Label _error = new() { AutoSize = true, ForeColor = Estilo.Error, Font = Estilo.Negrita };
    private readonly Label _conteo = new() { AutoSize = true, Font = Estilo.Negrita };
    private readonly DataGridView _grid = Ui.Ui.Grid("Resultado del reporte",
        new Col("N.", nameof(PedidoResumenDto.Id), 30), new Col("Cliente", nameof(PedidoResumenDto.Cliente), 90), new Col("Estado", nameof(PedidoResumenDto.Estado), 60),
        new Col("Total (USD)", nameof(PedidoResumenDto.Total), 60, "0.00", true), new Col("Fecha", nameof(PedidoResumenDto.Fecha), 90, "g"));
    private readonly ComboBox _tipo = Ui.Ui.Combo("Tipo de comprobante", 150, "Venta", "Devolucion"), _formato = Ui.Ui.Combo("Medio de salida", 160, "VISTA_PREVIA", "TXT");
    private readonly TextBox _vista = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, Font = new Font("Consolas", 9.5f), AccessibleName = "Vista previa del comprobante", WordWrap = false, BackColor = Color.White };
    private readonly Label _resultado = Ui.Ui.Mensaje("Resultado del comprobante");

    public override string Titulo => "Reportes y comprobantes";
    public override TipoVista Tipo => TipoVista.Reportes;
    public override Permiso PermisoAcceso => Permiso.Consultar;

    public VistaReportes(Aplicacion app) : base(app)
    {
        _filtro.KeyDown += async (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await Seguro(() => RefrescarAsync()); } };
        var filtros = Ui.Ui.Grupo("Filtro de pedidos (ESTADO = valor, TOTAL >= numero, CLIENTE = texto; AND / OR y parentesis)", Ui.Ui.Fila(
            Ui.Ui.Campo("&Consulta (vacio = todos)", _filtro), Boton("&Aplicar", () => RefrescarAsync()),
            Boton("&Limpiar", () => { _filtro.Clear(); return RefrescarAsync(); })), 104);
        var emision = Ui.Ui.Grupo("Comprobante del pedido seleccionado (documento interno de demostracion)", Ui.Ui.Fila(
            Ui.Ui.Campo("&Tipo", _tipo), Ui.Ui.Campo("&Medio de salida", _formato), Boton("&Emitir comprobante", EmitirAsync, Permiso.Consultar)), 104);

        Controls.Add(Ui.Ui.Pila(
            (Ui.Ui.Titulo(Titulo), 0), (filtros, 0), (Ui.Ui.Fila(_error, _conteo), 0), (_grid, 1),
            (emision, 0), (_vista, 1), (_resultado, 0)));
    }

    public override async Task RefrescarAsync(CancellationToken ct = default)
    {
        _error.Text = string.Empty;
        try
        {
            var texto = _filtro.Text.Trim();
            var datos = await App.EjecutarAsync<ConsultasPedidos, IReadOnlyList<PedidoResumenDto>>(c => texto.Length == 0 ? c.ListarAsync(ct) : c.FiltrarAsync(texto, ct));
            Ui.Ui.Cargar(_grid, datos, p => p.Id);
            _conteo.Text = $"   {datos.Count} pedido(s) coinciden";
        }
        catch (ErrorSintaxisConsultaException ex)
        {
            _error.Text = "Consulta no valida: " + ex.Message;
            _conteo.Text = string.Empty;
        }
    }

    /// <summary>Gancho para la autoprueba: aplica un filtro, selecciona un pedido y emite la vista previa de su comprobante.</summary>
    internal async Task DemostrarAsync(string filtro, int pedidoId)
    {
        _filtro.Text = filtro;
        await RefrescarAsync();
        foreach (DataGridViewRow fila in _grid.Rows)
            if (fila.DataBoundItem is PedidoResumenDto p && p.Id == pedidoId) { _grid.CurrentCell = fila.Cells[0]; break; }
        _tipo.SelectedIndex = 0;
        _formato.SelectedIndex = 0;
        await EmitirAsync();
    }

    private async Task EmitirAsync()
    {
        if (Ui.Ui.Seleccion<PedidoResumenDto>(_grid) is not { } p) { Ui.Ui.MostrarResultado(_resultado, false, "Selecciona un pedido de la tabla."); return; }
        var formato = (string)_formato.SelectedItem!;
        var venta = (string)_tipo.SelectedItem! == "Venta";
        var salida = await App.EjecutarAsync<ServicioComprobantes, ResultadoSalida>(s =>
            venta ? s.EmitirVentaAsync(p.Id, formato) : s.EmitirDevolucionAsync(p.Id, formato));
        _vista.Text = salida.Contenido;
        Ui.Ui.MostrarResultado(_resultado, true, formato == "TXT" ? $"Archivo generado: {salida.Destino}" : "Vista previa generada (el contenido es el mismo que se escribiria en el archivo).");
    }
}
