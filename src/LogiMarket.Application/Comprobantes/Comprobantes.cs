using LogiMarket.Application.Puertos;
using LogiMarket.Application.Seguridad;
using LogiMarket.Domain.Common;
using LogiMarket.Domain.Pedidos;

namespace LogiMarket.Application.Comprobantes;

public sealed record DocumentoTexto(string Titulo, IReadOnlyList<string> Lineas)
{
    public string ComoTexto() => Titulo + Environment.NewLine + new string('=', Titulo.Length) + Environment.NewLine
                                 + string.Join(Environment.NewLine, Lineas) + Environment.NewLine;
}

public sealed record ResultadoSalida(string Formato, string Destino, string Contenido);

/// <summary>Implementor del Bridge: medio de salida (vista previa, archivo TXT, ...).</summary>
public interface ISalidaDocumento
{
    string Formato { get; }
    Task<ResultadoSalida> EscribirAsync(string nombreBase, DocumentoTexto documento, CancellationToken ct = default);
}

/// <summary>Abstraccion del Bridge: define QUE se emite; el medio concreto llega por ISalidaDocumento.</summary>
public abstract class Comprobante(ISalidaDocumento salida)
{
    protected abstract string NombreBase { get; }
    protected abstract DocumentoTexto Construir();

    public Task<ResultadoSalida> EmitirAsync(CancellationToken ct = default) =>
        salida.EscribirAsync(NombreBase, Construir(), ct);
}

public sealed class ComprobanteVenta(ISalidaDocumento salida, Pedido pedido, string? guia) : Comprobante(salida)
{
    protected override string NombreBase => $"venta_{pedido.Id:000000}";

    protected override DocumentoTexto Construir()
    {
        var lineas = new List<string> { $"Cliente: {pedido.Cliente.Nombre}", $"Estado: {pedido.Estado}", "" };
        lineas.AddRange(pedido.Detalles.Select(d =>
            $"{d.Sku,-16} x{d.Cantidad,-3} {Dinero.Formato(d.PrecioUnitario),12} {Dinero.Formato(d.Subtotal),12}"));
        lineas.Add("");
        lineas.Add($"Subtotal: {Dinero.Formato(pedido.Subtotal)}");
        lineas.Add($"Descuento ({pedido.PoliticaDescuento}): {Dinero.Formato(pedido.Descuento)}");
        lineas.Add($"Tarifa de envio ({pedido.Entrega}): {Dinero.Formato(pedido.TarifaEnvio)}");
        lineas.Add($"Total: {Dinero.Formato(pedido.Total)}");
        if (guia is not null) lineas.Add($"Guia: {guia}");
        lineas.Add("Comprobante interno de demostracion (no es factura tributaria).");
        return new DocumentoTexto($"COMPROBANTE DE VENTA #{pedido.Id}", lineas);
    }
}

public sealed class ComprobanteDevolucion(ISalidaDocumento salida, Pedido pedido, Domain.Devoluciones.Devolucion devolucion) : Comprobante(salida)
{
    protected override string NombreBase => $"devolucion_{pedido.Id:000000}";

    protected override DocumentoTexto Construir() => new($"COMPROBANTE DE DEVOLUCION #{pedido.Id}", new[]
    {
        $"Cliente: {pedido.Cliente.Nombre}",
        $"Motivo: {devolucion.Motivo}",
        $"Reembolso simulado: {Dinero.Formato(devolucion.Reembolso)}",
        "La tarifa de envio no se reembolsa.",
        "Comprobante interno de demostracion (no es factura tributaria)."
    });
}

public sealed class ServicioComprobantes(IRepositorioPedidos pedidos, IRepositorioDevoluciones devoluciones,
    IRepositorioDespachos despachos, IEnumerable<ISalidaDocumento> salidas, IAutorizador autorizador)
{
    public async Task<ResultadoSalida> EmitirVentaAsync(int pedidoId, string formato, CancellationToken ct = default)
    {
        await autorizador.ExigirAsync(Permiso.Consultar, ct);
        var pedido = await pedidos.ObtenerPorIdAsync(pedidoId, ct) ?? throw new DomainException("El pedido no existe.");
        var guia = (await despachos.ObtenerPorPedidoAsync(pedidoId, ct))?.Guia;
        return await new ComprobanteVenta(Salida(formato), pedido, guia).EmitirAsync(ct);
    }

    public async Task<ResultadoSalida> EmitirDevolucionAsync(int pedidoId, string formato, CancellationToken ct = default)
    {
        await autorizador.ExigirAsync(Permiso.Consultar, ct);
        var pedido = await pedidos.ObtenerPorIdAsync(pedidoId, ct) ?? throw new DomainException("El pedido no existe.");
        var devolucion = await devoluciones.ObtenerPorPedidoAsync(pedidoId, ct)
            ?? throw new DomainException("El pedido no tiene devolucion registrada.");
        return await new ComprobanteDevolucion(Salida(formato), pedido, devolucion).EmitirAsync(ct);
    }

    private ISalidaDocumento Salida(string formato) =>
        salidas.FirstOrDefault(s => s.Formato.Equals(formato, StringComparison.OrdinalIgnoreCase))
        ?? throw new DomainException($"Formato de salida desconocido: {formato}.");
}