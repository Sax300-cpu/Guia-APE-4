namespace LogiMarket.Domain.Common;

/// <summary>Invariantes numericas compartidas por las entidades del dominio.</summary>
public static class Guardia
{
    public static void CantidadPositiva(int cantidad, string mensaje = "La cantidad debe ser positiva.")
    {
        if (cantidad <= 0) throw new DomainException(mensaje);
    }
}
