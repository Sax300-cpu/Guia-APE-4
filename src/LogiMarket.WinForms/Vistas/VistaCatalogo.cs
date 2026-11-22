using LogiMarket.Application.Consultas;
using LogiMarket.Application.Coordinacion;
using LogiMarket.Application.Seguridad;
using LogiMarket.Application.Servicios;
using LogiMarket.Domain.Common;
using LogiMarket.WinForms.Infraestructura;
using LogiMarket.WinForms.Ui;

namespace LogiMarket.WinForms.Vistas;

/// <summary>Pantalla 03 - Catalogo y kits: gestion de SKU, precios, existencias y kits anidados.</summary>
internal sealed class VistaCatalogo : VistaBase
{
    private readonly TextBox _buscar = Ui.Ui.Caja("Buscar producto", 240);
    private readonly DataGridView _grid = Ui.Ui.Grid("Catalogo",
        new Col("SKU", nameof(CatalogoFilaDto.Sku), 70), new Col("Nombre", nameof(CatalogoFilaDto.Nombre), 120),
        new Col("Tipo", nameof(CatalogoFilaDto.Tipo), 50), new Col("Precio (USD)", nameof(CatalogoFilaDto.Precio), 60, "0.00", true),
        new Col("Stock", nameof(CatalogoFilaDto.Stock), 40, null, true), new Col("Peso (kg)", nameof(CatalogoFilaDto.PesoKg), 50, "0.00", true),
        new Col("Composicion", nameof(CatalogoFilaDto.Composicion), 140));
    private readonly TextBox _sku = Ui.Ui.Caja("SKU", 110), _nombre = Ui.Ui.Caja("Nombre", 200);
    private readonly NumericUpDown _precio = Ui.Ui.Decimal("Precio", 0.01m, 100000m, 2, 1m), _peso = Ui.Ui.Decimal("Peso en kg", 0m, 1000m, 3, 0.1m),
        _stock = Ui.Ui.Entero("Stock inicial", 0, 1000000, 0);
    private readonly TextBox _kitSku = Ui.Ui.Caja("SKU del kit", 130), _kitNombre = Ui.Ui.Caja("Nombre del kit", 200), _componentes = Ui.Ui.Caja("Componentes del kit", 320);
    private readonly Label _resultado = Ui.Ui.Mensaje("Resultado del catalogo");

    public override string Titulo => "Catalogo y kits";
    public override TipoVista Tipo => TipoVista.Catalogo;
    public override Permiso PermisoAcceso => Permiso.Consultar;

    public VistaCatalogo(Aplicacion app) : base(app)
    {
        var buscar = Boton("&Buscar", () => RefrescarAsync());
        _buscar.KeyDown += async (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await Seguro(() => RefrescarAsync()); } };

        var producto = Ui.Ui.Grupo("Producto (alta y edicion)", Ui.Ui.Fila(
            Ui.Ui.Campo("&SKU", _sku), Ui.Ui.Campo("&Nombre", _nombre), Ui.Ui.Campo("&Precio (USD)", _precio),
            Ui.Ui.Campo("Pe&so (kg)", _peso), Ui.Ui.Campo("S&tock inicial", _stock),
            Boton("&Crear producto", CrearProductoAsync, Permiso.GestionarCatalogo),
            Boton("&Guardar cambios", GuardarAsync, Permiso.GestionarCatalogo),
            Boton("&Eliminar", EliminarAsync, Permiso.GestionarCatalogo)), 160);
        var kit = Ui.Ui.Grupo("Kit (puede contener productos y otros kits, sin ciclos)", Ui.Ui.Fila(
            Ui.Ui.Campo("SKU del &kit", _kitSku), Ui.Ui.Campo("Nombre del k&it", _kitNombre),
            Ui.Ui.Campo("Componentes (ej. P01:1, KIT_OFICINA:1)", _componentes),
            Boton("Crear k&it", CrearKitAsync, Permiso.GestionarCatalogo)), 104);

