namespace LogiMarket.Application.Metricas;

/// <summary>Singleton (registrado una sola vez en DI): contadores de sesion con sincronizacion segura.</summary>
public sealed class MetricasSesion
{
    private long _pagados, _despachos, _devoluciones, _rechazos;

    public long PedidosPagados => Interlocked.Read(ref _pagados);
    public long Despachos => Interlocked.Read(ref _despachos);
    public long Devoluciones => Interlocked.Read(ref _devoluciones);
    public long Rechazos => Interlocked.Read(ref _rechazos);

    public void RegistrarPedidoPagado() => Interlocked.Increment(ref _pagados);
    public void RegistrarDespacho() => Interlocked.Increment(ref _despachos);
    public void RegistrarDevolucion() => Interlocked.Increment(ref _devoluciones);
    public void RegistrarRechazo() => Interlocked.Increment(ref _rechazos);
}
