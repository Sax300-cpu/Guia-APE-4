using LogiMarket.Application.Consultas;
using LogiMarket.Application.Coordinacion;
using LogiMarket.Application.Seguridad;
using LogiMarket.Application.Servicios;
using LogiMarket.Domain.Clientes;
using LogiMarket.WinForms.Infraestructura;
using LogiMarket.WinForms.Ui;

namespace LogiMarket.WinForms.Vistas;

/// <summary>Pantalla 04 - Clientes: busqueda, registro, categoria y edicion.</summary>
internal sealed class VistaClientes : VistaBase
{
    private readonly TextBox _buscar = Ui.Ui.Caja("Buscar cliente", 240);
    private readonly DataGridView _grid = Ui.Ui.Grid("Clientes",
        new Col("N.", nameof(ClienteDto.Id), 30), new Col("Nombre", nameof(ClienteDto.Nombre), 120), new Col("Correo", nameof(ClienteDto.Email), 120),
        new Col("Categoria", nameof(ClienteDto.Categoria), 60), new Col("Activo", nameof(ClienteDto.Activo), 40));
    private readonly TextBox _nombre = Ui.Ui.Caja("Nombre del cliente", 220), _email = Ui.Ui.Caja("Correo del cliente", 240);
    private readonly ComboBox _categoria = Ui.Ui.Combo("Categoria del cliente", 130, CategoriaCliente.Normal, CategoriaCliente.Vip);
    private readonly CheckBox _activo = Ui.Ui.Casilla("Cliente activo", "Cliente activo");
    private readonly Label _resultado = Ui.Ui.Mensaje("Resultado de clientes");
    private int _idSeleccionado;

    public override string Titulo => "Clientes";
    public override TipoVista Tipo => TipoVista.Clientes;
    public override Permiso PermisoAcceso => Permiso.Consultar;

    public VistaClientes(Aplicacion app) : base(app)
    {
        _activo.Checked = true;
        _buscar.KeyDown += async (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await Seguro(() => RefrescarAsync()); } };
        var formulario = Ui.Ui.Grupo("Datos del cliente", Ui.Ui.Fila(
            Ui.Ui.Campo("&Nombre", _nombre), Ui.Ui.Campo("C&orreo", _email), Ui.Ui.Campo("C&ategoria", _categoria), _activo,
            Boton("&Registrar nuevo", RegistrarAsync, Permiso.GestionarClientes),
            Boton("&Guardar cambios", GuardarAsync, Permiso.GestionarClientes),
            Boton("&Limpiar", () => { Limpiar(); return Task.CompletedTask; })), 160);
        _grid.SelectionChanged += (_, _) => CargarSeleccion();
        Controls.Add(Ui.Ui.Pila(
            (Ui.Ui.Titulo(Titulo), 0), (Ui.Ui.Fila(Ui.Ui.Campo("&Buscar por nombre", _buscar), Boton("Bu&scar", () => RefrescarAsync())), 0),
            (_grid, 1), (formulario, 0), (_resultado, 0)));
    }

    private void CargarSeleccion()
    {
        if (Ui.Ui.Seleccion<ClienteDto>(_grid) is not { } c) return;
        _idSeleccionado = c.Id;
        _nombre.Text = c.Nombre;
        _email.Text = c.Email;
        _categoria.SelectedItem = Enum.Parse<CategoriaCliente>(c.Categoria);
        _activo.Checked = c.Activo;
    }

    private void Limpiar()
    {
        _idSeleccionado = 0;
        _nombre.Clear();
        _email.Clear();
        _categoria.SelectedIndex = 0;
        _activo.Checked = true;
        Errores.Clear();
        _nombre.Focus();
    }

    public override async Task RefrescarAsync(CancellationToken ct = default) =>
        Ui.Ui.Cargar(_grid, await App.EjecutarAsync<ServicioClientes, IReadOnlyList<ClienteDto>>(s => s.ListarAsync(_buscar.Text, ct)), c => c.Id);

    private async Task RegistrarAsync()
    {
        if (!Ui.Ui.Requerido(Errores, _nombre, "El nombre es obligatorio.")) return;
        var categoria = (CategoriaCliente)_categoria.SelectedItem!;
        await App.EjecutarAsync<ServicioClientes, int>(s => s.RegistrarAsync(_nombre.Text, _email.Text, categoria));
        Ui.Ui.MostrarResultado(_resultado, true, $"Cliente {_nombre.Text.Trim()} registrado.");
        Limpiar();
        await Notificar(OperacionUI.ClienteCambiado);
        await RefrescarAsync();
    }

    private async Task GuardarAsync()
    {
        if (_idSeleccionado == 0) { Errores.SetError(_nombre, "Selecciona un cliente de la tabla para editarlo."); return; }
        if (!Ui.Ui.Requerido(Errores, _nombre, "El nombre es obligatorio.")) return;
        var categoria = (CategoriaCliente)_categoria.SelectedItem!;
        await App.EjecutarAsync<ServicioClientes>(s => s.ActualizarAsync(_idSeleccionado, _nombre.Text, _email.Text, categoria, _activo.Checked));
        Ui.Ui.MostrarResultado(_resultado, true, $"Cliente {_nombre.Text.Trim()} actualizado.");
        await Notificar(OperacionUI.ClienteCambiado);
        await RefrescarAsync();
    }
}