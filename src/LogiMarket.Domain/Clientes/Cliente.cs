using LogiMarket.Domain.Common;

namespace LogiMarket.Domain.Clientes;

public enum CategoriaCliente { Normal = 0, Vip = 1 }

public sealed class Cliente
{
    public int Id { get; private set; }
    public string Nombre { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public CategoriaCliente Categoria { get; private set; }
    public bool Activo { get; private set; } = true;

    private Cliente() { }

    public Cliente(string nombre, string email, CategoriaCliente categoria)
    {
        Actualizar(nombre, email, categoria);
    }

    public void Actualizar(string nombre, string email, CategoriaCliente categoria)
    {
        if (string.IsNullOrWhiteSpace(nombre)) throw new DomainException("El nombre del cliente es obligatorio.");
        Nombre = nombre.Trim();
        Email = email?.Trim() ?? string.Empty;
        Categoria = categoria;
    }

    public void Desactivar() => Activo = false;
    public void Activar() => Activo = true;
}