using LogiMarket.Application.Consultas;
using LogiMarket.Application.Coordinacion;
using LogiMarket.Application.Pedidos;
using LogiMarket.Application.Puertos;
using LogiMarket.Application.Servicios;
using LogiMarket.Domain.Common;
using LogiMarket.Domain.Pedidos;
using LogiMarket.Domain.Politicas;
using LogiMarket.Infrastructure.Persistencia;
using LogiMarket.Tests.Dominio;
using Microsoft.EntityFrameworkCore;
using static LogiMarket.Tests.Dominio.CatalogoPrueba;

namespace LogiMarket.Tests.Aplicacion;

public class ComportamentalesTests
{
    // ---------- P25 / P26 Interpreter ----------
    private static readonly IReadOnlyList<PedidoResumenDto> Datos = new[]
    {
        new PedidoResumenDto(1, "Ana", "Pagado", 59.00m, Ahora),
        new PedidoResumenDto(2, "Luis", "Pagado", 10.00m, Ahora),
        new PedidoResumenDto(3, "Carmen", "Borrador", 90.00m, Ahora),
        new PedidoResumenDto(4, "Ana", "Borrador", 20.00m, Ahora),
        new PedidoResumenDto(5, "Luis", "Cancelado", 80.00m, Ahora)
    };

    private static int[] Filtrar(string consulta) =>
        Datos.Where(ParserConsultas.Analizar(consulta).Evaluar).Select(p => p.Id).ToArray();

    [Fact] // P25
    public async Task P25_Interpreter_ESTADO_PAGADO_AND_TOTAL_mayor_igual_50_devuelve_solo_los_que_cumplen_en_la_base()
    {
        using var e = await Entorno.CrearAsync();
        var ana = await Escenarios.PagarAnaAsync(e); // Pagado 59.00
        var luis = await e.CrearPedidoAsync("Luis", Domain.Pedidos.TipoEntrega.Estandar, ("P05", 1));
        Assert.True((await e.Obtener<ConfirmacionPedidoFacade>().ConfirmarAsync(luis, "k-luis")).Exito); // Pagado 10.00
        var carmen = await e.CrearPedidoAsync("Carmen", Domain.Pedidos.TipoEntrega.Estandar, ("P03", 10)); // Borrador 90.00

        var r = await e.Obtener<ConsultasPedidos>().FiltrarAsync("ESTADO = PAGADO AND TOTAL >= 50");

        Assert.Equal(new[] { ana }, r.Select(p => p.Id));
        Assert.DoesNotContain(r, p => p.Id == luis || p.Id == carmen);
    }

    [Fact] // P25: CLIENTE y combinaciones
    public void P25_Interpreter_filtra_por_cliente_estado_y_total()
    {
        Assert.Equal(new[] { 1, 4 }, Filtrar("CLIENTE = Ana"));
        Assert.Equal(new[] { 1 }, Filtrar("CLIENTE = ana AND ESTADO = Pagado"));
        Assert.Equal(new[] { 1, 3, 5 }, Filtrar("TOTAL >= 59"));
        Assert.Equal(new[] { 5 }, Filtrar("ESTADO = CANCELADO"));
        Assert.Equal(new[] { 3 }, Filtrar("CLIENTE = \"Carmen\" AND TOTAL >= 90.00"));
    }

    [Fact] // P26: precedencia AND > OR y parentesis
    public void P26_Interpreter_respeta_precedencia_y_parentesis()
    {
        // AND liga mas fuerte: PAGADO  OR  (BORRADOR AND TOTAL>=80)
        Assert.Equal(new[] { 1, 2, 3 }, Filtrar("ESTADO = PAGADO OR ESTADO = BORRADOR AND TOTAL >= 80"));
        // Con parentesis: (PAGADO OR BORRADOR) AND TOTAL>=80
        Assert.Equal(new[] { 3 }, Filtrar("(ESTADO = PAGADO OR ESTADO = BORRADOR) AND TOTAL >= 80"));
        Assert.Equal(new[] { 1, 2, 3, 4 }, Filtrar("((ESTADO = PAGADO) OR (ESTADO = BORRADOR))"));
    }

