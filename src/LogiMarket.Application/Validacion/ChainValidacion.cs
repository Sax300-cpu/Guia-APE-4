using LogiMarket.Application.Pedidos;
using LogiMarket.Application.Puertos;
using LogiMarket.Application.Seguridad;
using LogiMarket.Domain.Catalogo;
using LogiMarket.Domain.Pedidos;

namespace LogiMarket.Application.Validacion;

public sealed record ContextoValidacion(Pedido Pedido, IReadOnlyDictionary<string, ProductoSimple> Productos, bool Autorizado);

public sealed record ResultadoValidacion(bool Valido, string? Eslabon, string? Mensaje)
{
    public static ResultadoValidacion Ok { get; } = new(true, null, null);
    public static ResultadoValidacion Falla(string eslabon, string mensaje) => new(false, eslabon, mensaje);
}

/// <summary>Chain of Responsibility: el primer rechazo detiene la cadena y nombra al eslabon causante.</summary>
public abstract class EslabonValidacion
{
    private EslabonValidacion? _siguiente;

    public EslabonValidacion Encadenar(EslabonValidacion siguiente)
    {
        _siguiente = siguiente;
        return siguiente;
    }

    public ResultadoValidacion Validar(ContextoValidacion contexto)
    {
        var resultado = Comprobar(contexto);
        if (!resultado.Valido) return resultado;
        return _siguiente?.Validar(contexto) ?? resultado;
    }

    protected abstract ResultadoValidacion Comprobar(ContextoValidacion contexto);
}

public sealed class EslabonClienteActivo : EslabonValidacion
{
    public const string Nombre = "ClienteActivo";

    protected override ResultadoValidacion Comprobar(ContextoValidacion c) =>
        c.Pedido.Cliente is { Activo: true } ? ResultadoValidacion.Ok
            : ResultadoValidacion.Falla(Nombre, "El cliente no existe o esta inactivo.");
}

public sealed class EslabonLineasYCantidades : EslabonValidacion
{
    public const string Nombre = "LineasYCantidades";

    protected override ResultadoValidacion Comprobar(ContextoValidacion c) =>
        c.Pedido.Detalles.Count > 0
            ? ResultadoValidacion.Ok
            : ResultadoValidacion.Falla(Nombre, Pedido.MensajeSinLineas);
}

public sealed class EslabonStock : EslabonValidacion
{
    public const string Nombre = "Stock";

    protected override ResultadoValidacion Comprobar(ContextoValidacion c)
    {
        foreach (var (sku, requerido) in Requerimientos.PorSku(c.Pedido))
        {
            if (!c.Productos.TryGetValue(sku, out var producto))
                return ResultadoValidacion.Falla(Nombre, $"El producto {sku} ya no existe en el catalogo.");
            if (requerido > producto.Stock)
                return ResultadoValidacion.Falla(Nombre, $"Stock insuficiente de {sku}: requiere {requerido}, disponible {producto.Stock}.");
        }
        return ResultadoValidacion.Ok;
    }
}

public sealed class EslabonCondicionesConfirmacion : EslabonValidacion
{
    public const string Nombre = "Condiciones";

    protected override ResultadoValidacion Comprobar(ContextoValidacion c)
    {
        if (!c.Autorizado) return ResultadoValidacion.Falla(Nombre, "El usuario no esta autorizado para esta operacion.");
        return c.Pedido.Estado is EstadoPedido.Borrador or EstadoPedido.Validado
            ? ResultadoValidacion.Ok
            : ResultadoValidacion.Falla(Nombre, $"Un pedido en estado {c.Pedido.Estado} no se puede confirmar.");
    }
}

public static class PipelineValidacion
{
    /// <summary>cliente activo -> lineas/cantidades -> stock -> autorizacion/condiciones.</summary>
    public static EslabonValidacion Crear()
    {
        var primero = new EslabonClienteActivo();
        primero.Encadenar(new EslabonLineasYCantidades())
               .Encadenar(new EslabonStock())
               .Encadenar(new EslabonCondicionesConfirmacion());
        return primero;
    }
}

public sealed record ValidacionPedido(ResultadoValidacion Resultado, IReadOnlyDictionary<string, ProductoSimple> Productos);

/// <summary>Carga el catalogo vigente y recorre la cadena de validacion: unico punto de entrada para validar y para cobrar.</summary>
public sealed class ValidadorPedido(IRepositorioCatalogo catalogo, IAutorizador autorizador)
{
    public async Task<ValidacionPedido> ValidarAsync(Pedido pedido, Permiso permiso, CancellationToken ct)
    {
        var productos = (await catalogo.ObtenerTodosAsync(ct)).OfType<ProductoSimple>().ToDictionary(p => p.Sku);
        var resultado = PipelineValidacion.Crear().Validar(new ContextoValidacion(pedido, productos, autorizador.Tiene(permiso)));
        return new ValidacionPedido(resultado, productos);
    }
}
