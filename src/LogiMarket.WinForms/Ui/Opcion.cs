namespace LogiMarket.WinForms.Ui;

/// <summary>Elemento de lista con texto visible y valor asociado.</summary>
internal sealed record Opcion<T>(string Texto, T Valor)
{
    public override string ToString() => Texto;
}

internal static class ComboExtensiones
{
    /// <summary>Recarga el combo conservando la opcion seleccionada (por valor) si sigue existiendo.</summary>
    public static void Cargar<T>(this ComboBox combo, IEnumerable<Opcion<T>> opciones, bool conVacio = false)
    {
        var previo = combo.SelectedItem is Opcion<T> o ? o.Valor : default;
        combo.BeginUpdate();
        combo.Items.Clear();
        if (conVacio) combo.Items.Add(new Opcion<T?>("(ninguno)", default));
        combo.Items.AddRange(opciones.Cast<object>().ToArray());
        var indice = previo is null ? -1 : combo.Items.OfType<Opcion<T>>().ToList().FindIndex(x => Equals(x.Valor, previo));
        combo.SelectedIndex = indice >= 0 ? combo.Items.IndexOf(combo.Items.OfType<Opcion<T>>().ElementAt(indice)) : combo.Items.Count > 0 ? 0 : -1;
        combo.EndUpdate();
    }

    public static T? Valor<T>(this ComboBox combo) => combo.SelectedItem is Opcion<T> o ? o.Valor : default;
}