    [Theory] // P26: errores legibles con posicion
    [InlineData("", "vacia")]
    [InlineData("ESTADO PAGADO", "'=' despues de ESTADO")]
    [InlineData("TOTAL >= ", "un numero")]
    [InlineData("TOTAL >= abc", "TOTAL requiere un numero")]
    [InlineData("TOTAL > 5", "solo se admite '>='")]
    [InlineData("ESTADO = INEXISTENTE", "Estado desconocido")]
    [InlineData("PRECIO = 5", "Campo desconocido")]
    [InlineData("(ESTADO = PAGADO", "Falta cerrar el parentesis")]
    [InlineData("ESTADO = PAGADO)", "Se esperaba AND, OR")]
    [InlineData("ESTADO = PAGADO AND", "se encontro 'el fin de la consulta'")]
    [InlineData("CLIENTE = 'Ana'", "Caracter no valido")]
    [InlineData("CLIENTE = \"Ana", "comillas")]
    public void P26_Interpreter_rechaza_sintaxis_invalida_con_mensaje_legible(string consulta, string fragmento)
    {
        var ex = Assert.Throws<ErrorSintaxisConsultaException>(() => ParserConsultas.Analizar(consulta));
        Assert.Contains(fragmento, ex.Message);
        Assert.Contains("posicion", ex.Message);
    }

    [Fact] // P26: sin SQL concatenado
    public async Task P26_Interpreter_un_intento_de_inyeccion_es_texto_literal_y_no_se_ejecuta_como_SQL()
    {
        using var e = await Entorno.CrearAsync();
        await Escenarios.PagarAnaAsync(e);
        var consultas = e.Obtener<ConsultasPedidos>();

        var r = await consultas.FiltrarAsync("CLIENTE = \"x' OR '1'='1\"");
        Assert.Empty(r);
        Assert.Throws<ErrorSintaxisConsultaException>(() => ParserConsultas.Analizar("CLIENTE = Ana'; DROP TABLE Pedidos;--"));
        await Assert.ThrowsAsync<ErrorSintaxisConsultaException>(() => consultas.FiltrarAsync("ESTADO ="));
        Assert.Equal(1, await e.Obtener<LogiMarketDbContext>().Pedidos.CountAsync()); // la tabla sigue intacta
    }

    // ---------- P29 Memento ----------
    private static Pedido CrearBorrador(CatalogoPrueba c) =>
        new PedidoBuilder().ParaCliente(c.Ana).ConEntrega(Estandar).AgregarLinea(c.KitTeletrabajo, 1).Construir(Ahora);

    private static string Huella(Pedido p) =>
        $"{p.Subtotal}|{p.Descuento}|{p.Total}|" + string.Join(";", p.Detalles.Select(d =>
            $"{d.Sku}x{d.Cantidad}[{string.Join(",", d.Componentes.Select(k => $"{k.Sku}:{k.CantidadPorUnidad}"))}]"));

    [Fact] // P29
    public void P29_Memento_tres_ediciones_dos_deshacer_y_un_rehacer_restauran_borradores_exactos()
    {
        var c = new CatalogoPrueba();
        var pedido = CrearBorrador(c);
        var calc = CalculadoraDescuento.Predeterminada();
        var historial = new HistorialEdicionesBorrador();
        var s0 = Huella(pedido);

        historial.Registrar(pedido); pedido.CambiarCantidad("KIT_TELETRABAJO", 2, calc); var s1 = Huella(pedido);
        historial.Registrar(pedido); pedido.AgregarLinea(c.P05, 3, calc); var s2 = Huella(pedido);
        historial.Registrar(pedido); pedido.CambiarCantidad("KIT_TELETRABAJO", 3, calc); var s3 = Huella(pedido);
        Assert.NotEqual(s2, s3);

        historial.Deshacer(pedido);
        Assert.Equal(s2, Huella(pedido));
        historial.Deshacer(pedido);
        Assert.Equal(s1, Huella(pedido)); // valores exactos, incluidos los items anidados del kit
        Assert.Equal(2, pedido.Detalles.Single().Cantidad); // s1: solo el kit, con cantidad 2

        historial.Rehacer(pedido);
        Assert.Equal(s2, Huella(pedido));
        Assert.Equal((2, 1), (historial.PasosDeshacer, historial.PasosRehacer));
        historial.Deshacer(pedido); historial.Deshacer(pedido);
        Assert.Equal(s0, Huella(pedido));
        Assert.Throws<DomainException>(() => historial.Deshacer(pedido)); // nada mas que deshacer
    }

