using LogiMarket.Application.Comandos;
using LogiMarket.Application.Comun;
using LogiMarket.Application.Consultas;
using LogiMarket.Application.Coordinacion;
using LogiMarket.Application.Seguridad;
using LogiMarket.Application.Servicios;
using LogiMarket.WinForms.Infraestructura;
using LogiMarket.WinForms.Ui;
using Microsoft.Extensions.DependencyInjection;

namespace LogiMarket.WinForms.Vistas;

/// <summary>Pantalla 08 - Despacho y entrega: transicion de estados autorizada (solo en secuencia permitida).</summary>
internal sealed class VistaDespacho : VistaBase
{
    private readonly DataGridView _grid = Ui.Ui.Grid("Pedidos por despachar o entregar",
        new Col("N.", nameof(PedidoResumenDto.Id), 30), new Col("Cliente", nameof(PedidoResumenDto.Cliente), 90), new Col("Estado", nameof(PedidoResumenDto.Estado), 60),
        new Col("Total (USD)", nameof(PedidoResumenDto.Total), 60, "0.00", true), new Col("Fecha", nameof(PedidoResumenDto.Fecha), 90, "g"));
    private readonly Label _resultado = Ui.Ui.Mensaje("Resultado del despacho");

    public override string Titulo => "Despacho y entrega";
    public override TipoVista Tipo => TipoVista.Despacho;
    public override Permiso PermisoAcceso => Permiso.DespacharPedido;

    public VistaDespacho(Aplicacion app) : base(app)
    {
        Controls.Add(Ui.Ui.Pila(
            (Ui.Ui.Titulo(Titulo), 0),
            (Ui.Ui.Etiqueta("Solo se despachan pedidos PAGADOS; solo se entregan pedidos DESPACHADOS. La guia se genera segun la familia de entrega."), 0),
            (_grid, 1),
            (Ui.Ui.Fila(Boton("&Despachar pedido", () => EjecutarAsync(true), Permiso.DespacharPedido),
                Boton("Marcar &entregado", () => EjecutarAsync(false), Permiso.EntregarPedido)), 0),
            (_resultado, 0)));
    }

    public override async Task RefrescarAsync(CancellationToken ct = default)
    {
        var pedidos = await App.EjecutarAsync<ConsultasPedidos, IReadOnlyList<PedidoResumenDto>>(c => c.ListarAsync(ct));
        Ui.Ui.Cargar(_grid, pedidos.Where(p => p.Estado is EstadosPedidoTexto.Pagado or EstadosPedidoTexto.Despachado).ToList(), p => p.Id);
    }

    private async Task EjecutarAsync(bool despachar)
    {
        if (Ui.Ui.Seleccion<PedidoResumenDto>(_grid) is not { } p) { Ui.Ui.MostrarResultado(_resultado, false, "Selecciona un pedido."); return; }
        var verbo = despachar ? "despachar" : "marcar como entregado";
        if (!Ui.Ui.Confirmar(FindForm(), $"Confirmas {verbo} el pedido #{p.Id}? No se puede retroceder el estado.")) return;

        var r = await App.EjecutarAsync(async sp =>
        {
            var servicio = sp.GetRequiredService<ServicioDespacho>();
            var invocador = sp.GetRequiredService<InvocadorComandos>();
            return despachar
                ? await invocador.EjecutarAsync(new DespacharPedidoCommand(servicio, p.Id))
                : await invocador.EjecutarAsync(new EntregarPedidoCommand(servicio, p.Id));
        });
        Ui.Ui.MostrarResultado(_resultado, r.Exito, r.Exito ? $"Pedido #{p.Id}: {r.Mensaje}" : $"Rechazado ({r.Codigo}): {r.Mensaje}");
        if (r.Exito) await Notificar(despachar ? OperacionUI.PedidoDespachado : OperacionUI.PedidoEntregado);
        await RefrescarAsync();
    }
}
