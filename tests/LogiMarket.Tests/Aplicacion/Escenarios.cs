using LogiMarket.Application.Servicios;
using LogiMarket.Domain.Pedidos;

namespace LogiMarket.Tests.Aplicacion;

internal static class Escenarios
{
    /// <summary>Ana compra KIT_TELETRABAJO x1 (59.00) con el rol VENTAS.</summary>
    public static async Task<int> PagarAnaAsync(Entorno e, string clave = "k-ana")
    {
        await e.IniciarSesionAsync("ventas");
        var id = await e.CrearPedidoAsync("Ana", TipoEntrega.Estandar, ("KIT_TELETRABAJO", 1));
        Assert.True((await e.Obtener<ConfirmacionPedidoFacade>().ConfirmarAsync(id, clave)).Exito);
        return id;
    }

    public static async Task<int> EntregarAnaAsync(Entorno e)
    {
        var id = await PagarAnaAsync(e);
        await e.IniciarSesionAsync("bodega");
        Assert.True((await e.Obtener<ServicioDespacho>().DespacharAsync(id)).Exito);
        Assert.True((await e.Obtener<ServicioDespacho>().EntregarAsync(id)).Exito);
        await e.IniciarSesionAsync("ventas");
        return id;
    }
}
