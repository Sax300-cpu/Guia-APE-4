using LogiMarket.Application.Demo;
using LogiMarket.Application.Puertos;
using LogiMarket.Application.Servicios;
using LogiMarket.Application.Validacion;
using LogiMarket.Domain.Pedidos;
using LogiMarket.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LogiMarket.Tests.Aplicacion;

public class FlujoVentasTests(Xunit.Abstractions.ITestOutputHelper salida)
{
    private static async Task<(Entorno Entorno, ResultadoConfirmacion Resultado, int PedidoId)> ConfirmarAsync(
        string cliente, TipoEntrega tipo, params (string, int)[] lineas)
    {
        var e = await Entorno.CrearAsync();
        await e.IniciarSesionAsync("ventas");
        var id = await e.CrearPedidoAsync(cliente, tipo, lineas);
        return (e, await e.Obtener<ConfirmacionPedidoFacade>().ConfirmarAsync(id, $"clave-{id}"), id);
    }

    private static async Task<(int Pagos, int Movimientos, int Eventos)> EfectosAsync(Entorno e)
    {
        var db = e.Obtener<LogiMarketDbContext>();
        return (await db.Pagos.CountAsync(), await db.Movimientos.CountAsync(m => m.Referencia.StartsWith("PEDIDO-")), await db.Outbox.CountAsync());
    }

    [Fact] // P02
    public async Task P02_Ana_VIP_kit_teletrabajo_paga_59_y_descuenta_stock()
    {
        var (e, r, id) = await ConfirmarAsync("Ana", TipoEntrega.Estandar, ("KIT_TELETRABAJO", 1));
        using var _ = e;
        Assert.True(r.Exito, r.Mensaje);
        Assert.Equal(59.00m, r.Total);
        var stock = await e.StockAsync();
        Assert.Equal((7, 4, 19), (stock["P01"], stock["P02"], stock["P03"]));
        var pedido = (await e.Obtener<IRepositorioPedidos>().ObtenerPorIdAsync(id))!;
        Assert.Equal(EstadoPedido.Pagado, pedido.Estado);
        Assert.Equal((1, 3, 1), await EfectosAsync(e));
    }

    [Fact] // P03
    public async Task P03_Luis_NORMAL_expres_paga_102()
    {
        var (e, r, _) = await ConfirmarAsync("Luis", TipoEntrega.Expres, ("P04", 2), ("P05", 2));
        using var _ = e;
        Assert.True(r.Exito, r.Mensaje);
        Assert.Equal(102.00m, r.Total);
        var stock = await e.StockAsync();
        Assert.Equal((2, 28), (stock["P04"], stock["P05"]));
        Assert.Equal(1, (await EfectosAsync(e)).Pagos);
    }

    [Fact] // P04
    public async Task P04_Carmen_mayorista_paga_90_con_descuento_15()
    {
        var (e, r, id) = await ConfirmarAsync("Carmen", TipoEntrega.Estandar, ("P03", 10));
        using var _ = e;
        Assert.True(r.Exito, r.Mensaje);
        Assert.Equal(90.00m, r.Total);
        Assert.Equal(10, (await e.StockAsync())["P03"]);
        Assert.Equal(15.00m, (await e.Obtener<IRepositorioPedidos>().ObtenerPorIdAsync(id))!.Descuento);
    }

    [Fact] // P05
    public async Task P05_Kit_x6_se_rechaza_por_stock_sin_efectos()
    {
        var (e, r, id) = await ConfirmarAsync("Ana", TipoEntrega.Estandar, ("KIT_TELETRABAJO", 6));
        using var _ = e;
        Assert.False(r.Exito);
        Assert.Equal(EslabonStock.Nombre, r.Eslabon);
        Assert.Contains("P02", r.Mensaje);
        var stock = await e.StockAsync();
        Assert.Equal((8, 5, 20), (stock["P01"], stock["P02"], stock["P03"]));
        Assert.Equal((0, 0, 0), await EfectosAsync(e));
        Assert.Equal(EstadoPedido.Borrador, (await e.Obtener<IRepositorioPedidos>().ObtenerPorIdAsync(id))!.Estado);
    }

