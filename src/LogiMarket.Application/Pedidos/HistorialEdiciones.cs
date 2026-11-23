using System.Collections.Concurrent;
using LogiMarket.Domain.Common;
using LogiMarket.Domain.Pedidos;

namespace LogiMarket.Application.Pedidos;

/// <summary>Caretaker del Memento: hasta N estados completos con deshacer/rehacer; se bloquea al confirmar.</summary>
public sealed class HistorialEdicionesBorrador(int capacidad = 5)
{
    private readonly object _candado = new();
    private readonly LinkedList<PedidoMemento> _deshacer = new();
    private readonly Stack<PedidoMemento> _rehacer = new();
    private bool _bloqueado;

    public int PasosDeshacer { get { lock (_candado) return _deshacer.Count; } }
    public int PasosRehacer { get { lock (_candado) return _rehacer.Count; } }
    public bool Bloqueado { get { lock (_candado) return _bloqueado; } }

    /// <summary>Guarda el estado ACTUAL antes de aplicar una edicion.</summary>
    public void Registrar(Pedido pedido)
    {
        lock (_candado)
        {
            ExigirActivo();
            _deshacer.AddLast(pedido.CrearMemento());
            if (_deshacer.Count > capacidad) _deshacer.RemoveFirst();
            _rehacer.Clear();
        }
    }

    /// <summary>Anula el ultimo registro cuando la edicion fallo y no cambio nada.</summary>
    public void DescartarUltimoRegistro()
    {
        lock (_candado)
            if (_deshacer.Count > 0) _deshacer.RemoveLast();
    }

    public void Deshacer(Pedido pedido)
    {
        lock (_candado)
        {
            ExigirActivo();
            if (_deshacer.Count == 0) throw new DomainException("No hay cambios para deshacer.");
            var actual = pedido.CrearMemento();
            pedido.Restaurar(_deshacer.Last!.Value); // si falla (pedido no editable) el historial queda intacto
            _deshacer.RemoveLast();
            _rehacer.Push(actual);
        }
    }

    public void Rehacer(Pedido pedido)
    {
        lock (_candado)
        {
            ExigirActivo();
            if (_rehacer.Count == 0) throw new DomainException("No hay cambios para rehacer.");
            var actual = pedido.CrearMemento();
            pedido.Restaurar(_rehacer.Peek());
            _rehacer.Pop();
            _deshacer.AddLast(actual);
            if (_deshacer.Count > capacidad) _deshacer.RemoveFirst();
        }
    }

    /// <summary>Al confirmar se limpia y bloquea: ningun cobro o stock confirmado se puede revertir por aqui.</summary>
    public void Bloquear()
    {
        lock (_candado)
        {
            _deshacer.Clear();
            _rehacer.Clear();
            _bloqueado = true;
        }
    }

    private void ExigirActivo()
    {
        if (_bloqueado) throw new DomainException("El pedido fue confirmado: el historial de ediciones esta bloqueado.");
    }
}

/// <summary>Un historial por borrador, compartido por toda la sesion de la aplicacion.</summary>
public sealed class RegistroHistorialesBorrador
{
    private readonly ConcurrentDictionary<int, HistorialEdicionesBorrador> _historiales = new();

    public HistorialEdicionesBorrador Obtener(int pedidoId) => _historiales.GetOrAdd(pedidoId, _ => new HistorialEdicionesBorrador());

    public void Bloquear(int pedidoId) => Obtener(pedidoId).Bloquear();
}
