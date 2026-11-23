using LogiMarket.Application.Comprobantes;

namespace LogiMarket.Infrastructure.Salidas;

public sealed record OpcionesSalidaDocumento(string Directorio)
{
    public static OpcionesSalidaDocumento Predeterminadas() => new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LogiMarket360", "comprobantes"));
}

/// <summary>Salida "vista previa": conserva el ultimo documento para que la interfaz lo muestre.</summary>
public sealed class VistaPreviaSalida : ISalidaDocumento
{
    public string Formato => "VISTA_PREVIA";
    public string UltimaVista { get; private set; } = string.Empty;
    public event Action<string>? VistaGenerada;

    public Task<ResultadoSalida> EscribirAsync(string nombreBase, DocumentoTexto documento, CancellationToken ct = default)
    {
        var texto = documento.ComoTexto();
        UltimaVista = texto;
        VistaGenerada?.Invoke(texto);
        return Task.FromResult(new ResultadoSalida(Formato, "(vista previa)", texto));
    }
}

/// <summary>Salida "archivo TXT": escribe de verdad el contenido en disco.</summary>
public sealed class ArchivoTxtSalida(OpcionesSalidaDocumento opciones) : ISalidaDocumento
{
    public string Formato => "TXT";

    public async Task<ResultadoSalida> EscribirAsync(string nombreBase, DocumentoTexto documento, CancellationToken ct = default)
    {
        Directory.CreateDirectory(opciones.Directorio);
        var ruta = Path.Combine(opciones.Directorio, $"{nombreBase}_{DateTime.UtcNow:yyyyMMddHHmmssfff}.txt");
        var texto = documento.ComoTexto();
        await File.WriteAllTextAsync(ruta, texto, ct);
        return new ResultadoSalida(Formato, ruta, texto);
    }
}