    [Fact] // P06
    public async Task P06_Error_de_cobro_conserva_inventario_y_estado()
    {
        using var e = await Entorno.CrearAsync();
        await e.IniciarSesionAsync("ventas");
        var id = await e.CrearPedidoAsync("Ana", TipoEntrega.Estandar, ("KIT_TELETRABAJO", 1));
        e.Obtener<ConfiguracionDemo>().FallarCobro = true;

        var r = await e.Obtener<ConfirmacionPedidoFacade>().ConfirmarAsync(id, "k-1");

        Assert.False(r.Exito);
        Assert.Equal("COBRO_RECHAZADO", r.Codigo);
        using var otra = e.AbrirOtraSesion();
        var stock = await otra.StockAsync();
        Assert.Equal((8, 5, 20), (stock["P01"], stock["P02"], stock["P03"]));
        Assert.Equal((0, 0, 0), await EfectosAsync(otra));
        Assert.Equal(EstadoPedido.Borrador, (await otra.Obtener<IRepositorioPedidos>().ObtenerPorIdAsync(id))!.Estado);
    }

    [Fact] // P07
    public async Task P07_Misma_clave_no_cobra_dos_veces()
    {
        var (e, primero, id) = await ConfirmarAsync("Ana", TipoEntrega.Estandar, ("KIT_TELETRABAJO", 1));
        using var _ = e;
        var segundo = await e.Obtener<ConfirmacionPedidoFacade>().ConfirmarAsync(id, $"clave-{id}");

        Assert.True(primero.Exito);
        Assert.True(segundo.Exito);
        Assert.True(segundo.EsRepeticion);
        Assert.Equal(primero.Total, segundo.Total);
        Assert.Equal((1, 3, 1), await EfectosAsync(e));
        Assert.Equal(7, (await e.StockAsync())["P01"]);
    }

    [Fact] // P08
    public async Task P08_Pedido_pagado_no_se_cancela_ni_se_edita()
    {
        var (e, _, id) = await ConfirmarAsync("Ana", TipoEntrega.Estandar, ("KIT_TELETRABAJO", 1));
        using var _ = e;
        var servicio = e.Obtener<ServicioPedidos>();

        Assert.False((await servicio.CancelarAsync(id)).Exito);
        Assert.False((await e.Obtener<ServicioEdicionBorrador>().CambiarCantidadAsync(id, "KIT_TELETRABAJO", 5)).Exito);

        using var otra = e.AbrirOtraSesion();
        var pedido = (await otra.Obtener<IRepositorioPedidos>().ObtenerPorIdAsync(id))!;
        Assert.Equal((EstadoPedido.Pagado, 59.00m, 1), (pedido.Estado, pedido.Total, pedido.Detalles[0].Cantidad));
        Assert.Equal(3, pedido.Historial.Count); // Borrador, Validado, Pagado
    }

    [Fact] // P09
    public async Task P09_Bodega_despacha_y_entrega_en_secuencia_y_no_despacha_sin_pago()
    {
        using var e = await Entorno.CrearAsync();
        await e.IniciarSesionAsync("ventas");
        var sinPago = await e.CrearPedidoAsync("Luis", TipoEntrega.Estandar, ("P05", 1));
        var conPago = await e.CrearPedidoAsync("Ana", TipoEntrega.Expres, ("P01", 1));
        Assert.True((await e.Obtener<ConfirmacionPedidoFacade>().ConfirmarAsync(conPago, "k-pago")).Exito);

        await e.IniciarSesionAsync("bodega");
        var despacho = e.Obtener<ServicioDespacho>();
        var rechazo = await despacho.DespacharAsync(sinPago);
        Assert.False(rechazo.Exito);
        Assert.Equal(EstadoPedido.Borrador, (await e.Obtener<IRepositorioPedidos>().ObtenerPorIdAsync(sinPago))!.Estado);

        Assert.True((await despacho.DespacharAsync(conPago)).Exito);
        Assert.False((await despacho.DespacharAsync(conPago)).Exito); // repetido
        Assert.True((await despacho.EntregarAsync(conPago)).Exito);
        Assert.Equal(EstadoPedido.Entregado, (await e.Obtener<IRepositorioPedidos>().ObtenerPorIdAsync(conPago))!.Estado);
        Assert.StartsWith("EXP-", (await e.Obtener<IRepositorioDespachos>().ObtenerPorPedidoAsync(conPago))!.Guia);
    }

