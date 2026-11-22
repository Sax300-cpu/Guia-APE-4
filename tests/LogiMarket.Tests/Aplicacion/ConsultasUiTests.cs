using LogiMarket.Application.Comun;
using LogiMarket.Application.Consultas;
using LogiMarket.Application.Puertos;
using LogiMarket.Application.Servicios;
using LogiMarket.Domain.Clientes;
using LogiMarket.Domain.Common;
using LogiMarket.Domain.Pedidos;
using LogiMarket.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace LogiMarket.Tests.Aplicacion;

/// <summary>Casos de uso que consume la interfaz WinForms (consultas, CRUD, plantillas, auditoria).</summary>
public class ConsultasUiTests
{
    [Fact]
    public async Task Catalogo_lista_filas_con_composicion_de_kits_y_stock_de_productos()
    {
        using var e = await Entorno.CrearAsync();
        await e.IniciarSesionAsync("consulta");

        var filas = await e.Obtener<ConsultasCatalogo>().ListarAsync(null);

        Assert.Equal(7, filas.Count);
        var kit = filas.Single(f => f.Sku == "KIT_TELETRABAJO");
        Assert.Equal(("Kit", 60.00m, null), (kit.Tipo, kit.Precio, kit.Stock));
        Assert.Equal("KIT_OFICINA x1 + P03 x1", kit.Composicion);
        Assert.Equal(8, filas.Single(f => f.Sku == "P01").Stock);
        Assert.Equal(new[] { "P04" }, (await e.Obtener<ConsultasCatalogo>().ListarAsync("webcam")).Select(f => f.Sku));
    }

    [Fact]
    public async Task Solo_ADMIN_gestiona_el_catalogo_y_los_kits_rechazan_ciclos_y_componentes_inexistentes()
    {
        using var e = await Entorno.CrearAsync();
        await e.IniciarSesionAsync("ventas");
        await Assert.ThrowsAsync<AccesoDenegadoException>(() => e.Obtener<ServicioCatalogo>().CrearProductoAsync("P06", "Cable", 3m, 0.1m, 10));

        await e.IniciarSesionAsync("admin");
        var servicio = e.Obtener<ServicioCatalogo>();
        await servicio.CrearProductoAsync("P06", "Cable HDMI", 3.50m, 0.15m, 10);
        await servicio.CrearKitAsync("KIT_GAMER", "Kit Gamer", new[] { ("P06", 2), ("KIT_OFICINA", 1) });

        var filas = await e.Obtener<ConsultasCatalogo>().ListarAsync(null);
        Assert.Equal(57.00m, filas.Single(f => f.Sku == "KIT_GAMER").Precio); // 2 x 3.50 + 50.00
        await Assert.ThrowsAsync<DomainException>(() => servicio.CrearKitAsync("KIT_X", "X", new[] { ("NOEXISTE", 1) }));
        await Assert.ThrowsAsync<DomainException>(() => servicio.CrearKitAsync("KIT_Y", "Y", Array.Empty<(string, int)>()));
        // La carga inicial de un alta deja movimiento trazable
        var mov = await e.Obtener<ConsultasInventario>().MovimientosAsync("P06");
        Assert.Equal(("CargaInicial", 10, "ALTA-P06"), (mov.Single().Motivo, mov.Single().Cantidad, mov.Single().Referencia));
    }

    [Fact]
    public async Task Eliminar_solo_funciona_sin_historial_ni_pertenencia_a_kits()
    {
        using var e = await Entorno.CrearAsync();
        await e.IniciarSesionAsync("admin");
        var servicio = e.Obtener<ServicioCatalogo>();
        await servicio.CrearProductoAsync("P07", "Sin historial", 2m, 0.1m, stock: 0); // sin stock: sin movimientos
        await servicio.CrearProductoAsync("P08", "Con historial", 2m, 0.1m, stock: 4);

        await servicio.EliminarAsync("P07");
        var ex1 = await Assert.ThrowsAsync<DomainException>(() => servicio.EliminarAsync("P08"));
        var ex2 = await Assert.ThrowsAsync<DomainException>(() => servicio.EliminarAsync("P01"));

        Assert.Contains("historial", ex1.Message);
        Assert.Contains("kit", ex2.Message);
        var skus = (await e.Obtener<ConsultasCatalogo>().ListarAsync(null)).Select(f => f.Sku).ToList();
        Assert.DoesNotContain("P07", skus);
        Assert.Contains("P08", skus);
    }

    [Fact]
    public async Task Renombrar_actualiza_la_vista_aunque_los_metadatos_esten_compartidos_por_Flyweight()
    {
        using var e = await Entorno.CrearAsync();
        await e.IniciarSesionAsync("admin");
        var consulta = e.Obtener<ConsultasCatalogo>();
        Assert.Equal("Mouse USB", (await consulta.ListarAsync("P01")).Single().Nombre); // llena la cache compartida

        await e.Obtener<ServicioCatalogo>().ActualizarAsync("P01", "Mouse Inalambrico", 25.00m);

        var fila = (await consulta.ListarAsync("P01")).Single();
        Assert.Equal(("Mouse Inalambrico", 25.00m), (fila.Nombre, fila.Precio));
        await Assert.ThrowsAsync<DomainException>(() => e.Obtener<ServicioCatalogo>().ActualizarAsync("KIT_OFICINA", "Kit", 10m));
    }