    [Fact]
    public void P29_Memento_conserva_como_maximo_cinco_estados_y_una_edicion_nueva_invalida_el_rehacer()
    {
        var c = new CatalogoPrueba();
        var pedido = CrearBorrador(c);
        var calc = CalculadoraDescuento.Predeterminada();
        var historial = new HistorialEdicionesBorrador();

        for (var cantidad = 2; cantidad <= 8; cantidad++)
        {
            historial.Registrar(pedido);
            pedido.CambiarCantidad("KIT_TELETRABAJO", cantidad, calc);
        }
        Assert.Equal(5, historial.PasosDeshacer);

        historial.Deshacer(pedido);
        Assert.Equal(1, historial.PasosRehacer);
        historial.Registrar(pedido); // nueva edicion
        Assert.Equal(0, historial.PasosRehacer);
    }

    [Fact] // P29: no se deshace un pago
    public async Task P29_Memento_se_bloquea_al_confirmar_y_no_revierte_pago_ni_stock()
    {
        using var e = await Entorno.CrearAsync();
        await e.IniciarSesionAsync("ventas");
        var id = await e.CrearPedidoAsync("Ana", Domain.Pedidos.TipoEntrega.Estandar, ("KIT_TELETRABAJO", 1));
        var edicion = e.Obtener<ServicioEdicionBorrador>();

        Assert.True((await edicion.AgregarLineaAsync(id, "P05", 2)).Exito);
        Assert.True((await edicion.CambiarCantidadAsync(id, "KIT_TELETRABAJO", 2)).Exito);
        Assert.True((await edicion.DeshacerAsync(id)).Exito);
        using (var otra = e.AbrirOtraSesion()) // el deshacer se guardo en la base
        {
            var p = (await otra.Obtener<IRepositorioPedidos>().ObtenerPorIdAsync(id))!;
            Assert.Equal(new[] { "KIT_TELETRABAJO", "P05" }, p.Detalles.Select(d => d.Sku));
            Assert.Equal(1, p.Detalles[0].Cantidad);
        }

        Assert.True((await e.Obtener<ConfirmacionPedidoFacade>().ConfirmarAsync(id, "k-memento")).Exito);
        var historial = e.Obtener<RegistroHistorialesBorrador>().Obtener(id);
        Assert.True(historial.Bloqueado);
        Assert.Equal(0, historial.PasosDeshacer);

        var deshacer = await edicion.DeshacerAsync(id);
        Assert.False(deshacer.Exito);
        var edicionTardia = await edicion.AgregarLineaAsync(id, "P01", 1);
        Assert.False(edicionTardia.Exito);

        using var final = e.AbrirOtraSesion();
        var pagado = (await final.Obtener<IRepositorioPedidos>().ObtenerPorIdAsync(id))!;
        Assert.Equal(EstadoPedido.Pagado, pagado.Estado);
        Assert.Equal(1, await final.Obtener<LogiMarketDbContext>().Pagos.CountAsync());
        Assert.Equal(7, (await final.StockAsync())["P01"]);
    }

