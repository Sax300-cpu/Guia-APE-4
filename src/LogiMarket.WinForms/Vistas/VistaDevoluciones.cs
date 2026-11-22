using LogiMarket.Application.Comandos;
using LogiMarket.Application.Consultas;
using LogiMarket.Application.Coordinacion;
using LogiMarket.Application.Seguridad;
using LogiMarket.Application.Servicios;
using LogiMarket.Domain.Devoluciones;
using LogiMarket.WinForms.Infraestructura;
using LogiMarket.WinForms.Ui;
using Microsoft.Extensions.DependencyInjection;

namespace LogiMarket.WinForms.Vistas;

/// <summary>Pantalla 09 - Devoluciones: politica por motivo, reintegro unico de stock y reembolso simulado unico.</summary>
internal sealed class VistaDevoluciones : VistaBase
{
    private readonly DataGridView _grid = Ui.Ui.Grid("Pedidos entregados",
        new Col("N.", nameof(PedidoResumenDto.Id), 30), new Col("Cliente", nameof(PedidoResumenDto.Cliente), 90),
        new Col("Total pagado (USD)", nameof(PedidoResumenDto.Total), 60, "0.00", true), new Col("Fecha", nameof(PedidoResumenDto.Fecha), 90, "g"));
    private readonly ComboBox _motivo = Ui.Ui.Combo("Motivo de devolucion", 190, MotivoDevolucion.Defecto, MotivoDevolucion.Arrepentimiento);
    private readonly CheckBox _sinUso = Ui.Ui.Casilla("Producto sin uso (requerido por arrepentimiento)", "Producto sin uso");
    private readonly Label _resultado = Ui.Ui.Mensaje("Resultado de la devolucion");

    public override string Titulo => "Devoluciones";
    public override TipoVista Tipo => TipoVista.Devoluciones;
    public override Permiso PermisoAcceso => Permiso.ProcesarDevolucion;

    public VistaDevoluciones(Aplicacion app) : base(app)
    {
        Controls.Add(Ui.Ui.Pila(
            (Ui.Ui.Titulo(Titulo), 0),
            (Ui.Ui.Etiqueta("Devolucion TOTAL de pedidos ENTREGADOS. DEFECTO: hasta 30 dias. ARREPENTIMIENTO: hasta 7 dias y sin uso. El reembolso no incluye la tarifa de envio. Un pedido ya devuelto sigue listado: repetir la solicitud se rechaza (devolucion unica)."), 0),
            (_grid, 1),
            (Ui.Ui.Fila(Ui.Ui.Campo("&Motivo", _motivo), _sinUso, Boton("&Solicitar devolucion", SolicitarAsync, Permiso.ProcesarDevolucion)), 0),
            (_resultado, 0)));
    }

    public override async Task RefrescarAsync(CancellationToken ct = default)
    {
        var pedidos = await App.EjecutarAsync<ConsultasPedidos, IReadOnlyList<PedidoResumenDto>>(c => c.ListarAsync(ct));
        Ui.Ui.Cargar(_grid, pedidos.Where(p => p.Estado == "Entregado").ToList(), p => p.Id);
    }

    private async Task SolicitarAsync()
    {
        if (Ui.Ui.Seleccion<PedidoResumenDto>(_grid) is not { } p) { Ui.Ui.MostrarResultado(_resultado, false, "Selecciona un pedido entregado."); return; }
        var solicitud = new SolicitudDevolucion(p.Id, (MotivoDevolucion)_motivo.SelectedItem!, _sinUso.Checked);
        if (!Ui.Ui.Confirmar(FindForm(), $"Procesar la devolucion total del pedido #{p.Id} por {solicitud.Motivo}? El reintegro de stock y el reembolso se hacen una sola vez.")) return;

        var r = await App.EjecutarAsync(async sp =>
            await sp.GetRequiredService<InvocadorComandos>().EjecutarAsync(new ProcesarDevolucionCommand(sp.GetRequiredService<ServicioDevoluciones>(), solicitud)));
        Ui.Ui.MostrarResultado(_resultado, r.Exito, r.Exito
            ? $"Devolucion aceptada del pedido #{p.Id}. Reembolso simulado: {Ui.Ui.Usd(r.Reembolso)} (sin tarifa de envio). Stock reintegrado."
            : $"Devolucion rechazada ({r.Codigo}): {r.Mensaje} No se altero stock ni se genero reembolso.");
        if (r.Exito) await Notificar(OperacionUI.DevolucionProcesada);
        await RefrescarAsync();
    }
}