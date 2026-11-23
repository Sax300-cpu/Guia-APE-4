using LogiMarket.Application.Catalogo;
using LogiMarket.Application.Comun;
using LogiMarket.Application.Consultas;
using LogiMarket.Application.Puertos;
using LogiMarket.Application.Seguridad;
using LogiMarket.Domain.Catalogo;
using LogiMarket.Domain.Clientes;
using LogiMarket.Domain.Common;
using LogiMarket.Domain.Inventario;

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

    public async Task ActualizarAsync(int id, string nombre, string email, CategoriaCliente categoria, bool activo, CancellationToken ct = default)
    {
        await autorizador.ExigirAsync(Permiso.GestionarClientes, ct);
        var cliente = await clientes.ObtenerPorIdAsync(id, ct) ?? throw new DomainException("El cliente no existe.");
        cliente.Actualizar(nombre, email, categoria);
        if (activo) cliente.Activar(); else cliente.Desactivar();
        await uow.GuardarCambiosAsync(ct);
    }

    public async Task<IReadOnlyList<Cliente>> BuscarAsync(string? texto, CancellationToken ct = default)
    {
        await autorizador.ExigirAsync(Permiso.Consultar, ct);
        return await clientes.BuscarAsync(texto, ct);
    }

    public async Task<IReadOnlyList<ClienteDto>> ListarAsync(string? texto, CancellationToken ct = default) =>
        (await BuscarAsync(texto, ct)).Select(c => new ClienteDto(c.Id, c.Nombre, c.Email, c.Categoria.ToString(), c.Activo)).ToList();
}

public sealed class ServicioCatalogo(IRepositorioCatalogo catalogo, IRepositorioInventario inventario, CatalogoVisualFlyweightFactory flyweight,
    IAutorizador autorizador, IUnitOfWork uow, IClock reloj)
{
    public async Task CrearProductoAsync(string sku, string nombre, decimal precio, decimal pesoKg, int stock, CancellationToken ct = default)
    {
        await autorizador.ExigirAsync(Permiso.GestionarCatalogo, ct);
        var producto = new ProductoSimple(sku, nombre, precio, pesoKg, stock);
        await uow.EjecutarEnTransaccionAsync(async c =>
        {
            await catalogo.AgregarAsync(producto, c);
            await uow.GuardarCambiosAsync(c); // se necesita el Id para el movimiento de carga inicial
            if (stock > 0)
                await inventario.AgregarMovimientoAsync(new MovimientoInventario(producto.Id, stock, MotivoMovimiento.CargaInicial,
                    $"ALTA-{producto.Sku}", producto.Stock, reloj.UtcAhora), c);
        }, ct);
    }

    public async Task CrearKitAsync(string sku, string nombre, IReadOnlyList<(string Sku, int Cantidad)> componentes, CancellationToken ct = default)
    {
        await autorizador.ExigirAsync(Permiso.GestionarCatalogo, ct);
        if (componentes.Count == 0) throw new DomainException("Un kit necesita al menos un componente.");
        var todos = await catalogo.ObtenerTodosAsync(ct);
        var kit = new KitCompuesto(sku, nombre);
        foreach (var (skuHijo, cantidad) in componentes)
        {
            var hijo = todos.FirstOrDefault(e => e.Sku == skuHijo.Trim().ToUpperInvariant())
                ?? throw new DomainException($"El componente {skuHijo} no existe.");
            kit.Agregar(hijo, cantidad);
        }
        await catalogo.AgregarAsync(kit, ct);
        await uow.GuardarCambiosAsync(ct);
    }

    public async Task ActualizarAsync(string sku, string nombre, decimal? precio, CancellationToken ct = default)
    {
        await autorizador.ExigirAsync(Permiso.GestionarCatalogo, ct);
        var elemento = await catalogo.ObtenerPorSkuAsync(sku, ct) ?? throw new DomainException($"El SKU {sku} no existe.");
        elemento.Renombrar(nombre);
        if (precio is not null)
            (elemento as ProductoSimple ?? throw new DomainException("El precio de un kit se calcula de sus componentes.")).CambiarPrecio(precio.Value);
        await uow.GuardarCambiosAsync(ct);
        flyweight.Invalidar(elemento.Sku); // los metadatos compartidos ya no son vigentes
    }

    /// <summary>Solo se eliminan elementos sin historial: la integridad referencial protege el resto.</summary>
    public async Task EliminarAsync(string sku, CancellationToken ct = default)
    {
        await autorizador.ExigirAsync(Permiso.GestionarCatalogo, ct);
        var todos = await catalogo.ObtenerTodosAsync(ct);
        var elemento = todos.FirstOrDefault(e => e.Sku == sku.Trim().ToUpperInvariant()) ?? throw new DomainException($"El SKU {sku} no existe.");
        if (todos.OfType<KitCompuesto>().Any(k => k.Componentes.Any(c => ReferenceEquals(c.Hijo, elemento))))
            throw new DomainException($"{elemento.Sku} forma parte de un kit y no se puede eliminar.");
        if (elemento is ProductoSimple producto && await inventario.ExistenMovimientosAsync(producto.Id, ct))
            throw new DomainException($"{elemento.Sku} tiene historial (movimientos o pedidos) y no se puede eliminar.");
        try
        {
            await catalogo.EliminarAsync(elemento, ct);
            await uow.GuardarCambiosAsync(ct);
        }
        catch (IntegridadReferencialException)
        {
            throw new DomainException($"{elemento.Sku} tiene historial (movimientos o pedidos) y no se puede eliminar.");
        }
        flyweight.Invalidar(elemento.Sku);
    }
}

