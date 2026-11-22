using LogiMarket.Application.Comandos;
using LogiMarket.Application.Consultas;
using LogiMarket.Application.Coordinacion;
using LogiMarket.Application.Seguridad;
using LogiMarket.Application.Servicios;
using LogiMarket.WinForms.Infraestructura;
using LogiMarket.WinForms.Ui;
using Microsoft.Extensions.DependencyInjection;

namespace LogiMarket.WinForms.Vistas;

/// <summary>Pantalla 07 - Confirmacion y cobro: importes, proveedor y cobro protegido con clave de idempotencia.</summary>
internal sealed class VistaCobro : VistaBase
{
    private readonly ComboBox _pedido = Ui.Ui.Combo("Pedido a cobrar", 320), _proveedor = Ui.Ui.Combo("Proveedor de cobro", 140, "SIMULADA", "LEGADA");
    private readonly TextBox _clave = Ui.Ui.Caja("Clave de idempotencia", 300);
    private readonly Label _subtotal = new() { AutoSize = true }, _descuento = new() { AutoSize = true }, _tarifa = new() { AutoSize = true },
        _total = new() { AutoSize = true, Font = Estilo.Cifra, ForeColor = Estilo.Principal }, _estado = new() { AutoSize = true, Font = Estilo.Negrita };
    private readonly Label _resultado = Ui.Ui.Mensaje("Resultado del cobro");
    private readonly DataGridView _lineas = Ui.Ui.Grid("Lineas a cobrar",
        new Col("SKU", nameof(LineaPedidoDto.Sku), 80), new Col("Cant.", nameof(LineaPedidoDto.Cantidad), 35, null, true), new Col("Subtotal (USD)", nameof(LineaPedidoDto.Subtotal), 60, "0.00", true));
    private bool _cargando;

    public override string Titulo => "Cobro";
    public override TipoVista Tipo => TipoVista.Cobro;
    public override Permiso PermisoAcceso => Permiso.CobrarPedido;

    public VistaCobro(Aplicacion app) : base(app)
    {
        _proveedor.SelectedItem = app.Demo.ProveedorPago;
        _proveedor.SelectedIndexChanged += (_, _) => App.Demo.ProveedorPago = (string)_proveedor.SelectedItem!;
        _pedido.SelectedIndexChanged += async (_, _) => { if (!_cargando) await Seguro(MostrarAsync); };
        _clave.Text = NuevaClave();

        var totales = new TableLayoutPanel { AutoSize = true, ColumnCount = 2 };
        void Fila(string texto, Control valor, bool negrita = false)
        {
            totales.Controls.Add(new Label { Text = texto, AutoSize = true, Font = negrita ? Estilo.Negrita : Estilo.Fuente, Margin = new Padding(0, 3, 24, 3) });
            totales.Controls.Add(valor);
        }
        Fila("Estado:", _estado, true);
        Fila("Subtotal:", _subtotal);
        Fila("Descuento:", _descuento);
        Fila("Costo de entrega:", _tarifa);
        Fila("TOTAL A COBRAR:", _total, true);

        Controls.Add(Ui.Ui.Pila(
            (Ui.Ui.Titulo(Titulo), 0),
            (Ui.Ui.Fila(Ui.Ui.Campo("&Pedido (borrador o validado)", _pedido), Ui.Ui.Campo("Pro&veedor de cobro", _proveedor)), 0),
            (_lineas, 1), (Ui.Ui.Grupo("Resumen de importes", totales), 0),
            (Ui.Ui.Fila(Ui.Ui.Campo("&Clave de idempotencia (la misma clave nunca cobra dos veces)", _clave),
                Boton("&Nueva clave", () => { _clave.Text = NuevaClave(); return Task.CompletedTask; }),
                Boton("C&obrar pedido", CobrarAsync, Permiso.CobrarPedido)), 0),
            (_resultado, 0)));
    }

    private static string NuevaClave() => "K-" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();

    public override async Task RefrescarAsync(CancellationToken ct = default)
    {
        var pedidos = await App.EjecutarAsync<ConsultasPedidos, IReadOnlyList<PedidoResumenDto>>(c => c.ListarAsync(ct));
        _cargando = true;
        _pedido.Cargar(pedidos.Where(p => p.Estado is "Borrador" or "Validado").Select(p => new Opcion<int>($"#{p.Id} {p.Cliente} - {p.Estado} - USD {p.Total:0.00}", p.Id)));
        _proveedor.SelectedItem = App.Demo.ProveedorPago;
        _cargando = false;
        await MostrarAsync();
    }

    private async Task MostrarAsync()
    {
        var id = _pedido.Valor<int>();
        var d = id == 0 ? null : await App.EjecutarAsync<ConsultasPedidos, PedidoDetalleDto?>(c => c.ObtenerAsync(id));
        if (d is null)
        {
            _estado.Text = "No hay pedidos pendientes de cobro.";
            _subtotal.Text = _descuento.Text = _tarifa.Text = "-";
            _total.Text = Ui.Ui.Usd(0);
            _lineas.DataSource = null;
            return;
        }
        _estado.Text = $"#{d.Id} - {d.Cliente} - {d.Estado.ToUpperInvariant()}";
        _subtotal.Text = Ui.Ui.Usd(d.Subtotal);
        _descuento.Text = $"{Ui.Ui.Usd(d.Descuento)}  ({d.Politica})";
        _tarifa.Text = $"{Ui.Ui.Usd(d.Tarifa)}  ({d.Entrega})";
        _total.Text = Ui.Ui.Usd(d.Total);
        Ui.Ui.Cargar(_lineas, d.Lineas);
    }

    private async Task CobrarAsync()
    {
        var id = _pedido.Valor<int>();
        if (id == 0) { Ui.Ui.MostrarResultado(_resultado, false, "No hay un pedido seleccionado."); return; }
        if (!Ui.Ui.Requerido(Errores, _clave, "La clave de idempotencia es obligatoria.")) return;
        if (!Ui.Ui.Confirmar(FindForm(), $"Cobrar {_total.Text} del pedido #{id} con el proveedor {_proveedor.SelectedItem}? El cobro y el descuento de stock no se pueden deshacer.")) return;

        // La misma accion que ejecutaria una prueba: ConfirmarPedidoCommand, registrado por el invocador.
        var clave = _clave.Text.Trim();
        var r = await App.EjecutarAsync(async sp =>
            await sp.GetRequiredService<InvocadorComandos>().EjecutarAsync(new ConfirmarPedidoCommand(sp.GetRequiredService<ConfirmacionPedidoFacade>(), id, clave)));

        var texto = r.Exito
            ? (r.EsRepeticion ? $"Operacion repetida con la misma clave: se devuelve el resultado original ({Ui.Ui.Usd(r.Total)}); no hubo un segundo cobro." : $"Pedido #{id} pagado: {Ui.Ui.Usd(r.Total)}.")
            : $"Rechazado ({r.Codigo}){(r.Eslabon is null ? "" : $" en el eslabon {r.Eslabon}")}: {r.Mensaje}";
        Ui.Ui.MostrarResultado(_resultado, r.Exito, texto);
        if (r.Exito) await Notificar(OperacionUI.PedidoConfirmado);
        await RefrescarAsync();
    }
}