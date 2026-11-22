using LogiMarket.Application.Comandos;
using LogiMarket.Application.Comun;
using LogiMarket.Application.Demo;
using LogiMarket.Application.Eventos;
using LogiMarket.Application.Metricas;
using LogiMarket.Application.Notificaciones;
using LogiMarket.Application.Puertos;
using LogiMarket.Application.Servicios;
using LogiMarket.Application.Validacion;
using LogiMarket.Domain.Auditoria;
using LogiMarket.Domain.Catalogo;
using LogiMarket.Domain.Devoluciones;
using LogiMarket.Domain.Pedidos;
using LogiMarket.Infrastructure.Persistencia;
using LogiMarket.Tests.Dominio;
using Microsoft.EntityFrameworkCore;

namespace LogiMarket.Tests.Aplicacion;

public class PatronesAplicacionTests
{
    private static async Task<int> PagarAsync(Entorno e, string cliente = "Ana", string clave = "k-1")
    {
        await e.IniciarSesionAsync("ventas");
        var id = await e.CrearPedidoAsync(cliente, TipoEntrega.Estandar, ("KIT_TELETRABAJO", 1));
        Assert.True((await e.Obtener<ConfirmacionPedidoFacade>().ConfirmarAsync(id, clave)).Exito);
        return id;
    }

    private static async Task<int> EntregarAsync(Entorno e)
    {
        var id = await PagarAsync(e);
        await e.IniciarSesionAsync("bodega");
        Assert.True((await e.Obtener<ServicioDespacho>().DespacharAsync(id)).Exito);
        Assert.True((await e.Obtener<ServicioDespacho>().EntregarAsync(id)).Exito);
        await e.IniciarSesionAsync("ventas");
        return id;
    }

    private static Task<int> ContarAuditoriaAsync(Entorno e, string accion) =>
        e.Obtener<LogiMarketDbContext>().Auditoria.CountAsync(a => a.Accion == accion);

    [Fact] // P23 Chain
    public void P23_Chain_informa_primero_el_cliente_y_luego_el_stock_en_orden()
    {
        var c = new CatalogoPrueba();
        var pedido = new PedidoBuilder().ParaCliente(c.Ana).ConEntrega(CatalogoPrueba.Estandar)
            .AgregarLinea(c.KitTeletrabajo, 6).Construir(CatalogoPrueba.Ahora);
        var productos = new[] { c.P01, c.P02, c.P03 }.ToDictionary(p => p.Sku);
        var cadena = PipelineValidacion.Crear();

        c.Ana.Desactivar();
        var primero = cadena.Validar(new ContextoValidacion(pedido, productos, true));
        Assert.Equal(EslabonClienteActivo.Nombre, primero.Eslabon);

        c.Ana.Activar(); // corregido el primer error se alcanza el siguiente eslabon
        var segundo = cadena.Validar(new ContextoValidacion(pedido, productos, true));
        Assert.Equal(EslabonStock.Nombre, segundo.Eslabon);

        c.P02.Reponer(1);
        Assert.True(cadena.Validar(new ContextoValidacion(pedido, productos, true)).Valido);
        Assert.Equal(EslabonCondicionesConfirmacion.Nombre, cadena.Validar(new ContextoValidacion(pedido, productos, false)).Eslabon);
    }

    [Fact] // P24 Command
    public async Task P24_Mismo_comando_desde_la_interfaz_y_desde_prueba_con_historial()
    {
        using var e = await Entorno.CrearAsync();
        await e.IniciarSesionAsync("ventas");
        var primero = await e.CrearPedidoAsync("Ana", TipoEntrega.Estandar, ("P01", 1));
        var segundo = await e.CrearPedidoAsync("Luis", TipoEntrega.Estandar, ("P05", 1));
        var facade = e.Obtener<ConfirmacionPedidoFacade>();
        var invocador = e.Obtener<InvocadorComandos>();

        var viaBoton = await invocador.EjecutarAsync(new ConfirmarPedidoCommand(facade, primero, "k-boton"));
        var viaPrueba = await new ConfirmarPedidoCommand(facade, segundo, "k-prueba").EjecutarAsync();

        Assert.True(viaBoton.Exito);
        Assert.True(viaPrueba.Exito);
        var registro = Assert.Single(invocador.Historial);
        Assert.Equal((nameof(ConfirmarPedidoCommand), "ventas"), (registro.Comando, registro.Usuario));
        Assert.Contains($"pedido={primero}", registro.Entrada);
        Assert.Equal(1, await ContarAuditoriaAsync(e, "COMANDO_ConfirmarPedidoCommand"));
    }

