using LogiMarket.Domain.Common;

namespace LogiMarket.WinForms.Ui;

internal sealed record Col(string Encabezado, string Propiedad, int Peso = 100, string? Formato = null, bool Derecha = false);

internal static class Estilo
{
    public static readonly Font Fuente = new("Segoe UI", 9.5f);
    public static readonly Font Negrita = new("Segoe UI", 9.5f, FontStyle.Bold);
    public static readonly Font Titulo = new("Segoe UI Semibold", 15f);
    public static readonly Font Cifra = new("Segoe UI Semibold", 20f);
    public static readonly Color Principal = Color.FromArgb(0x1F, 0x4E, 0x79);
    public static readonly Color Fondo = Color.FromArgb(0xF4, 0xF6, 0xF8);
    public static readonly Color Ok = Color.FromArgb(0x1E, 0x7B, 0x34);
    public static readonly Color Error = Color.FromArgb(0xB0, 0x20, 0x20);
    public static readonly Color Alerta = Color.FromArgb(0xFF, 0xE3, 0xE3);
}

internal static class Ui
{
    public static Label Etiqueta(string texto, bool negrita = false) => new()
    {
        Text = texto, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 6, 0),
        Font = negrita ? Estilo.Negrita : Estilo.Fuente, UseMnemonic = true
    };

    public static Label Titulo(string texto) => new()
    {
        Text = texto, AutoSize = true, Font = Estilo.Titulo, ForeColor = Estilo.Principal, Margin = new Padding(0, 0, 0, 4)
    };

    public static Label Mensaje(string accesible) => new()
    {
        AutoSize = false, Height = 38, Font = Estilo.Negrita, Name = accesible, AccessibleName = accesible, AccessibleRole = AccessibleRole.StaticText
    };

    public static TextBox Caja(string accesible, int ancho = 180, bool clave = false) => new()
    {
        Width = ancho, AccessibleName = accesible, Name = accesible, UseSystemPasswordChar = clave, Margin = new Padding(0, 3, 12, 3)
    };

    public static ComboBox Combo(string accesible, int ancho = 180, params object[] items)
    {
        var c = new ComboBox { Width = ancho, DropDownStyle = ComboBoxStyle.DropDownList, AccessibleName = accesible, Name = accesible, Margin = new Padding(0, 3, 12, 3) };
        c.Items.AddRange(items);
        if (items.Length > 0) c.SelectedIndex = 0;
        return c;
    }

    public static NumericUpDown Entero(string accesible, int minimo, int maximo, int valor = 1, int ancho = 80) => new()
    {
        Minimum = minimo, Maximum = maximo, Value = valor, Width = ancho, AccessibleName = accesible, Name = accesible, Margin = new Padding(0, 3, 12, 3)
    };

    public static NumericUpDown Decimal(string accesible, decimal minimo, decimal maximo, int decimales, decimal valor, int ancho = 100) => new()
    {
        Minimum = minimo, Maximum = maximo, DecimalPlaces = decimales, Value = valor, Width = ancho, AccessibleName = accesible,
        Name = accesible, Margin = new Padding(0, 3, 12, 3), ThousandsSeparator = false
    };

    public static CheckBox Casilla(string texto, string accesible) => new()
    {
        Text = texto, AutoSize = true, AccessibleName = accesible, Name = accesible, Margin = new Padding(0, 6, 12, 3)
    };

    public static FlowLayoutPanel Fila(params Control[] controles)
    {
        var f = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, Margin = new Padding(0) };
        f.Controls.AddRange(controles);
        return f;
    }

    /// <summary>Etiqueta con mnemonico seguida de su control: Alt+letra enfoca el campo.</summary>
    public static FlowLayoutPanel Campo(string etiqueta, Control control)
    {
        var f = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = new Padding(0, 0, 8, 4) };
        f.Controls.Add(new Label { Text = etiqueta, AutoSize = true, UseMnemonic = true, Margin = new Padding(0, 0, 0, 0), Font = Estilo.Fuente });
        f.Controls.Add(control);
        return f;
    }

    /// <summary>Agrupa controles. Con alto fijo (>0) los controles pueden acomodarse en varias filas sin recortarse.</summary>
    public static GroupBox Grupo(string titulo, Control contenido, int alto = 0)
    {
        var g = new GroupBox { Text = titulo, Padding = new Padding(10, 6, 10, 8), Font = Estilo.Negrita };
        if (alto > 0) g.Height = alto;
        else { g.AutoSize = true; g.AutoSizeMode = AutoSizeMode.GrowAndShrink; }
        contenido.Font = Estilo.Fuente;
        contenido.Dock = DockStyle.Fill;
        g.Controls.Add(contenido);
        return g;
    }

    /// <summary>Apila controles verticalmente: peso 0 = alto automatico; peso &gt; 0 = reparte el espacio sobrante.</summary>
    public static TableLayoutPanel Pila(params (Control Control, float Peso)[] filas)
    {
        var t = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = filas.Length };
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        foreach (var (control, peso) in filas)
        {
            t.RowStyles.Add(peso == 0 ? new RowStyle(SizeType.AutoSize) : new RowStyle(SizeType.Percent, peso));
            control.Dock = DockStyle.Fill;
            control.Margin = new Padding(0, 0, 0, 8);
            t.Controls.Add(control);
        }
        return t;
    }

    public static DataGridView Grid(string accesible, params Col[] columnas)
    {
        var g = new DataGridView
        {
            Name = accesible, AccessibleName = accesible, Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false,
            AllowUserToDeleteRows = false, AllowUserToResizeRows = false, MultiSelect = false, RowHeadersVisible = false,
            AutoGenerateColumns = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, BackgroundColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, TabStop = true,
            EnableHeadersVisualStyles = true, ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
            MinimumSize = new Size(200, 110)
        };
        g.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(0xF7, 0xF9, 0xFB);
        g.CellFormatting += (_, e) =>
        {
            if (e.Value is bool valor) { e.Value = valor ? "Si" : "No"; e.FormattingApplied = true; }
        };
        foreach (var c in columnas)
        {
            var col = new DataGridViewTextBoxColumn { HeaderText = c.Encabezado, DataPropertyName = c.Propiedad, FillWeight = c.Peso, SortMode = DataGridViewColumnSortMode.Automatic };
            if (c.Formato is not null) col.DefaultCellStyle.Format = c.Formato;
            if (c.Derecha) { col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight; col.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleRight; }
            g.Columns.Add(col);
        }
        return g;
    }

    /// <summary>Carga datos conservando la fila seleccionada (seleccion persistente) cuando existe una clave.</summary>
    public static void Cargar<T>(DataGridView grid, IReadOnlyList<T> datos, Func<T, object>? clave = null) where T : class
    {
        object? seleccionada = clave is not null && grid.CurrentRow?.DataBoundItem is T actual ? clave(actual) : null;
        grid.DataSource = datos.ToList();
        if (seleccionada is null) return;
        foreach (DataGridViewRow fila in grid.Rows)
            if (fila.DataBoundItem is T item && Equals(clave!(item), seleccionada))
            {
                fila.Selected = true;
                grid.CurrentCell = fila.Cells[0];
                break;
            }
    }

    public static T? Seleccion<T>(DataGridView grid) where T : class => grid.CurrentRow?.DataBoundItem as T;

    /// <summary>Solo para la autoprueba: responde los cuadros de dialogo sin bloquear la ejecucion.</summary>
    public static Func<IWin32Window?, string, bool>? ConfirmadorAutomatico { get; set; }
    public static Action<string>? AvisoAutomatico { get; set; }

    public static bool Confirmar(IWin32Window? dueno, string mensaje) =>
        ConfirmadorAutomatico is { } auto ? auto(dueno, mensaje) : MessageBox.Show(dueno, mensaje, "Confirmar accion", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes;

    public static void Aviso(IWin32Window? dueno, string mensaje, MessageBoxIcon icono = MessageBoxIcon.Information)
    {
        if (AvisoAutomatico is { } auto) { auto(mensaje); return; }
        MessageBox.Show(dueno, mensaje, "LogiMarket 360", MessageBoxButtons.OK, icono);
    }

    public static string Usd(decimal valor) => Dinero.Formato(valor);

    /// <summary>Validacion cercana al campo: el mensaje aparece junto al control, no solo en un cuadro.</summary>
    public static bool Requerido(ErrorProvider errores, Control control, string mensaje)
    {
        var vacio = control is TextBox t && string.IsNullOrWhiteSpace(t.Text) || control is ComboBox { SelectedItem: null };
        errores.SetError(control, vacio ? mensaje : string.Empty);
        return !vacio;
    }

    public static void MostrarResultado(Label etiqueta, bool exito, string texto)
    {
        etiqueta.ForeColor = exito ? Estilo.Ok : Estilo.Error;
        etiqueta.Text = (exito ? "\u2714 " : "\u2716 ") + texto;
    }
}