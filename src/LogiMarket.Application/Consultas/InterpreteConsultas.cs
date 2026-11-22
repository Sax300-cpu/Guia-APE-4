using System.Globalization;
using LogiMarket.Application.Servicios;
using LogiMarket.Domain.Pedidos;

namespace LogiMarket.Application.Consultas;

public sealed class ErrorSintaxisConsultaException(string mensaje, int posicion)
    : Exception($"{mensaje} (posicion {posicion})")
{
    public int Posicion { get; } = posicion;
}

/// <summary>Expresion del Interpreter: se evalua en memoria sobre DTO; nunca se concatena SQL.</summary>
public interface IExpresionConsulta
{
    bool Evaluar(PedidoResumenDto pedido);
}

// Expresiones terminales
public sealed class ExpresionEstado(EstadoPedido esperado) : IExpresionConsulta
{
    public bool Evaluar(PedidoResumenDto p) => string.Equals(p.Estado, esperado.ToString(), StringComparison.OrdinalIgnoreCase);
}

public sealed class ExpresionTotalMayorIgual(decimal minimo) : IExpresionConsulta
{
    public bool Evaluar(PedidoResumenDto p) => p.Total >= minimo;
}

public sealed class ExpresionCliente(string nombre) : IExpresionConsulta
{
    public bool Evaluar(PedidoResumenDto p) => string.Equals(p.Cliente, nombre, StringComparison.OrdinalIgnoreCase);
}

// Expresiones no terminales
public sealed class ExpresionAnd(IExpresionConsulta izquierda, IExpresionConsulta derecha) : IExpresionConsulta
{
    public bool Evaluar(PedidoResumenDto p) => izquierda.Evaluar(p) && derecha.Evaluar(p);
}

public sealed class ExpresionOr(IExpresionConsulta izquierda, IExpresionConsulta derecha) : IExpresionConsulta
{
    public bool Evaluar(PedidoResumenDto p) => izquierda.Evaluar(p) || derecha.Evaluar(p);
}

/// <summary>
/// Gramatica acotada (AND tiene mayor precedencia que OR; los parentesis la alteran):
///   consulta := or ; or := and (OR and)* ; and := termino (AND termino)*
///   termino  := '(' or ')' | ESTADO = valor | TOTAL &gt;= numero | CLIENTE = texto
/// </summary>
public static class ParserConsultas
{
    private enum TipoToken { Palabra, Cadena, Igual, MayorIgual, ParAbre, ParCierra, Fin }

    private sealed record Token(TipoToken Tipo, string Texto, int Posicion);

    public static IExpresionConsulta Analizar(string consulta)
    {
        if (string.IsNullOrWhiteSpace(consulta)) throw new ErrorSintaxisConsultaException("La consulta esta vacia", 0);
        return new Analizador(Tokenizar(consulta)).Analizar();
    }

    private static List<Token> Tokenizar(string texto)
    {
        var tokens = new List<Token>();
        var i = 0;
        while (i < texto.Length)
        {
            var c = texto[i];
            if (char.IsWhiteSpace(c)) { i++; continue; }
            switch (c)
            {
                case '(': tokens.Add(new Token(TipoToken.ParAbre, "(", i++)); continue;
                case ')': tokens.Add(new Token(TipoToken.ParCierra, ")", i++)); continue;
                case '=': tokens.Add(new Token(TipoToken.Igual, "=", i++)); continue;
                case '>':
                    if (i + 1 >= texto.Length || texto[i + 1] != '=')
                        throw new ErrorSintaxisConsultaException("Operador no valido: solo se admite '>='", i);
                    tokens.Add(new Token(TipoToken.MayorIgual, ">=", i));
                    i += 2;
                    continue;
                case '"':
                    var cierre = texto.IndexOf('"', i + 1);
                    if (cierre < 0) throw new ErrorSintaxisConsultaException("Falta cerrar las comillas", i);
                    tokens.Add(new Token(TipoToken.Cadena, texto[(i + 1)..cierre], i));
                    i = cierre + 1;
                    continue;
            }
            if (!EsCaracterDePalabra(c))
                throw new ErrorSintaxisConsultaException($"Caracter no valido '{c}'", i);
            var inicio = i;
            while (i < texto.Length && EsCaracterDePalabra(texto[i])) i++;
            tokens.Add(new Token(TipoToken.Palabra, texto[inicio..i], inicio));
        }
        tokens.Add(new Token(TipoToken.Fin, string.Empty, texto.Length));
        return tokens;
    }