    [Fact] // P30 Observer + Outbox
    public async Task P30_Outbox_notifica_a_tres_suscriptores_una_sola_vez()
    {
        using var e = await Entorno.CrearAsync();
        await PagarAsync(e);

        Assert.Equal(1, await e.Obtener<ProcesadorOutbox>().ProcesarPendientesAsync());
        Assert.Equal(0, await e.Obtener<ProcesadorOutbox>().ProcesarPendientesAsync()); // sin duplicar

        Assert.Equal(1, await ContarAuditoriaAsync(e, "NOTIFICACION_EMAIL"));
        Assert.Equal(1, await ContarAuditoriaAsync(e, "EVENTO_PedidoPagado"));
        Assert.Equal(1, e.Obtener<MetricasSesion>().PedidosPagados);
    }

    [Fact] // P11 Factory Method (seleccion fuera de formularios)
    public async Task P11_Factory_Method_cambia_de_EMAIL_a_SMS_sin_tocar_el_consumidor()
    {
        using var e = await Entorno.CrearAsync();
        await PagarAsync(e);
        e.Obtener<ConfiguracionNotificacion>().Canal = "SMS";

        await e.Obtener<ProcesadorOutbox>().ProcesarPendientesAsync();

        Assert.Equal(1, await ContarAuditoriaAsync(e, "NOTIFICACION_SMS"));
        Assert.Equal(0, await ContarAuditoriaAsync(e, "NOTIFICACION_EMAIL"));
    }

    [Fact] // P37 Outbox: falla el notificador y se recupera tras reiniciar
    public async Task P37_Evento_fallido_queda_pendiente_y_se_procesa_una_vez_tras_reiniciar()
    {
        using var e = await Entorno.CrearAsync();
        await PagarAsync(e);
        e.Obtener<ConfiguracionDemo>().FallarNotificador = true;

        Assert.Equal(0, await e.Obtener<ProcesadorOutbox>().ProcesarPendientesAsync());
        var db = e.Obtener<LogiMarketDbContext>();
        var evento = await db.Outbox.AsNoTracking().SingleAsync();
        Assert.Null(evento.ProcesadoEn);
        Assert.Equal(1, evento.Intentos);
        Assert.Contains("no esta disponible", evento.UltimoError);
        Assert.Equal(0, await ContarAuditoriaAsync(e, "EVENTO_PedidoPagado")); // nada parcial
        Assert.Equal(0, e.Obtener<MetricasSesion>().PedidosPagados);

        using var reiniciada = e.AbrirOtraSesion(); // nuevo procesador, notificador ya disponible
        Assert.Equal(1, await reiniciada.Obtener<ProcesadorOutbox>().ProcesarPendientesAsync());
        Assert.Equal(0, await reiniciada.Obtener<ProcesadorOutbox>().ProcesarPendientesAsync());
        Assert.Equal(1, await ContarAuditoriaAsync(reiniciada, "EVENTO_PedidoPagado"));
        Assert.Equal(1, await reiniciada.Obtener<LogiMarketDbContext>().Pagos.CountAsync()); // sin duplicar cobro
        Assert.NotNull((await reiniciada.Obtener<LogiMarketDbContext>().Outbox.SingleAsync()).ProcesadoEn);
    }

    [Fact] // P35 + Template Method (variante DEFECTO)
    public async Task P35_Devolucion_de_Ana_reembolsa_54_una_sola_vez_y_reintegra_stock_una_vez()
    {
        using var e = await Entorno.CrearAsync();
        var id = await EntregarAsync(e);
        var servicio = e.Obtener<ServicioDevoluciones>();

        var primera = await servicio.SolicitarAsync(new SolicitudDevolucion(id, MotivoDevolucion.Defecto, false));
        var repetida = await servicio.SolicitarAsync(new SolicitudDevolucion(id, MotivoDevolucion.Defecto, false));

        Assert.True(primera.Exito, primera.Mensaje);
        Assert.Equal(54.00m, primera.Reembolso);
        Assert.False(repetida.Exito);
        Assert.Equal("YA_DEVUELTO", repetida.Codigo);
        var stock = await e.StockAsync();
        Assert.Equal((8, 5, 20), (stock["P01"], stock["P02"], stock["P03"]));
        var db = e.Obtener<LogiMarketDbContext>();
        Assert.Equal(1, await db.Devoluciones.CountAsync());
        Assert.Equal(3, await db.Movimientos.CountAsync(m => m.Referencia == $"DEVOLUCION-{id}"));
    }

