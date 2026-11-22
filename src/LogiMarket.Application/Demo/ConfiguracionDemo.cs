namespace LogiMarket.Application.Demo;

/// <summary>Interruptores de la pantalla de demostracion; solo afectan a los colaboradores simulados.</summary>
public sealed class ConfiguracionDemo
{
    private volatile bool _fallarCobro;
    private volatile bool _fallarNotificador;
    private volatile string _proveedorPago = "SIMULADA";

    public bool FallarCobro { get => _fallarCobro; set => _fallarCobro = value; }
    public bool FallarNotificador { get => _fallarNotificador; set => _fallarNotificador = value; }

    /// <summary>SIMULADA o LEGADA: proveedor de cobro sustituible desde la pantalla de demostracion.</summary>
    public string ProveedorPago { get => _proveedorPago; set => _proveedorPago = value.ToUpperInvariant(); }
}

/// <summary>Canal activo de notificacion (EMAIL o SMS); se cambia fuera de los formularios.</summary>
public sealed class ConfiguracionNotificacion
{
    private volatile string _canal = "EMAIL";
    public string Canal { get => _canal; set => _canal = value.ToUpperInvariant(); }
}