    [Fact]
    public async Task Clientes_se_registran_editan_y_desactivan()
    {
        using var e = await Entorno.CrearAsync();
        await e.IniciarSesionAsync("ventas");
        var servicio = e.Obtener<ServicioClientes>();
        var id = await servicio.RegistrarAsync("Marta", "marta@example.test", CategoriaCliente.Normal);

        await servicio.ActualizarAsync(id, "Marta Ruiz", "marta@example.test", CategoriaCliente.Vip, activo: false);

        var c = (await servicio.ListarAsync("Marta")).Single();
        Assert.Equal(("Marta Ruiz", "Vip", false), (c.Nombre, c.Categoria, c.Activo));
        await e.IniciarSesionAsync("consulta");
        await Assert.ThrowsAsync<AccesoDenegadoException>(() => servicio.RegistrarAsync("X", "", CategoriaCliente.Normal));
    }

    [Fact]
    public async Task Un_cliente_inactivo_no_puede_confirmar_su_pedido_Chain()
    {
        using var e = await Entorno.CrearAsync();
        await e.IniciarSesionAsync("ventas");
        var id = await e.CrearPedidoAsync("Luis", TipoEntrega.Estandar, ("P05", 1));
        var luis = (await e.Obtener<IRepositorioClientes>().BuscarAsync("Luis")).Single();
        await e.Obtener<ServicioClientes>().ActualizarAsync(luis.Id, "Luis", "luis@example.test", CategoriaCliente.Normal, activo: false);

        var r = await e.Obtener<ConfirmacionPedidoFacade>().ConfirmarAsync(id, "k-inactivo");

        Assert.False(r.Exito);
        Assert.Equal("ClienteActivo", r.Eslabon);
    }

    [Fact]
    public async Task Detalle_de_pedido_expone_lineas_totales_e_historial_como_DTO()
    {
        using var e = await Entorno.CrearAsync();
        var id = await Escenarios.PagarAnaAsync(e);

        var d = (await e.Obtener<ConsultasPedidos>().ObtenerAsync(id))!;

        Assert.Equal(("Ana", "Pagado", 60.00m, 6.00m, "VIP", 5.00m, 59.00m, false), (d.Cliente, d.Estado, d.Subtotal, d.Descuento, d.Politica, d.Tarifa, d.Total, d.PermiteEdicion));
        Assert.Equal("KIT_TELETRABAJO", d.Lineas.Single().Sku);
        Assert.Equal(new[] { "Borrador", "Validado", "Pagado" }, d.Historial.Select(h => h.Hacia));
        Assert.Null(await e.Obtener<ConsultasPedidos>().ObtenerAsync(9999));
    }

    [Fact] // Prototype a traves del caso de uso
    public async Task Clonar_borrador_crea_una_plantilla_nueva_sin_tocar_el_original()
    {
        using var e = await Entorno.CrearAsync();
        await e.IniciarSesionAsync("ventas");
        var original = await e.CrearPedidoAsync("Ana", TipoEntrega.Estandar, ("KIT_TELETRABAJO", 1));
        var pedidos = e.Obtener<ServicioPedidos>();

        var copia = await pedidos.ClonarBorradorAsync(original);
        await e.Obtener<ServicioEdicionBorrador>().CambiarCantidadAsync(copia, "KIT_TELETRABAJO", 3);

        var consulta = e.Obtener<ConsultasPedidos>();
        var o = (await consulta.ObtenerAsync(original))!;
        var c = (await consulta.ObtenerAsync(copia))!;
        Assert.NotEqual(original, copia);
        Assert.Equal((60.00m, "Borrador"), (o.Subtotal, o.Estado));
        Assert.Equal((180.00m, "Borrador"), (c.Subtotal, c.Estado));
        // un pedido pagado no es plantilla valida
        Assert.True((await e.Obtener<ConfirmacionPedidoFacade>().ConfirmarAsync(original, "k-orig")).Exito);
        await Assert.ThrowsAsync<DomainException>(() => pedidos.ClonarBorradorAsync(original));
    }

    [Fact]
    public async Task Inventario_marca_alertas_y_lista_movimientos_con_su_referencia()
    {
        using var e = await Entorno.CrearAsync();
        await e.IniciarSesionAsync("ventas");
        var id = await e.CrearPedidoAsync("Luis", TipoEntrega.Expres, ("P04", 2), ("P05", 2));
        Assert.True((await e.Obtener<ConfirmacionPedidoFacade>().ConfirmarAsync(id, "k-luis")).Exito);
        await e.IniciarSesionAsync("consulta");
        var consulta = e.Obtener<ConsultasInventario>();

        var stock = await consulta.StockAsync();
        Assert.Equal(new[] { "P04" }, stock.Where(s => s.Alerta).Select(s => s.Sku)); // P04 quedo en 2 (<= umbral 3)

        var movimientos = await consulta.MovimientosAsync("P04");
        Assert.Equal(new[] { ("Venta", -2, 2, $"PEDIDO-{id}"), ("CargaInicial", 4, 4, "SEED") },
            movimientos.Select(m => (m.Motivo, m.Cantidad, m.StockResultante, m.Referencia)));
    }

    [Fact]
    public async Task Auditoria_y_resumen_Outbox_solo_para_roles_con_permiso()
    {
        using var e = await Entorno.CrearAsync();
        await Escenarios.PagarAnaAsync(e);
        await e.IniciarSesionAsync("ventas");
        await Assert.ThrowsAsync<AccesoDenegadoException>(() => e.Obtener<ConsultasAuditoria>().ListarAsync());

        await e.IniciarSesionAsync("consulta");
        var auditoria = e.Obtener<ConsultasAuditoria>();
        Assert.Contains(await auditoria.ListarAsync(), a => a.Accion == "LOGIN_OK" && a.Categoria == "Seguridad");
        Assert.Equal(new ResumenOutbox(1, 0, 0), await auditoria.ResumenOutboxAsync());
        await e.Obtener<LogiMarket.Application.Eventos.ProcesadorOutbox>().ProcesarPendientesAsync();
        Assert.Equal(new ResumenOutbox(0, 1, 0), await auditoria.ResumenOutboxAsync());
    }
}