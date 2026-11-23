using LogiMarket.Domain.Common;

namespace LogiMarket.Domain.Auditoria;

public enum CategoriaAuditoria { Negocio, Seguridad }

public sealed class RegistroAuditoria
{
    public int Id { get; private set; }
    public DateTime Fecha { get; private set; }
    public string Usuario { get; private set; } = string.Empty;
    public string Accion { get; private set; } = string.Empty;
    public string Detalle { get; private set; } = string.Empty;
    public CategoriaAuditoria Categoria { get; private set; }

    private RegistroAuditoria() { }

    public RegistroAuditoria(DateTime fecha, string usuario, string accion, string detalle, CategoriaAuditoria categoria)
    {
        if (string.IsNullOrWhiteSpace(accion)) throw new DomainException("La accion es obligatoria.");
        Fecha = fecha;
        Usuario = usuario;
        Accion = accion;
        Detalle = detalle;
        Categoria = categoria;
    }
}
