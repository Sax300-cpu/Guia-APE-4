using LogiMarket.Application.Consultas;
using LogiMarket.Application.Coordinacion;
using LogiMarket.Application.Demo;
using LogiMarket.Application.Eventos;
using LogiMarket.Application.Seguridad;
using LogiMarket.Application.Servicios;
using LogiMarket.WinForms.Infraestructura;
using LogiMarket.WinForms.Ui;

namespace LogiMarket.WinForms.Vistas;

/// <summary>Pantalla 12 - Auditoria, metricas y demostracion controlada (los interruptores solo afectan a colaboradores simulados).</summary>
internal sealed class VistaAuditoria : VistaBase
{
    private readonly ComboBox _categoria = Ui.Ui.Combo("Categoria de auditoria", 140, "(todas)", "Negocio", "Seguridad");
    private readonly DataGridView _grid = Ui.Ui.Grid("Bitacora de auditoria",
        new Col("Fecha", nameof(AuditoriaDto.Fecha), 80, "g"), new Col("Usuario", nameof(AuditoriaDto.Usuario), 50), new Col("Categoria", nameof(AuditoriaDto.Categoria), 50),
        new Col("Accion", nameof(AuditoriaDto.Accion), 90), new Col("Detalle", nameof(AuditoriaDto.Detalle), 160));
    private readonly Label _metricas = new() { AutoSize = true, Font = new Font("Segoe UI Semibold", 12f) };
    private readonly Label _outbox = new() { AutoSize = true, Font = Estilo.Negrita };
    private readonly ComboBox _proveedor = Ui.Ui.Combo("Proveedor de cobro simulado", 150, ProveedoresPago.Todos), _canal = Ui.Ui.Combo("Canal de notificacion", 120, CanalesNotificacion.Todos);
    private readonly CheckBox _fallaCobro = Ui.Ui.Casilla("Inducir fallo del cobro", "Inducir fallo del cobro"),
        _fallaNotificador = Ui.Ui.Casilla("Inducir fallo del notificador (el evento queda pendiente en el Outbox)", "Inducir fallo del notificador");
    private readonly Label _resultado = Ui.Ui.Mensaje("Resultado de la demostracion");
    private readonly TabControl _pestanas = new() { Dock = DockStyle.Fill, AccessibleName = "Secciones de auditoria" };
    private bool _cargando;

    public override string Titulo => "Auditoria y demostracion";
    public override TipoVista Tipo => TipoVista.Auditoria;
    public override Permiso PermisoAcceso => Permiso.VerAuditoria;

    public VistaAuditoria(Aplicacion app) : base(app)
    {
        _categoria.SelectedIndexChanged += async (_, _) => { if (!_cargando) await Seguro(() => RefrescarAsync()); };
        var bitacora = new TabPage("Bitacora de auditoria") { Padding = new Padding(8), BackColor = Estilo.Fondo };
        bitacora.Controls.Add(Ui.Ui.Pila((Ui.Ui.Fila(Ui.Ui.Campo("&Categoria", _categoria), Boton("&Actualizar", () => RefrescarAsync())), 0), (_grid, 1)));

        var indicadores = new TabPage("Metricas e indicadores") { Padding = new Padding(8), BackColor = Estilo.Fondo };
        indicadores.Controls.Add(Ui.Ui.Pila(
            (Ui.Ui.Etiqueta("Contadores de la sesion (Singleton compartido por todas las pantallas; la base de datos sigue siendo la fuente de verdad)", true), 0),
            (_metricas, 0), (Ui.Ui.Etiqueta("Estado del Outbox (eventos de negocio pendientes de entrega)", true), 0), (_outbox, 0),
            (Boton("&Procesar Outbox ahora", ProcesarOutboxAsync, Permiso.VerAuditoria), 0), (new Panel(), 1)));

        _pestanas.TabPages.Add(bitacora);
        _pestanas.TabPages.Add(indicadores);
        if (App.Tiene(Permiso.UsarDemo)) _pestanas.TabPages.Add(CrearDemostracion());

        _proveedor.SelectedIndexChanged += (_, _) => { if (!_cargando) App.Demo.ProveedorPago = (string)_proveedor.SelectedItem!; };
        _canal.SelectedIndexChanged += (_, _) => { if (!_cargando) App.Notificacion.Canal = (string)_canal.SelectedItem!; };
        _fallaCobro.CheckedChanged += (_, _) => { if (!_cargando) App.Demo.FallarCobro = _fallaCobro.Checked; };
        _fallaNotificador.CheckedChanged += (_, _) => { if (!_cargando) App.Demo.FallarNotificador = _fallaNotificador.Checked; };

        Controls.Add(Ui.Ui.Pila((Ui.Ui.Titulo(Titulo), 0), (_pestanas, 1), (_resultado, 0)));
    }

