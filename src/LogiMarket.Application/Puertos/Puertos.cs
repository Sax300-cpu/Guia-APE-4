using LogiMarket.Domain.Catalogo;
using LogiMarket.Domain.Clientes;
using LogiMarket.Domain.Pedidos;
using LogiMarket.Domain.Usuarios;

namespace LogiMarket.Application.Puertos;

/// <summary>Repository: contratos del nucleo; los adaptadores EF viven en Infrastructure.</summary>
public interface IRepositorioCatalogo
{
    /// <summary>Carga todo el catalogo con el grafo de kits resuelto.</summary>
    Task<IReadOnlyList<ElementoCatalogo>> ObtenerTodosAsync(CancellationToken ct = default);
    Task<ElementoCatalogo?> ObtenerPorSkuAsync(string sku, CancellationToken ct = default);
    Task AgregarAsync(ElementoCatalogo elemento, CancellationToken ct = default);
    Task EliminarAsync(ElementoCatalogo elemento, CancellationToken ct = default);
}

public interface IRepositorioClientes
{
    Task<Cliente?> ObtenerPorIdAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<Cliente>> BuscarAsync(string? texto, CancellationToken ct = default);
    Task AgregarAsync(Cliente cliente, CancellationToken ct = default);
}

public interface IRepositorioPedidos
{
    Task<Pedido?> ObtenerPorIdAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<Pedido>> ListarAsync(CancellationToken ct = default);
    Task AgregarAsync(Pedido pedido, CancellationToken ct = default);
}

public interface IRepositorioUsuarios
{
    Task<Usuario?> ObtenerPorNombreAsync(string nombreUsuario, CancellationToken ct = default);
}

/// <summary>Unit of Work real: guarda cambios y abre transacciones sobre el DbContext.</summary>
public interface IUnitOfWork
{
    Task<int> GuardarCambiosAsync(CancellationToken ct = default);
    Task EjecutarEnTransaccionAsync(Func<CancellationToken, Task> operacion, CancellationToken ct = default);
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verificar(string password, string hash);
}

public interface IClock
{
    DateTime UtcAhora { get; }
}