public sealed class ConsultasCatalogo(IRepositorioCatalogo catalogo, CatalogoVisualFlyweightFactory flyweight, IAutorizador autorizador)
{
    public async Task<IReadOnlyList<CatalogoFilaDto>> ListarAsync(string? texto, CancellationToken ct = default)
    {
        await autorizador.ExigirAsync(Permiso.Consultar, ct);
        var filtro = texto?.Trim();
        return (await catalogo.ObtenerTodosAsync(ct))
            .Where(e => string.IsNullOrEmpty(filtro) || e.Sku.Contains(filtro, StringComparison.OrdinalIgnoreCase)
                                                      || e.Nombre.Contains(filtro, StringComparison.OrdinalIgnoreCase))
            .Select(e =>
            {
                var fila = flyweight.CrearFila(e); // metadatos compartidos; stock y precio son datos externos de la fila
                return new CatalogoFilaDto(fila.Metadatos.Sku, fila.Metadatos.Nombre, fila.Metadatos.Categoria,
                    e is KitCompuesto ? "Kit" : "Producto", fila.Precio, e is ProductoSimple ? fila.Stock : null, e.Peso,
                    e is KitCompuesto k ? string.Join(" + ", k.Componentes.Select(c => $"{c.Hijo.Sku} x{c.Cantidad}")) : string.Empty);
            }).ToList();
    }
}

public sealed class ConsultasInventario(IRepositorioCatalogo catalogo, IRepositorioInventario inventario, IAutorizador autorizador)
{
    public const int UmbralAlerta = 3;

    public async Task<IReadOnlyList<StockDto>> StockAsync(CancellationToken ct = default)
    {
        await autorizador.ExigirAsync(Permiso.Consultar, ct);
        return (await catalogo.ObtenerTodosAsync(ct)).OfType<ProductoSimple>()
            .Select(p => new StockDto(p.Sku, p.Nombre, p.Stock, p.Stock <= UmbralAlerta)).ToList();
    }

    public async Task<IReadOnlyList<MovimientoDto>> MovimientosAsync(string? sku, int maximo = 200, CancellationToken ct = default)
    {
        await autorizador.ExigirAsync(Permiso.Consultar, ct);
        return await inventario.ListarMovimientosAsync(string.IsNullOrWhiteSpace(sku) ? null : sku.Trim().ToUpperInvariant(), maximo, ct);
    }
}

public sealed class ConsultasAuditoria(IRepositorioAuditoria auditoria, IRepositorioOutbox outbox, IAutorizador autorizador)
{
    public async Task<IReadOnlyList<AuditoriaDto>> ListarAsync(int maximo = 300, CancellationToken ct = default)
    {
        await autorizador.ExigirAsync(Permiso.VerAuditoria, ct);
        return (await auditoria.ListarAsync(maximo, ct))
            .Select(a => new AuditoriaDto(a.Fecha, a.Usuario, a.Accion, a.Detalle, a.Categoria.ToString())).ToList();
    }

    public async Task<ResumenOutbox> ResumenOutboxAsync(CancellationToken ct = default)
    {
        await autorizador.ExigirAsync(Permiso.VerAuditoria, ct);
        return await outbox.ResumenAsync(ct);
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

    public async Task<PedidoDetalleDto?> ObtenerAsync(int pedidoId, CancellationToken ct = default)
    {
        await autorizador.ExigirAsync(Permiso.Consultar, ct);
        var p = await pedidos.ObtenerPorIdAsync(pedidoId, ct);
        return p is null ? null : new PedidoDetalleDto(p.Id, p.Cliente.Nombre, p.Estado.ToString(), p.Entrega.ToString(), p.PlazoDias,
            p.Subtotal, p.Descuento, p.PoliticaDescuento, p.TarifaEnvio, p.Total, p.PermiteEdicion,
            p.Detalles.Select(d => new LineaPedidoDto(d.Sku, d.Nombre, d.Cantidad, d.PrecioUnitario, d.Subtotal, d.UnidadesFisicas)).ToList(),
            p.Historial.Select(h => new HistorialEstadoDto(h.Fecha, h.Desde?.ToString() ?? "-", h.Hacia.ToString())).ToList());
    }

    /// <summary>Filtra con el Interpreter (ESTADO = ..., TOTAL >= ..., CLIENTE = ..., AND/OR y parentesis).</summary>
    public async Task<IReadOnlyList<PedidoResumenDto>> FiltrarAsync(string filtro, CancellationToken ct = default)
    {
        var expresion = ParserConsultas.Analizar(filtro); // un error de sintaxis se informa antes de leer datos
        var todos = await ListarAsync(ct);
        return todos.Where(expresion.Evaluar).ToList();
    }
}
