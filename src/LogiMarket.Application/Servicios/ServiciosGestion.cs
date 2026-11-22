using LogiMarket.Application.Consultas;
using LogiMarket.Application.Puertos;
using LogiMarket.Application.Seguridad;
using LogiMarket.Domain.Catalogo;
using LogiMarket.Domain.Clientes;
using LogiMarket.Domain.Pedidos;

namespace LogiMarket.Application.Servicios;

public sealed record PedidoResumenDto(int Id, string Cliente, string Estado, decimal Total, DateTime Fecha);

public sealed class ServicioClientes(IRepositorioClientes clientes, IAutorizador autorizador, IUnitOfWork uow)
{
    public async Task<int> RegistrarAsync(string nombre, string email, CategoriaCliente categoria, CancellationToken ct = default)
    {
        await autorizador.ExigirAsync(Permiso.GestionarClientes, ct);
        var cliente = new Cliente(nombre, email, categoria);
        await clientes.AgregarAsync(cliente, ct);
        await uow.GuardarCambiosAsync(ct);
        return cliente.Id;
    }

    public async Task<IReadOnlyList<Cliente>> BuscarAsync(string? texto, CancellationToken ct = default)
    {
        await autorizador.ExigirAsync(Permiso.Consultar, ct);
        return await clientes.BuscarAsync(texto, ct);
    }
}

public sealed class ServicioCatalogo(IRepositorioCatalogo catalogo, IAutorizador autorizador, IUnitOfWork uow)
{
    public async Task CrearProductoAsync(string sku, string nombre, decimal precio, decimal pesoKg, int stock, CancellationToken ct = default)
    {
        await autorizador.ExigirAsync(Permiso.GestionarCatalogo, ct);
        await catalogo.AgregarAsync(new ProductoSimple(sku, nombre, precio, pesoKg, stock), ct);
        await uow.GuardarCambiosAsync(ct);
    }

    public async Task<IReadOnlyList<ElementoCatalogo>> BuscarAsync(string? texto, CancellationToken ct = default)
    {
        await autorizador.ExigirAsync(Permiso.Consultar, ct);
        var todos = await catalogo.ObtenerTodosAsync(ct);
        return string.IsNullOrWhiteSpace(texto)
            ? todos
            : todos.Where(e => e.Sku.Contains(texto.Trim(), StringComparison.OrdinalIgnoreCase)
                            || e.Nombre.Contains(texto.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
    }
}

/// <summary>Consultas (lado Query de CQRS): solo lectura y DTO, nunca entidades mutables hacia la UI.</summary>
public sealed class ConsultasPedidos(IRepositorioPedidos pedidos, IAutorizador autorizador)
{
    public async Task<IReadOnlyList<PedidoResumenDto>> ListarAsync(CancellationToken ct = default)
    {
        await autorizador.ExigirAsync(Permiso.Consultar, ct);
        return (await pedidos.ListarAsync(ct))
            .Select(p => new PedidoResumenDto(p.Id, p.Cliente.Nombre, p.Estado.ToString(), p.Total, p.CreadoEn))
            .ToList();
    }

    /// <summary>Filtra con el Interpreter (ESTADO = ..., TOTAL >= ..., CLIENTE = ..., AND/OR y parentesis).</summary>
    public async Task<IReadOnlyList<PedidoResumenDto>> FiltrarAsync(string filtro, CancellationToken ct = default)
    {
        var expresion = ParserConsultas.Analizar(filtro); // un error de sintaxis se informa antes de leer datos
        var todos = await ListarAsync(ct);
        return todos.Where(expresion.Evaluar).ToList();
    }
}