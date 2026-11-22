using LogiMarket.Application.Comun;
using LogiMarket.Application.Puertos;
using LogiMarket.Domain.Auditoria;
using LogiMarket.Domain.Usuarios;

namespace LogiMarket.Application.Seguridad;

public enum Permiso
{
    GestionarCatalogo, GestionarClientes, GestionarPedidos, CobrarPedido, DespacharPedido,
    EntregarPedido, ProcesarDevolucion, Consultar, VerAuditoria, UsarDemo
}

public static class MatrizPermisos
{
    private static readonly IReadOnlyDictionary<Rol, IReadOnlySet<Permiso>> Mapa = new Dictionary<Rol, IReadOnlySet<Permiso>>
    {
        [Rol.Admin] = Enum.GetValues<Permiso>().ToHashSet(),
        [Rol.Ventas] = new HashSet<Permiso> { Permiso.GestionarClientes, Permiso.GestionarPedidos, Permiso.CobrarPedido, Permiso.ProcesarDevolucion, Permiso.Consultar },
        [Rol.Bodega] = new HashSet<Permiso> { Permiso.DespacharPedido, Permiso.EntregarPedido, Permiso.Consultar },
        [Rol.Consulta] = new HashSet<Permiso> { Permiso.Consultar, Permiso.VerAuditoria }
    };

    public static bool Tiene(Rol rol, Permiso permiso) => Mapa[rol].Contains(permiso);
}

public interface ISesionUsuario
{
    bool Autenticado { get; }
    string NombreUsuario { get; }
    Rol? Rol { get; }
}

public sealed class SesionUsuario : ISesionUsuario
{
    private volatile Usuario? _usuario;

    public bool Autenticado => _usuario is not null;
    public string NombreUsuario => _usuario?.NombreUsuario ?? "anonimo";
    public Rol? Rol => _usuario?.Rol;

    public void Iniciar(Usuario usuario) => _usuario = usuario;
    public void Cerrar() => _usuario = null;
}

public interface IAutorizador
{
    bool Tiene(Permiso permiso);
    /// <summary>Lanza AccesoDenegadoException y deja traza en la auditoria de seguridad.</summary>
    Task ExigirAsync(Permiso permiso, CancellationToken ct = default);
}

public sealed class Autorizador(ISesionUsuario sesion, IRepositorioAuditoria auditoria, IUnitOfWork uow, IClock reloj) : IAutorizador
{
    public bool Tiene(Permiso permiso) => sesion.Rol is { } rol && MatrizPermisos.Tiene(rol, permiso);

    public async Task ExigirAsync(Permiso permiso, CancellationToken ct = default)
    {
        if (Tiene(permiso)) return;
        await auditoria.AgregarAsync(new RegistroAuditoria(reloj.UtcAhora, sesion.NombreUsuario, "ACCESO_DENEGADO",
            $"Permiso requerido: {permiso}", CategoriaAuditoria.Seguridad), ct);
        await uow.GuardarCambiosAsync(ct);
        throw new AccesoDenegadoException($"El usuario '{sesion.NombreUsuario}' no tiene permiso para {permiso}.");
    }
}

public sealed class ServicioAutenticacion(IRepositorioUsuarios usuarios, IPasswordHasher hasher, SesionUsuario sesion,
    IRepositorioAuditoria auditoria, IUnitOfWork uow, IClock reloj)
{
    public async Task<bool> IniciarSesionAsync(string nombreUsuario, string password, CancellationToken ct = default)
    {
        var usuario = await usuarios.ObtenerPorNombreAsync(nombreUsuario, ct);
        var valido = usuario is { Activo: true } && hasher.Verificar(password, usuario.PasswordHash);
        await auditoria.AgregarAsync(new RegistroAuditoria(reloj.UtcAhora, nombreUsuario,
            valido ? "LOGIN_OK" : "LOGIN_FALLIDO", valido ? $"Rol {usuario!.Rol}" : "Credenciales invalidas",
            CategoriaAuditoria.Seguridad), ct);
        await uow.GuardarCambiosAsync(ct);
        if (valido) sesion.Iniciar(usuario!);
        return valido;
    }

    public async Task CerrarSesionAsync(CancellationToken ct = default)
    {
        await auditoria.AgregarAsync(new RegistroAuditoria(reloj.UtcAhora, sesion.NombreUsuario, "LOGOUT", "", CategoriaAuditoria.Seguridad), ct);
        await uow.GuardarCambiosAsync(ct);
        sesion.Cerrar();
    }
}