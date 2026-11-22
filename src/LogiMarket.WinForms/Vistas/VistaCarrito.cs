using LogiMarket.Application.Comun;
using LogiMarket.Application.Consultas;
using LogiMarket.Application.Coordinacion;
using LogiMarket.Application.Seguridad;
using LogiMarket.Application.Servicios;
using LogiMarket.Domain.Pedidos;
using LogiMarket.WinForms.Infraestructura;
using LogiMarket.WinForms.Ui;

namespace LogiMarket.WinForms.Vistas;

/// <summary>Pantalla 05 - Carrito y borrador: lineas, totales verificables y Deshacer/Rehacer (Memento).</summary>
internal sealed class VistaCarrito : VistaBase
{
    private readonly ComboBox _cliente = Ui.Ui.Combo("Cliente del pedido", 200), _entrega = Ui.Ui.Combo("Tipo de entrega", 130, TipoEntrega.Estandar, TipoEntrega.Expres),
        _borradores = Ui.Ui.Combo("Borradores existentes", 260), _producto = Ui.Ui.Combo("Producto o kit", 300);
    private readonly NumericUpDown _cantidad = Ui.Ui.Entero("Cantidad", 1, 10000, 1);
    private readonly DataGridView _lineas = Ui.Ui.Grid("Lineas del borrador",
        new Col("SKU", nameof(LineaPedidoDto.Sku), 70), new Col("Descripcion", nameof(LineaPedidoDto.Nombre), 110), new Col("Cant.", nameof(LineaPedidoDto.Cantidad), 35, null, true),
        new Col("Precio unit. (USD)", nameof(LineaPedidoDto.PrecioUnitario), 60, "0.00", true), new Col("Subtotal (USD)", nameof(LineaPedidoDto.Subtotal), 60, "0.00", true),
        new Col("Unid. fisicas", nameof(LineaPedidoDto.UnidadesFisicas), 45, null, true));
    private readonly Label _estado = new() { AutoSize = true, Font = Estilo.Negrita, ForeColor = Estilo.Principal, Name = "Estado del borrador", AccessibleName = "Estado del borrador" };
    private readonly Label _subtotal = new() { AutoSize = true }, _descuento = new() { AutoSize = true }, _tarifa = new() { AutoSize = true },
        _total = new() { AutoSize = true, Font = Estilo.Cifra, ForeColor = Estilo.Principal, Name = "Total del borrador", AccessibleName = "Total del borrador" };
    private readonly Label _resultado = Ui.Ui.Mensaje("Resultado del carrito");
    private int _pedidoId;
    private bool _cargando;

    public override string Titulo => "Carrito / borrador";
    public override TipoVista Tipo => TipoVista.Carrito;
    public override Permiso PermisoAcceso => Permiso.GestionarPedidos;

    public VistaCarrito(Aplicacion app) : base(app)
    {
        var cabecera = Ui.Ui.Grupo("Pedido", Ui.Ui.Fila(
            Ui.Ui.Campo("&Cliente", _cliente), Ui.Ui.Campo("&Entrega", _entrega),
            Boton("&Nuevo borrador", () => { Nuevo(); return Task.CompletedTask; }, Permiso.GestionarPedidos),
            Ui.Ui.Campo("Borradores e&xistentes", _borradores), Boton("Ca&rgar", CargarSeleccionadoAsync)), 160);
        var lineas = Ui.Ui.Grupo("Agregar o cambiar lineas", Ui.Ui.Fila(
            Ui.Ui.Campo("&Producto o kit", _producto), Ui.Ui.Campo("C&antidad", _cantidad),
            Boton("A&gregar linea", AgregarAsync, Permiso.GestionarPedidos), Boton("Cam&biar cantidad de la fila", CambiarAsync, Permiso.GestionarPedidos)), 104);
        var acciones = Ui.Ui.Fila(
            Boton("&Deshacer", () => EditarAsync(s => s.DeshacerAsync(_pedidoId)), Permiso.GestionarPedidos),
            Boton("Reha&cer", () => EditarAsync(s => s.RehacerAsync(_pedidoId)), Permiso.GestionarPedidos),
            Boton("D&uplicar como plantilla", DuplicarAsync, Permiso.GestionarPedidos),
            Boton("&Validar pedido", ValidarAsync, Permiso.GestionarPedidos),
            Boton("Cance&lar pedido", CancelarAsync, Permiso.GestionarPedidos));

        var totales = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, Dock = DockStyle.Fill };
        void Fila(string texto, Control valor, bool negrita = false)
        {
            totales.Controls.Add(new Label { Text = texto, AutoSize = true, Font = negrita ? Estilo.Negrita : Estilo.Fuente, Margin = new Padding(0, 3, 24, 3) });
            totales.Controls.Add(valor);
        }
        Fila("Estado:", _estado, true);
        Fila("Subtotal:", _subtotal);
        Fila("Descuento:", _descuento);
        Fila("Costo de entrega:", _tarifa);
        Fila("TOTAL:", _total, true);

