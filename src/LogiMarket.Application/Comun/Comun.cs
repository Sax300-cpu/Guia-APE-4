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

/// <summary>Codigos estables de rechazo que consumen la interfaz, las pruebas y la bitacora.</summary>
public static class CodigosResultado
{
    public const string NoEncontrado = "NO_ENCONTRADO";
    public const string EstadoInvalido = "ESTADO_INVALIDO";
    public const string YaDevuelto = "YA_DEVUELTO";
    public const string ReglaDeNegocio = "REGLA_DE_NEGOCIO";
    public const string NoElegible = "NO_ELEGIBLE";
    public const string Conflicto = "CONFLICTO";
    public const string CondicionNoCumplida = "CONDICION_NO_CUMPLIDA";
    public const string Validacion = "VALIDACION";
    public const string Duplicado = "DUPLICADO";
    public const string CobroRechazado = "COBRO_RECHAZADO";
    public const string ClaveObligatoria = "CLAVE_OBLIGATORIA";
    public const string ClaveEnUso = "CLAVE_EN_USO";
    public const string MensajePedidoInexistente = "El pedido no existe.";
}
