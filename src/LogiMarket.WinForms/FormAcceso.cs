using LogiMarket.Application.Seguridad;
using LogiMarket.WinForms.Infraestructura;
using LogiMarket.WinForms.Ui;

namespace LogiMarket.WinForms;

/// <summary>Pantalla 01 - Acceso: identificacion local con usuario y contrasena (la contrasena solo se compara contra su hash).</summary>
internal sealed class FormAcceso : Form
{
    private readonly Aplicacion _app;
    private readonly TextBox _usuario = Ui.Ui.Caja("Usuario", 280);
    private readonly TextBox _clave = Ui.Ui.Caja("Contrasena", 280, clave: true);
    private readonly Label _mensaje = Ui.Ui.Mensaje("Mensaje de acceso");
    private readonly ErrorProvider _errores = new() { BlinkStyle = ErrorBlinkStyle.NeverBlink };
    private readonly Button _ingresar = new() { Text = "&Ingresar", AutoSize = true, Padding = new Padding(16, 4, 16, 4), AccessibleName = "Ingresar" };
    private readonly Button _salir = new() { Text = "&Salir", AutoSize = true, Padding = new Padding(16, 4, 16, 4), AccessibleName = "Salir" };

    public FormAcceso(Aplicacion app)
    {
        _app = app;
        Text = "LogiMarket 360 - Acceso";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(470, 400);
        BackColor = Estilo.Fondo;
        Font = Estilo.Fuente;
        AutoScaleMode = AutoScaleMode.Dpi;
        AcceptButton = _ingresar;
        CancelButton = _salir;

        var titulo = new Label { Text = "LogiMarket 360", Font = new Font("Segoe UI Semibold", 22f), ForeColor = Estilo.Principal, AutoSize = true };
        var subtitulo = new Label { Text = "Acceso local al sistema de ventas y logistica", AutoSize = true, ForeColor = Color.DimGray };
        var ayuda = new Label
        {
            Text = "Cuentas de demostracion: admin, ventas, bodega y consulta.\r\nLas contrasenas de prueba estan en el README.",
            AutoSize = true, ForeColor = Color.DimGray, Margin = new Padding(0, 14, 0, 0)
        };
        var cuerpo = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, Location = new Point(50, 30) };
        cuerpo.Controls.AddRange(new Control[]
        {
            titulo, subtitulo,
            new Label { Height = 12 },
            Ui.Ui.Campo("&Usuario", _usuario), Ui.Ui.Campo("&Contrasena", _clave),
            Ui.Ui.Fila(_ingresar, _salir), _mensaje, ayuda
        });
        _mensaje.Width = 370;
        Controls.Add(cuerpo);

        _ingresar.Click += async (_, _) => await IngresarAsync();
        Shown += (_, _) => _usuario.Focus();
    }

    internal Task IntentarAsync(string usuario, string clave)
    {
        _usuario.Text = usuario;
        _clave.Text = clave;
        return IngresarAsync();
    }

    private async Task IngresarAsync()
    {
        var okUsuario = Ui.Ui.Requerido(_errores, _usuario, "Escribe tu usuario.");
        var okClave = Ui.Ui.Requerido(_errores, _clave, "Escribe tu contrasena.");
        if (!okUsuario || !okClave) return;

        _ingresar.Enabled = false;
        try
        {
            var valido = await _app.EjecutarAsync<ServicioAutenticacion, bool>(s => s.IniciarSesionAsync(_usuario.Text, _clave.Text));
            if (valido)
            {
                DialogResult = DialogResult.OK;
                return;
            }
            _clave.Clear();
            Ui.Ui.MostrarResultado(_mensaje, false, "Usuario o contrasena incorrectos.");
            _clave.Focus();
        }
        finally
        {
            _ingresar.Enabled = true;
        }
    }
}