        _grid.SelectionChanged += (_, _) => CargarSeleccionEnFormulario();
        Controls.Add(Ui.Ui.Pila(
            (Ui.Ui.Titulo(Titulo), 0), (Ui.Ui.Fila(Ui.Ui.Campo("Buscar por SKU o nombre", _buscar), buscar), 0),
            (_grid, 1), (producto, 0), (kit, 0), (_resultado, 0)));
    }

    private void CargarSeleccionEnFormulario()
    {
        if (Ui.Ui.Seleccion<CatalogoFilaDto>(_grid) is not { } fila) return;
        _sku.Text = fila.Sku;
        _nombre.Text = fila.Nombre;
        if (fila.Tipo == "Producto")
        {
            _precio.Value = Math.Clamp(fila.Precio, _precio.Minimum, _precio.Maximum);
            _peso.Value = Math.Clamp(fila.PesoKg, _peso.Minimum, _peso.Maximum);
        }
    }

    public override async Task RefrescarAsync(CancellationToken ct = default) =>
        Ui.Ui.Cargar(_grid, await App.EjecutarAsync<ConsultasCatalogo, IReadOnlyList<CatalogoFilaDto>>(c => c.ListarAsync(_buscar.Text, ct)), f => f.Sku);

    private async Task CrearProductoAsync()
    {
        if (!(Ui.Ui.Requerido(Errores, _sku, "El SKU es obligatorio.") & Ui.Ui.Requerido(Errores, _nombre, "El nombre es obligatorio."))) return;
        await App.EjecutarAsync<ServicioCatalogo>(s => s.CrearProductoAsync(_sku.Text, _nombre.Text, _precio.Value, _peso.Value, (int)_stock.Value));
        Ui.Ui.MostrarResultado(_resultado, true, $"Producto {_sku.Text.Trim().ToUpperInvariant()} creado.");
        await Notificar(OperacionUI.CatalogoCambiado);
        await RefrescarAsync();
    }

    private async Task GuardarAsync()
    {
        if (!Ui.Ui.Requerido(Errores, _sku, "Selecciona un elemento del catalogo o escribe su SKU.")) return;
        if (!Ui.Ui.Requerido(Errores, _nombre, "El nombre es obligatorio.")) return;
        var esKit = Ui.Ui.Seleccion<CatalogoFilaDto>(_grid) is { Tipo: "Kit" };
        await App.EjecutarAsync<ServicioCatalogo>(s => s.ActualizarAsync(_sku.Text, _nombre.Text, esKit ? null : _precio.Value));
        Ui.Ui.MostrarResultado(_resultado, true, $"{_sku.Text.Trim().ToUpperInvariant()} actualizado" + (esKit ? " (el precio de un kit se calcula de sus componentes)." : "."));
        await Notificar(OperacionUI.CatalogoCambiado);
        await RefrescarAsync();
    }

    private async Task EliminarAsync()
    {
        if (!Ui.Ui.Requerido(Errores, _sku, "Selecciona el elemento a eliminar.")) return;
        if (!Ui.Ui.Confirmar(FindForm(), $"Eliminar {_sku.Text.Trim().ToUpperInvariant()}? Solo es posible si no tiene historial.")) return;
        await App.EjecutarAsync<ServicioCatalogo>(s => s.EliminarAsync(_sku.Text));
        Ui.Ui.MostrarResultado(_resultado, true, "Elemento eliminado.");
        await Notificar(OperacionUI.CatalogoCambiado);
        await RefrescarAsync();
    }

    private async Task CrearKitAsync()
    {
        if (!(Ui.Ui.Requerido(Errores, _kitSku, "El SKU del kit es obligatorio.") & Ui.Ui.Requerido(Errores, _kitNombre, "El nombre del kit es obligatorio.")
              & Ui.Ui.Requerido(Errores, _componentes, "Indica los componentes, por ejemplo P01:1, P02:2."))) return;
        var componentes = Interpretar(_componentes.Text);
        await App.EjecutarAsync<ServicioCatalogo>(s => s.CrearKitAsync(_kitSku.Text, _kitNombre.Text, componentes));
        Ui.Ui.MostrarResultado(_resultado, true, $"Kit {_kitSku.Text.Trim().ToUpperInvariant()} creado.");
        await Notificar(OperacionUI.CatalogoCambiado);
        await RefrescarAsync();
    }

    private static List<(string Sku, int Cantidad)> Interpretar(string texto)
    {
        var lista = new List<(string, int)>();
        foreach (var parte in texto.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var trozos = parte.Split(':', StringSplitOptions.TrimEntries);
            var cantidad = 1;
            if (trozos.Length > 2 || trozos[0].Length == 0 || (trozos.Length == 2 && !int.TryParse(trozos[1], out cantidad)))
                throw new DomainException($"Componente no valido: '{parte}'. Usa el formato SKU:cantidad.");
            lista.Add((trozos[0], cantidad));
        }
        return lista;
    }
}