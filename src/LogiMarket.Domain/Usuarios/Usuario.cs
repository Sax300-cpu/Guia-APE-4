using LogiMarket.Domain.Common;

namespace LogiMarket.Domain.Usuarios;

public enum Rol { Admin, Ventas, Bodega, Consulta }

public sealed class Usuario
{
    public int Id { get; private set; }
    public string NombreUsuario { get; private set; } = string.Empty;
    public Rol Rol { get; private set; }
    /// <summary>Hash PBKDF2 con sal; nunca texto claro.</summary>
    public string PasswordHash { get; private set; } = string.Empty;
    public bool Activo { get; private set; } = true;

    private Usuario() { }

    public Usuario(string nombreUsuario, Rol rol, string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(nombreUsuario)) throw new DomainException("El usuario es obligatorio.");
        if (string.IsNullOrWhiteSpace(passwordHash)) throw new DomainException("El hash de contrasena es obligatorio.");
        NombreUsuario = nombreUsuario.Trim().ToLowerInvariant();
        Rol = rol;
        PasswordHash = passwordHash;
    }
}
