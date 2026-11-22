using LogiMarket.Application.Comun;
using LogiMarket.Application.Consultas;
using LogiMarket.Application.Coordinacion;
using LogiMarket.Application.Seguridad;
using LogiMarket.Domain.Common;
using LogiMarket.WinForms.Infraestructura;

namespace LogiMarket.WinForms.Ui;

/// <summary>Base de las pantallas: se registra en el Mediator y nunca conoce a otras pantallas.</summary>
internal abstract class VistaBase : UserControl, IVistaActualizable
{
    private readonly ToolTip _ayuda = new();

    protected Aplicacion App { get; }
    protected ErrorProvider Errores { get; } = new() { BlinkStyle = ErrorBlinkStyle.NeverBlink };

    public abstract string Titulo { get; }
    public abstract TipoVista Tipo { get; }
    /// <summary>Permiso para ver la pantalla; la autorizacion real de cada accion vive en Application.</summary>
    public abstract Permiso PermisoAcceso { get; }
    public abstract Task RefrescarAsync(CancellationToken ct = default);

    protected VistaBase(Aplicacion app)
    {
        App = app;
        Dock = DockStyle.Fill;
        Font = Estilo.Fuente;
        BackColor = Estilo.Fondo;
        Padding = new Padding(16);
        AutoScaleMode = AutoScaleMode.Dpi;
        app.Mediador.Registrar(this);
    }

    protected Button Boton(string texto, Func<Task> accion, Permiso? permiso = null, string? accesible = null)
    {
        var boton = new Button { Text = texto, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Margin = new Padding(0, 3, 8, 3),
            AccessibleName = accesible ?? texto.Replace("&", ""), Padding = new Padding(8, 2, 8, 2) };
        boton.Click += async (_, _) => await Seguro(accion);
        if (permiso is { } p && !App.Tiene(p))
        {
            boton.Enabled = false;
            _ayuda.SetToolTip(boton, "Tu rol no tiene permiso para esta accion.");
        }
        return boton;
    }

    protected Task Notificar(OperacionUI operacion) => App.Mediador.NotificarAsync(Tipo, operacion);

    public async Task Seguro(Func<Task> accion)
    {
        var dueno = FindForm();
        try
        {
            UseWaitCursor = true;
            await accion();
        }
        catch (AccesoDenegadoException ex)
        {
            Ui.Aviso(dueno, ex.Message, MessageBoxIcon.Warning);
        }
        catch (ErrorSintaxisConsultaException ex)
        {
            Ui.Aviso(dueno, "Consulta no valida: " + ex.Message);
        }
        catch (DomainException ex)
        {
            Ui.Aviso(dueno, ex.Message);
        }
        catch (Exception ex)
        {
            Ui.Aviso(dueno, "Ocurrio un error inesperado: " + ex.Message, MessageBoxIcon.Error);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            App.Mediador.Desregistrar(this);
            _ayuda.Dispose();
            Errores.Dispose();
        }
        base.Dispose(disposing);
    }
}