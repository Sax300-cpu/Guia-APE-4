namespace LogiMarket.Application.Comun;

public sealed record ResultadoOperacion(bool Exito, string Codigo, string Mensaje)
{
    public static ResultadoOperacion Ok(string mensaje = "OK") => new(true, "OK", mensaje);
    public static ResultadoOperacion Rechazo(string codigo, string mensaje) => new(false, codigo, mensaje);
}

public sealed class AccesoDenegadoException(string mensaje) : Exception(mensaje);

/// <summary>Conflicto de concurrencia optimista o de bloqueo: la operacion pierde sin efectos parciales.</summary>
public sealed class ConflictoConcurrenciaException(string mensaje, Exception? interna = null) : Exception(mensaje, interna);

public sealed class ViolacionUnicidadException(string mensaje, Exception? interna = null) : Exception(mensaje, interna);

public sealed class CobroRechazadoException(string motivo) : Exception(motivo);
public sealed class IntegridadReferencialException(string mensaje, Exception? interna = null) : Exception(mensaje, interna);