    internal void SeleccionarPestana(int indice) => _pestanas.SelectedIndex = Math.Min(indice, _pestanas.TabCount - 1); // gancho para la autoprueba

    private TabPage CrearDemostracion()
    {
        var aviso = new Label
        {
            Text = "MODO DEMOSTRACION: estos interruptores solo modifican a los colaboradores simulados (cobro y notificador). No alteran reglas de negocio, importes ni datos de produccion.",
            AutoSize = false,
            Height = 44,
            ForeColor = Estilo.Error,
            Font = Estilo.Negrita
        };
        var pagina = new TabPage("Demostracion controlada") { Padding = new Padding(8), BackColor = Estilo.Fondo };
        pagina.Controls.Add(Ui.Ui.Pila(
            (aviso, 0),
            (Ui.Ui.Fila(Ui.Ui.Campo("&Proveedor de cobro", _proveedor), Ui.Ui.Campo("Canal de &notificacion (Factory Method)", _canal)), 0),
            (Ui.Ui.Fila(_fallaCobro), 0), (Ui.Ui.Fila(_fallaNotificador), 0),
            (Ui.Ui.Fila(Boton("Procesar &Outbox ahora", ProcesarOutboxAsync, Permiso.UsarDemo)), 0),
            (new Panel(), 1)));
        return pagina;
    }

    public override async Task RefrescarAsync(CancellationToken ct = default)
    {
        _cargando = true;
        var categoria = _categoria.SelectedItem as string;
        var registros = await App.EjecutarAsync<ConsultasAuditoria, IReadOnlyList<AuditoriaDto>>(c => c.ListarAsync(300, ct));
        var resumen = await App.EjecutarAsync<ConsultasAuditoria, ResumenOutbox>(c => c.ResumenOutboxAsync(ct));
        Ui.Ui.Cargar(_grid, registros.Where(r => categoria is null or "(todas)" || r.Categoria == categoria).ToList());
        var m = App.Metricas;
        _metricas.Text = $"{m.PedidosPagados} pedidos pagados   |   {m.Despachos} despachos   |   {m.Devoluciones} devoluciones   |   {m.Rechazos} rechazos";
        _outbox.Text = $"Pendientes: {resumen.Pendientes}   |   Procesados: {resumen.Procesados}   |   Con error (reintento): {resumen.Fallidos}";
        _proveedor.SelectedItem = App.Demo.ProveedorPago;
        _canal.SelectedItem = App.Notificacion.Canal;
        _fallaCobro.Checked = App.Demo.FallarCobro;
        _fallaNotificador.Checked = App.Demo.FallarNotificador;
        _cargando = false;
    }

    private async Task ProcesarOutboxAsync()
    {
        var procesados = await App.EjecutarAsync<ProcesadorOutbox, int>(p => p.ProcesarPendientesAsync());
        Ui.Ui.MostrarResultado(_resultado, true, procesados > 0 ? $"{procesados} evento(s) entregado(s) a sus suscriptores (notificacion, auditoria y metricas)." : "No habia eventos entregables (los fallidos se reintentan; los procesados no se repiten).");
        await Notificar(OperacionUI.OutboxProcesado);
        await RefrescarAsync();
    }
}
