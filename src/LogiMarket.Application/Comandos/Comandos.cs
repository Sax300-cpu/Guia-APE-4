using LogiMarket.Application.Comun;
using LogiMarket.Application.Puertos;
using LogiMarket.Application.Seguridad;
using LogiMarket.Application.Servicios;
using LogiMarket.Domain.Auditoria;
using LogiMarket.Domain.Devoluciones;

namespace LogiMarket.Application.Comandos;

/// <summary>Command: encapsula una accion con su entrada para ejecutarla desde un boton o una prueba.</summary>
public interface IComando<TResultado>
{
    string Nombre { get; }
    string Entrada { get; }
    Task<TResultado> EjecutarAsync(CancellationToken ct = default);
}

public sealed class ConfirmarPedidoCommand(ConfirmacionPedidoFacade receptor, int pedidoId, string clave) : IComando<ResultadoConfirmacion>
{
    public string Nombre => nameof(ConfirmarPedidoCommand);
    public string Entrada => $"pedido={pedidoId}, clave={clave}";
    public Task<ResultadoConfirmacion> EjecutarAsync(CancellationToken ct = default) => receptor.ConfirmarAsync(pedidoId, clave, ct);
}

public sealed class CancelarPedidoCommand(ServicioPedidos receptor, int pedidoId) : IComando<ResultadoOperacion>
{
    public string Nombre => nameof(CancelarPedidoCommand);
    public string Entrada => $"pedido={pedidoId}";
    public Task<ResultadoOperacion> EjecutarAsync(CancellationToken ct = default) => receptor.CancelarAsync(pedidoId, ct);
}

public sealed class DespacharPedidoCommand(ServicioDespacho receptor, int pedidoId) : IComando<ResultadoOperacion>
{
    public string Nombre => nameof(DespacharPedidoCommand);
    public string Entrada => $"pedido={pedidoId}";
    public Task<ResultadoOperacion> EjecutarAsync(CancellationToken ct = default) => receptor.DespacharAsync(pedidoId, ct);
}

public sealed class EntregarPedidoCommand(ServicioDespacho receptor, int pedidoId) : IComando<ResultadoOperacion>
{
    public string Nombre => nameof(EntregarPedidoCommand);
    public string Entrada => $"pedido={pedidoId}";
    public Task<ResultadoOperacion> EjecutarAsync(CancellationToken ct = default) => receptor.EntregarAsync(pedidoId, ct);
}

public sealed class ProcesarDevolucionCommand(ServicioDevoluciones receptor, SolicitudDevolucion solicitud) : IComando<ResultadoDevolucion>
{
    public string Nombre => nameof(ProcesarDevolucionCommand);
    public string Entrada => $"pedido={solicitud.PedidoId}, motivo={solicitud.Motivo}, sinUso={solicitud.SinUso}";
    public Task<ResultadoDevolucion> EjecutarAsync(CancellationToken ct = default) => receptor.SolicitarAsync(solicitud, ct);
}

public sealed record RegistroComando(DateTime Fecha, string Usuario, string Comando, string Entrada, string Resultado);

/// <summary>Invocador: ejecuta cada comando y deja el registro (historial en memoria y bitacora persistente).</summary>
public sealed class InvocadorComandos(ISesionUsuario sesion, IRepositorioAuditoria auditoria, IUnitOfWork uow, IClock reloj)
{
    private readonly List<RegistroComando> _historial = new();

    public IReadOnlyList<RegistroComando> Historial => _historial;

    public async Task<TResultado> EjecutarAsync<TResultado>(IComando<TResultado> comando, CancellationToken ct = default)
    {
        try
        {
            var resultado = await comando.EjecutarAsync(ct);
            await RegistrarAsync(comando, resultado?.ToString() ?? "(sin resultado)", ct);
            return resultado;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await RegistrarAsync(comando, $"EXCEPCION {ex.GetType().Name}: {ex.Message}", ct);
            throw;
        }
    }

    private async Task RegistrarAsync<T>(IComando<T> comando, string resultado, CancellationToken ct)
    {
        var ahora = reloj.UtcAhora;
        _historial.Add(new RegistroComando(ahora, sesion.NombreUsuario, comando.Nombre, comando.Entrada, resultado));
        await auditoria.AgregarAsync(new RegistroAuditoria(ahora, sesion.NombreUsuario, $"COMANDO_{comando.Nombre}",
            $"{comando.Entrada} => {resultado}", CategoriaAuditoria.Negocio), ct);
        await uow.GuardarCambiosAsync(ct);
    }
}