    private static bool EsCaracterDePalabra(char c) => char.IsLetterOrDigit(c) || c is '_' or '.' or '-';

    private sealed class Analizador(List<Token> tokens)
    {
        private int _actual;

        private Token Actual => tokens[_actual];

        public IExpresionConsulta Analizar()
        {
            var expresion = AnalizarOr();
            if (Actual.Tipo != TipoToken.Fin)
                throw new ErrorSintaxisConsultaException($"Se esperaba AND, OR o el fin de la consulta, se encontro '{Actual.Texto}'", Actual.Posicion);
            return expresion;
        }

        private IExpresionConsulta AnalizarOr()
        {
            var izquierda = AnalizarAnd();
            while (EsPalabra("OR"))
            {
                _actual++;
                izquierda = new ExpresionOr(izquierda, AnalizarAnd());
            }
            return izquierda;
        }

        private IExpresionConsulta AnalizarAnd()
        {
            var izquierda = AnalizarTermino();
            while (EsPalabra("AND"))
            {
                _actual++;
                izquierda = new ExpresionAnd(izquierda, AnalizarTermino());
            }
            return izquierda;
        }

        private IExpresionConsulta AnalizarTermino()
        {
            if (Actual.Tipo == TipoToken.ParAbre)
            {
                _actual++;
                var interna = AnalizarOr();
                if (Actual.Tipo != TipoToken.ParCierra)
                    throw new ErrorSintaxisConsultaException("Falta cerrar el parentesis", Actual.Posicion);
                _actual++;
                return interna;
            }
            if (Actual.Tipo != TipoToken.Palabra)
                throw new ErrorSintaxisConsultaException($"Se esperaba un campo (ESTADO, TOTAL, CLIENTE) o '(', se encontro '{Descripcion(Actual)}'", Actual.Posicion);

            var campo = Actual;
            _actual++;
            switch (campo.Texto.ToUpperInvariant())
            {
                case "ESTADO":
                    Exigir(TipoToken.Igual, "'=' despues de ESTADO");
                    var estado = ValorTexto("un estado");
                    if (!Enum.TryParse<EstadoPedido>(estado.Texto, true, out var parseado) || !Enum.IsDefined(parseado))
                        throw new ErrorSintaxisConsultaException(
                            $"Estado desconocido '{estado.Texto}'. Validos: {string.Join(", ", Enum.GetNames<EstadoPedido>().Select(n => n.ToUpperInvariant()))}", estado.Posicion);
                    return new ExpresionEstado(parseado);
                case "TOTAL":
                    Exigir(TipoToken.MayorIgual, "'>=' despues de TOTAL");
                    var numero = ValorTexto("un numero");
                    if (!decimal.TryParse(numero.Texto, NumberStyles.Number, CultureInfo.InvariantCulture, out var minimo))
                        throw new ErrorSintaxisConsultaException($"TOTAL requiere un numero, se encontro '{numero.Texto}'", numero.Posicion);
                    return new ExpresionTotalMayorIgual(minimo);
                case "CLIENTE":
                    Exigir(TipoToken.Igual, "'=' despues de CLIENTE");
                    return new ExpresionCliente(ValorTexto("un nombre de cliente").Texto);
                default:
                    throw new ErrorSintaxisConsultaException($"Campo desconocido '{campo.Texto}'. Validos: ESTADO, TOTAL, CLIENTE", campo.Posicion);
            }
        }

        private bool EsPalabra(string palabra) =>
            Actual.Tipo == TipoToken.Palabra && Actual.Texto.Equals(palabra, StringComparison.OrdinalIgnoreCase);

        private void Exigir(TipoToken tipo, string descripcion)
        {
            if (Actual.Tipo != tipo)
                throw new ErrorSintaxisConsultaException($"Se esperaba {descripcion}, se encontro '{Descripcion(Actual)}'", Actual.Posicion);
            _actual++;
        }

        private Token ValorTexto(string descripcion)
        {
            if (Actual.Tipo is not (TipoToken.Palabra or TipoToken.Cadena))
                throw new ErrorSintaxisConsultaException($"Se esperaba {descripcion}, se encontro '{Descripcion(Actual)}'", Actual.Posicion);
            return tokens[_actual++];
        }

        private static string Descripcion(Token t) => t.Tipo == TipoToken.Fin ? "el fin de la consulta" : t.Texto;
    }
}