    [Fact] // P33 Template Method: plazos
    public async Task P33_Defecto_dia_20_se_acepta_y_arrepentimiento_dia_10_se_rechaza_sin_efectos()
    {
        using var aceptada = await Entorno.CrearAsync();
        var id1 = await EntregarAsync(aceptada);
        aceptada.Reloj.UtcAhora = aceptada.Reloj.UtcAhora.AddDays(20);
        Assert.True((await aceptada.Obtener<ServicioDevoluciones>().SolicitarAsync(new(id1, MotivoDevolucion.Defecto, false))).Exito);

        using var rechazada = await Entorno.CrearAsync();
        var id2 = await EntregarAsync(rechazada);
        rechazada.Reloj.UtcAhora = rechazada.Reloj.UtcAhora.AddDays(10);
        var r = await rechazada.Obtener<ServicioDevoluciones>().SolicitarAsync(new(id2, MotivoDevolucion.Arrepentimiento, true));

        Assert.False(r.Exito);
        Assert.Equal("NO_ELEGIBLE", r.Codigo);
        var stock = await rechazada.StockAsync();
        Assert.Equal((7, 4, 19), (stock["P01"], stock["P02"], stock["P03"])); // sin reintegro
        Assert.Equal(0, await rechazada.Obtener<LogiMarketDbContext>().Devoluciones.CountAsync());
    }

    [Fact] // Template Method: la inspeccion exige producto sin uso en arrepentimiento
    public async Task Arrepentimiento_con_producto_usado_se_rechaza_aunque_este_en_plazo()
    {
        using var e = await Entorno.CrearAsync();
        var id = await EntregarAsync(e);
        var r = await e.Obtener<ServicioDevoluciones>().SolicitarAsync(new(id, MotivoDevolucion.Arrepentimiento, SinUso: false));
        Assert.Equal("CONDICION_NO_CUMPLIDA", r.Codigo);
        Assert.Equal(7, (await e.StockAsync())["P01"]);
    }

    [Fact] // Autorizacion en Application (no solo botones)
    public async Task CONSULTA_no_puede_confirmar_y_queda_auditoria_de_seguridad()
    {
        using var e = await Entorno.CrearAsync();
        await e.IniciarSesionAsync("ventas");
        var id = await e.CrearPedidoAsync("Ana", TipoEntrega.Estandar, ("P01", 1));
        await e.IniciarSesionAsync("consulta");

        await Assert.ThrowsAsync<AccesoDenegadoException>(() => e.Obtener<ConfirmacionPedidoFacade>().ConfirmarAsync(id, "k-x"));

        Assert.Equal(1, await ContarAuditoriaAsync(e, "ACCESO_DENEGADO"));
        Assert.Equal(0, await e.Obtener<LogiMarketDbContext>().Pagos.CountAsync());
        Assert.Equal(8, (await e.StockAsync())["P01"]);
    }

    [Fact]
    public async Task Bodega_no_puede_crear_pedidos_ni_ventas_despachar()
    {
        using var e = await Entorno.CrearAsync();
        await e.IniciarSesionAsync("bodega");
        await Assert.ThrowsAsync<AccesoDenegadoException>(() => e.CrearPedidoAsync("Ana", TipoEntrega.Estandar, ("P01", 1)));
        await e.IniciarSesionAsync("ventas");
        await Assert.ThrowsAsync<AccesoDenegadoException>(() => e.Obtener<ServicioDespacho>().DespacharAsync(1));
    }

    [Fact]
    public async Task Login_fallido_se_audita_y_no_inicia_sesion()
    {
        using var e = await Entorno.CrearAsync();
        var auth = e.Obtener<LogiMarket.Application.Seguridad.ServicioAutenticacion>();

        Assert.False(await auth.IniciarSesionAsync("admin", "incorrecta"));
        Assert.False(e.Obtener<LogiMarket.Application.Seguridad.ISesionUsuario>().Autenticado);
        Assert.Equal(1, await ContarAuditoriaAsync(e, "LOGIN_FALLIDO"));
    }

    [Fact] // P12 Abstract Factory
    public void P12_Abstract_Factory_familias_estandar_y_expres()
    {
        var servicio = new LogiMarket.Application.Entrega.ServicioEntrega(new LogiMarket.Application.Entrega.IFabricaEntrega[]
        {
            new LogiMarket.Application.Entrega.FabricaEntregaEstandar(), new LogiMarket.Application.Entrega.FabricaEntregaExpres()
        });
        var estandar = servicio.CrearModalidad(TipoEntrega.Estandar);
        var expres = servicio.CrearModalidad(TipoEntrega.Expres);

        Assert.Equal((5.00m, 3), (estandar.Tarifa, estandar.PlazoDias));
        Assert.Equal((12.00m, 1), (expres.Tarifa, expres.PlazoDias));
        Assert.StartsWith("STD-", servicio.GenerarGuia(TipoEntrega.Estandar, 7));
        Assert.StartsWith("EXP-", servicio.GenerarGuia(TipoEntrega.Expres, 7));
    }
}