    [Fact] // P10 (reinicio, flujo completo de aplicacion)
    public async Task P10_Estado_y_stock_persisten_tras_reiniciar_la_aplicacion()
    {
        var (e, _, id) = await ConfirmarAsync("Ana", TipoEntrega.Estandar, ("KIT_TELETRABAJO", 1));
        using var _ = e;
        using var reiniciada = e.AbrirOtraSesion();
        var pedido = (await reiniciada.Obtener<IRepositorioPedidos>().ObtenerPorIdAsync(id))!;
        Assert.Equal(EstadoPedido.Pagado, pedido.Estado);
        Assert.Equal(7, (await reiniciada.StockAsync())["P01"]);
        Assert.Equal((1, 3, 1), await EfectosAsync(reiniciada));
        Assert.NotNull(await reiniciada.Obtener<IRepositorioPagos>().ObtenerPorClaveAsync($"clave-{id}"));
    }

    [Fact] // P20
    public async Task P20_Fallo_de_un_colaborador_no_deja_persistencia_parcial()
    {
        using var e = await Entorno.CrearAsync(s => s.AddScoped<IPasarelaPago, PasarelaQueExplota>());
        await e.IniciarSesionAsync("ventas");
        var id = await e.CrearPedidoAsync("Ana", TipoEntrega.Estandar, ("KIT_TELETRABAJO", 1));

        await Assert.ThrowsAsync<InvalidOperationException>(() => e.Obtener<ConfirmacionPedidoFacade>().ConfirmarAsync(id, "k-boom"));

        using var otra = e.AbrirOtraSesion();
        var stock = await otra.StockAsync();
        Assert.Equal((8, 5, 20), (stock["P01"], stock["P02"], stock["P03"]));
        Assert.Equal((0, 0, 0), await EfectosAsync(otra));
        Assert.Equal(EstadoPedido.Borrador, (await otra.Obtener<IRepositorioPedidos>().ObtenerPorIdAsync(id))!.Estado);
    }

    [Fact] // P36
    public async Task P36_Dos_cobros_concurrentes_sobre_stock_1_aprueban_como_maximo_uno()
    {
        using var a = await Entorno.CrearAsync();
        await a.IniciarSesionAsync("ventas");
        var db = a.Obtener<LogiMarketDbContext>();
        var p01 = await db.Elementos.OfType<LogiMarket.Domain.Catalogo.ProductoSimple>().SingleAsync(p => p.Sku == "P01");
        p01.Descontar(7); // stock configurado en una unidad
        await db.SaveChangesAsync();
        var pedidoA = await a.CrearPedidoAsync("Ana", TipoEntrega.Estandar, ("P01", 1));
        var pedidoB = await a.CrearPedidoAsync("Luis", TipoEntrega.Estandar, ("P01", 1));

        using var b = a.AbrirOtraSesion();
        await b.IniciarSesionAsync("ventas");
        var resultados = await Task.WhenAll(
            Task.Run(() => a.Obtener<ConfirmacionPedidoFacade>().ConfirmarAsync(pedidoA, "k-a")),
            Task.Run(() => b.Obtener<ConfirmacionPedidoFacade>().ConfirmarAsync(pedidoB, "k-b")));

        Assert.Equal(1, resultados.Count(r => r.Exito));
        using var c = a.AbrirOtraSesion();
        var stockFinal = (await c.StockAsync())["P01"];
        var pagos = (await EfectosAsync(c)).Pagos;
        salida.WriteLine($"P36: cobros aprobados={resultados.Count(r => r.Exito)} de {resultados.Length}, rechazos={string.Join(",", resultados.Where(r => !r.Exito).Select(r => r.Codigo))}, stock final P01={stockFinal}, pagos aprobados={pagos}");
        Assert.Equal(0, stockFinal);
        Assert.Equal(1, pagos);
    }

    private sealed class PasarelaQueExplota : IPasarelaPago
    {
        public string Proveedor => "EXPLOTA";
        public Task<ResultadoCobro> CobrarAsync(decimal monto, string claveIdempotencia, CancellationToken ct = default) =>
            throw new InvalidOperationException("Fallo del colaborador de cobro.");
    }
}