        Controls.Add(Ui.Ui.Pila(
            (Ui.Ui.Titulo(Titulo), 0), (cabecera, 0), (lineas, 0), (_lineas, 1), (acciones, 0),
            (Ui.Ui.Grupo("Totales del borrador", totales), 0), (_resultado, 0)));
        Nuevo();
    }

    private void Nuevo()
    {
        _pedidoId = 0;
        _lineas.DataSource = null;
        _estado.Text = "Sin borrador (se creara al agregar la primera linea)";
        _subtotal.Text = _descuento.Text = _tarifa.Text = "-";
        _total.Text = Ui.Ui.Usd(0);
        _cliente.Enabled = _entrega.Enabled = true;
        _resultado.Text = string.Empty;
    }

    public override async Task RefrescarAsync(CancellationToken ct = default)
    {
        _cargando = true;
        var clientes = await App.EjecutarAsync<ServicioClientes, IReadOnlyList<ClienteDto>>(s => s.ListarAsync(null, ct));
        var catalogo = await App.EjecutarAsync<ConsultasCatalogo, IReadOnlyList<CatalogoFilaDto>>(c => c.ListarAsync(null, ct));
        var pedidos = await App.EjecutarAsync<ConsultasPedidos, IReadOnlyList<PedidoResumenDto>>(c => c.ListarAsync(ct));
        _cliente.Cargar(clientes.Where(c => c.Activo).Select(c => new Opcion<int>($"{c.Nombre} ({c.Categoria})", c.Id)));
        _producto.Cargar(catalogo.Select(f => new Opcion<string>($"{f.Sku} - {f.Nombre} (USD {f.Precio:0.00})", f.Sku)));
        _borradores.Cargar(pedidos.Where(p => p.Estado == "Borrador").Select(p => new Opcion<int>($"#{p.Id} {p.Cliente} - USD {p.Total:0.00}", p.Id)));
        _cargando = false;
        if (_pedidoId != 0) await MostrarAsync(_pedidoId);
    }

    internal Task CargarAsync(int id) => MostrarAsync(id); // gancho para la autoprueba

    private async Task MostrarAsync(int id)
    {
        var d = await App.EjecutarAsync<ConsultasPedidos, PedidoDetalleDto?>(c => c.ObtenerAsync(id));
        if (d is null) { Nuevo(); return; }
        _pedidoId = d.Id;
        Ui.Ui.Cargar(_lineas, d.Lineas, l => l.Sku);
        _estado.Text = $"#{d.Id} - {d.Cliente} - {d.Estado.ToUpperInvariant()}" + (d.PermiteEdicion ? " (editable)" : " (no editable)");
        _subtotal.Text = Ui.Ui.Usd(d.Subtotal);
        _descuento.Text = $"{Ui.Ui.Usd(d.Descuento)}  ({d.Politica})";
        _tarifa.Text = $"{Ui.Ui.Usd(d.Tarifa)}  ({d.Entrega}, {d.PlazoDias} dia(s))";
        _total.Text = Ui.Ui.Usd(d.Total);
        _cliente.Enabled = _entrega.Enabled = false;
    }

    private async Task CargarSeleccionadoAsync()
    {
        if (_cargando || _borradores.Valor<int>() == 0) return;
        await MostrarAsync(_borradores.Valor<int>());
        Ui.Ui.MostrarResultado(_resultado, true, "Borrador cargado.");
    }

    private async Task AgregarAsync()
    {
        if (!Ui.Ui.Requerido(Errores, _producto, "Elige un producto o kit.")) return;
        var sku = _producto.Valor<string>()!;
        var cantidad = (int)_cantidad.Value;
        if (_pedidoId == 0)
        {
            if (!Ui.Ui.Requerido(Errores, _cliente, "Elige el cliente del pedido.")) return;
            var clienteId = _cliente.Valor<int>();
            var tipo = (TipoEntrega)_entrega.SelectedItem!;
            _pedidoId = await App.EjecutarAsync<ServicioPedidos, int>(s => s.CrearBorradorAsync(clienteId, tipo, new[] { new LineaSolicitada(sku, cantidad) }));
            Ui.Ui.MostrarResultado(_resultado, true, $"Borrador #{_pedidoId} creado.");
        }
        else
        {
            await EditarAsync(s => s.AgregarLineaAsync(_pedidoId, sku, cantidad));
            return;
        }
        await RefrescarAsync();
        await Notificar(OperacionUI.BorradorEditado);
    }

    private async Task CambiarAsync()
    {
        if (_pedidoId == 0 || Ui.Ui.Seleccion<LineaPedidoDto>(_lineas) is not { } linea)
        {
            Ui.Ui.MostrarResultado(_resultado, false, "Selecciona una linea del borrador.");
            return;
        }
        await EditarAsync(s => s.CambiarCantidadAsync(_pedidoId, linea.Sku, (int)_cantidad.Value));
    }

    private async Task EditarAsync(Func<ServicioEdicionBorrador, Task<ResultadoOperacion>> operacion)
    {
        if (_pedidoId == 0) { Ui.Ui.MostrarResultado(_resultado, false, "Primero crea o carga un borrador."); return; }
        var r = await App.EjecutarAsync<ServicioEdicionBorrador, ResultadoOperacion>(operacion);
        Ui.Ui.MostrarResultado(_resultado, r.Exito, r.Exito ? "Borrador actualizado." : r.Mensaje);
        await RefrescarAsync();
        await Notificar(OperacionUI.BorradorEditado);
    }

    private async Task DuplicarAsync()
    {
        if (_pedidoId == 0) { Ui.Ui.MostrarResultado(_resultado, false, "No hay borrador para duplicar."); return; }
        var origen = _pedidoId;
        _pedidoId = await App.EjecutarAsync<ServicioPedidos, int>(s => s.ClonarBorradorAsync(origen));
        Ui.Ui.MostrarResultado(_resultado, true, $"Plantilla creada: borrador #{_pedidoId} (copia profunda de #{origen}, sin pago ni identificador heredado).");
        await RefrescarAsync();
        await Notificar(OperacionUI.BorradorEditado);
    }

    private async Task ValidarAsync()
    {
        if (_pedidoId == 0) { Ui.Ui.MostrarResultado(_resultado, false, "No hay borrador para validar."); return; }
        var r = await App.EjecutarAsync<ServicioPedidos, ResultadoOperacion>(s => s.ValidarAsync(_pedidoId));
        Ui.Ui.MostrarResultado(_resultado, r.Exito, r.Exito ? "Pedido VALIDADO: ya puede cobrarse en la pantalla Cobro." : $"No se valido ({r.Codigo}): {r.Mensaje}");
        await RefrescarAsync();
        await Notificar(OperacionUI.BorradorEditado);
    }

    private async Task CancelarAsync()
    {
        if (_pedidoId == 0) { Ui.Ui.MostrarResultado(_resultado, false, "No hay pedido para cancelar."); return; }
        if (!Ui.Ui.Confirmar(FindForm(), $"Cancelar el pedido #{_pedidoId}? Esta accion no se puede deshacer.")) return;
        var r = await App.EjecutarAsync<ServicioPedidos, ResultadoOperacion>(s => s.CancelarAsync(_pedidoId));
        Ui.Ui.MostrarResultado(_resultado, r.Exito, r.Exito ? "Pedido cancelado." : r.Mensaje);
        await RefrescarAsync();
        await Notificar(OperacionUI.PedidoCancelado);
    }
}