    // ---------- P28 Mediator ----------
    [Fact]
    public async Task P28_Mediator_al_confirmar_refresca_catalogo_pedidos_y_metricas_sin_acoplamiento_entre_vistas()
    {
        var mediador = new FormularioPrincipalMediator();
        var catalogo = new VistaFalsa(TipoVista.Catalogo);
        var pedidos = new VistaFalsa(TipoVista.Pedidos);
        var metricas = new VistaFalsa(TipoVista.Metricas);
        var carrito = new VistaFalsa(TipoVista.Carrito);
        foreach (var v in new[] { catalogo, pedidos, metricas, carrito }) mediador.Registrar(v);

        await mediador.NotificarAsync(TipoVista.Carrito, OperacionUI.PedidoConfirmado);

        Assert.Equal((1, 1, 1), (catalogo.Refrescos, pedidos.Refrescos, metricas.Refrescos));
        Assert.Equal(0, carrito.Refrescos); // la vista de origen no se refresca a si misma
    }

    [Fact]
    public async Task P28_Mediator_solo_refresca_las_vistas_afectadas_y_admite_desregistrar()
    {
        var mediador = new FormularioPrincipalMediator();
        var catalogo = new VistaFalsa(TipoVista.Catalogo);
        var pedidos = new VistaFalsa(TipoVista.Pedidos);
        var carrito = new VistaFalsa(TipoVista.Carrito);
        foreach (var v in new[] { catalogo, pedidos, carrito }) mediador.Registrar(v);

        await mediador.NotificarAsync(TipoVista.Carrito, OperacionUI.BorradorEditado);
        Assert.Equal((0, 0, 0), (catalogo.Refrescos, pedidos.Refrescos, carrito.Refrescos)); // solo afecta al carrito (origen)

        await mediador.NotificarAsync(TipoVista.Pedidos, OperacionUI.PedidoDespachado);
        Assert.Equal((0, 0, 0), (catalogo.Refrescos, pedidos.Refrescos, carrito.Refrescos));

        mediador.Desregistrar(catalogo);
        await mediador.NotificarAsync(TipoVista.Carrito, OperacionUI.DevolucionProcesada);
        Assert.Equal((0, 1), (catalogo.Refrescos, pedidos.Refrescos));
    }

    [Fact]
    public async Task P28_Mediator_integrado_las_tres_vistas_ven_el_stock_y_los_pedidos_tras_confirmar()
    {
        using var e = await Entorno.CrearAsync();
        var mediador = e.Obtener<IMediadorVistas>();
        var catalogo = new VistaConDatos(TipoVista.Catalogo, async () => (await e.StockAsync())["P01"].ToString());
        var pedidos = new VistaConDatos(TipoVista.Pedidos, async () => (await e.Obtener<ConsultasPedidos>().ListarAsync()).Count.ToString());
        var metricas = new VistaConDatos(TipoVista.Metricas, () => Task.FromResult("m"));
        foreach (var v in new[] { catalogo, pedidos, metricas }) mediador.Registrar(v);
        try
        {
            await Escenarios.PagarAnaAsync(e);
            await mediador.NotificarAsync(TipoVista.Carrito, OperacionUI.PedidoConfirmado);

            Assert.Equal(("7", "1", "m"), (catalogo.Ultimo, pedidos.Ultimo, metricas.Ultimo));
        }
        finally
        {
            foreach (var v in new[] { catalogo, pedidos, metricas }) mediador.Desregistrar(v);
        }
    }

    private sealed class VistaFalsa(TipoVista tipo) : IVistaActualizable
    {
        public TipoVista Tipo => tipo;
        public int Refrescos { get; private set; }

        public Task RefrescarAsync(CancellationToken ct = default)
        {
            Refrescos++;
            return Task.CompletedTask;
        }
    }

    private sealed class VistaConDatos(TipoVista tipo, Func<Task<string>> leer) : IVistaActualizable
    {
        public TipoVista Tipo => tipo;
        public string? Ultimo { get; private set; }

        public async Task RefrescarAsync(CancellationToken ct = default) => Ultimo = await leer();
    }
}
