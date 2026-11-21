using System.Collections;
using LogiMarket.Domain.Common;

namespace LogiMarket.Domain.Catalogo;

/// <summary>
/// Iterator propio: recorre en orden determinista los SKU finales de un kit anidado propagando
/// cantidades, sin exponer la coleccion interna y con tope de profundidad ante datos corruptos.
/// </summary>
public sealed class IteradorComponentesFisicos : IEnumerator<ComponenteFisico>
{
    private const int ProfundidadMaxima = 32;

    private readonly ElementoCatalogo _raiz;
    private readonly int _cantidadRaiz;
    private readonly Stack<(IEnumerator<KitComponente> Cursor, int Multiplicador)> _pila = new();
    private bool _iniciado;

    public ComponenteFisico Current { get; private set; }
    object IEnumerator.Current => Current;

    internal IteradorComponentesFisicos(ElementoCatalogo raiz, int cantidad)
    {
        if (cantidad <= 0) throw new DomainException("La cantidad debe ser positiva.");
        _raiz = raiz;
        _cantidadRaiz = cantidad;
    }

    public bool MoveNext()
    {
        if (!_iniciado)
        {
            _iniciado = true;
            if (_raiz is ProductoSimple hoja)
            {
                Current = new ComponenteFisico(hoja, _cantidadRaiz);
                return true;
            }
            Empujar((KitCompuesto)_raiz, _cantidadRaiz);
        }

        while (_pila.Count > 0)
        {
            var (cursor, multiplicador) = _pila.Peek();
            if (!cursor.MoveNext()) { _pila.Pop(); continue; }

            var componente = cursor.Current;
            var cantidad = multiplicador * componente.Cantidad;
            switch (componente.Hijo)
            {
                case ProductoSimple producto:
                    Current = new ComponenteFisico(producto, cantidad);
                    return true;
                case KitCompuesto kit:
                    Empujar(kit, cantidad);
                    break;
            }
        }
        return false;
    }

    private void Empujar(KitCompuesto kit, int multiplicador)
    {
        if (_pila.Count >= ProfundidadMaxima)
            throw new DomainException("Anidamiento de kits excesivo o circular.");
        _pila.Push((((IEnumerable<KitComponente>)kit.Componentes).GetEnumerator(), multiplicador));
    }

    public void Reset() => throw new NotSupportedException("El iterador es de un solo recorrido.");

    public void Dispose()
    {
        while (_pila.Count > 0) _pila.Pop().Cursor.Dispose();
    }
}