using LogiMarket.Application.Coordinacion;
using LogiMarket.Application.Eventos;
using LogiMarket.Application.Seguridad;
using LogiMarket.WinForms.Infraestructura;
using LogiMarket.WinForms.Ui;

namespace LogiMarket.WinForms;

/// <summary>Ventana principal: navegacion por rol hacia las pantallas; solo conoce a las vistas por su contrato comun.</summary>
internal sealed class FormPrincipal : Form
{
    private readonly Aplicacion _app;
    private readonly IReadOnlyList<VistaBase> _vistas;
    private readonly Dictionary<VistaBase, Button> _botones = new();
    private readonly Panel _contenido = new() { Dock = DockStyle.Fill, BackColor = Estilo.Fondo };
    private readonly Label _usuario = new() { AutoSize = true, Font = Estilo.Negrita, Anchor = AnchorStyles.Left | AnchorStyles.Top };
    private readonly ToolStripStatusLabel _estado = new() { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
    private readonly System.Windows.Forms.Timer _outbox = new() { Interval = 4000 };
    private bool _procesando;

    public bool CerroSesion { get; private set; }
    internal IReadOnlyList<VistaBase> Vistas => _vistas;

    public FormPrincipal(Aplicacion app, IEnumerable<VistaBase> vistas)
    {
        _app = app;
        Text = "LogiMarket 360";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(1320, 800);
        MinimumSize = new Size(1100, 700);
        Font = Estilo.Fuente;
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Estilo.Fondo;

        var todas = vistas.OrderBy(v => v.Tipo).ToList();
        var visibles = todas.Where(v => app.Tiene(v.PermisoAcceso)).ToList();
        foreach (var oculta in todas.Except(visibles)) oculta.Dispose(); // sin permiso: ni siquiera se ofrece (Application igual lo protege)
        _vistas = visibles;

        Controls.Add(_contenido);
        Controls.Add(CrearEncabezado());
        Controls.Add(CrearMenu());
        Controls.Add(CrearBarraEstado());

        _outbox.Tick += async (_, _) => await ProcesarOutboxAsync();
        Load += async (_, _) =>
        {
            ActualizarEstado();
            _outbox.Start();
            if (_vistas.Count > 0) await MostrarAsync(_vistas[0]);
        };
        FormClosed += (_, _) => _outbox.Stop();

    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _outbox.Stop();
            _outbox.Dispose();
            foreach (var v in _vistas) v.Dispose(); // desregistra cada pantalla del Mediator
        }
        base.Dispose(disposing);
    }

    private Control CrearMenu()
    {
        var menu = new FlowLayoutPanel
        {
            Dock = DockStyle.Left,
            Width = 230,
            BackColor = Estilo.Principal,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(10, 16, 10, 10),
            AccessibleName = "Menu de navegacion",
            AccessibleRole = AccessibleRole.MenuBar
        };
        menu.Controls.Add(new Label { Text = "LogiMarket 360", ForeColor = Color.White, Font = new Font("Segoe UI Semibold", 15f), AutoSize = true, Margin = new Padding(4, 0, 0, 14) });
        var numero = 1;
        foreach (var vista in _vistas)
        {
            var boton = new Button
            {
                Text = $"&{numero++}  {vista.Titulo}",
                Width = 208,
                Height = 40,
                TextAlign = ContentAlignment.MiddleLeft,
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                BackColor = Estilo.Principal,
                Margin = new Padding(0, 0, 0, 4),
                AccessibleName = vista.Titulo,
                Cursor = Cursors.Hand,
                UseMnemonic = true
            };
            boton.FlatAppearance.BorderSize = 0;
            boton.FlatAppearance.MouseOverBackColor = Color.FromArgb(0x2E, 0x6D, 0xA4);
            var capturada = vista;
            boton.Click += async (_, _) => await MostrarAsync(capturada);
            _botones[vista] = boton;
            menu.Controls.Add(boton);
        }
        return menu;
    }

    private Control CrearEncabezado()
    {
        var cerrar = new Button { Text = "&Cerrar sesion", AutoSize = true, Anchor = AnchorStyles.Right | AnchorStyles.Top, AccessibleName = "Cerrar sesion", Padding = new Padding(8, 2, 8, 2) };
        cerrar.Click += async (_, _) => await CerrarSesionAsync();
        var panel = new Panel { Dock = DockStyle.Top, Height = 54, BackColor = Color.White, Padding = new Padding(16, 0, 16, 0) };
        _usuario.Text = $"Usuario: {_app.Sesion.NombreUsuario}   |   Rol: {_app.Sesion.Rol}";
        _usuario.Location = new Point(16, 17);
        panel.Controls.Add(_usuario);
        panel.Controls.Add(cerrar);
        panel.Resize += (_, _) => cerrar.Location = new Point(panel.Width - cerrar.Width - 16, 12);
        cerrar.Location = new Point(1000, 12);
        return panel;
    }

    private Control CrearBarraEstado()
    {
        var barra = new StatusStrip { Dock = DockStyle.Bottom, SizingGrip = false };
        barra.Items.Add(_estado);
        return barra;
    }

    internal Task MostrarAsync(TipoVista tipo) => MostrarAsync(_vistas.First(v => v.Tipo == tipo));

    private async Task MostrarAsync(VistaBase vista)
    {
        _contenido.Controls.Clear();
        _contenido.Controls.Add(vista);
        foreach (var (v, b) in _botones)
            b.BackColor = ReferenceEquals(v, vista) ? Color.FromArgb(0x2E, 0x6D, 0xA4) : Estilo.Principal;
        await vista.Seguro(() => vista.RefrescarAsync());
        ActualizarEstado();
    }

    /// <summary>El Outbox se procesa despues de confirmar la transaccion de negocio, no dentro de ella.</summary>
    private async Task ProcesarOutboxAsync()
    {
        if (_procesando) return;
        _procesando = true;
        try
        {
            var procesados = await _app.EjecutarAsync<ProcesadorOutbox, int>(p => p.ProcesarPendientesAsync());
            if (procesados > 0) await _app.Mediador.NotificarAsync(TipoVista.Metricas, OperacionUI.OutboxProcesado);
            ActualizarEstado();
        }
        catch (Exception ex)
        {
            _estado.Text = "Outbox: " + ex.Message;
        }
        finally
        {
            _procesando = false;
        }
    }

    private void ActualizarEstado()
    {
        var m = _app.Metricas;
        _estado.Text = $"Sesion: {m.PedidosPagados} pagados  |  {m.Despachos} despachos  |  {m.Devoluciones} devoluciones  |  {m.Rechazos} rechazos";
    }

    private async Task CerrarSesionAsync()
    {
        if (!Ui.Ui.Confirmar(this, "Se cerrara la sesion. Los borradores y pedidos ya estan guardados. Continuar?")) return;
        await _app.EjecutarAsync<ServicioAutenticacion>(s => s.CerrarSesionAsync());
        CerroSesion = true;
        Close();
    }
}
