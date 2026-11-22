using LogiMarket.Application.Catalogo;
using LogiMarket.Application.Comprobantes;
using LogiMarket.Application.Comun;
using LogiMarket.Application.Demo;
using LogiMarket.Application.Metricas;
using LogiMarket.Application.Pagos;
using LogiMarket.Application.Puertos;
using LogiMarket.Application.Seguridad;
using LogiMarket.Application.Servicios;
using LogiMarket.Domain.Catalogo;
using LogiMarket.Domain.Devoluciones;
using LogiMarket.Infrastructure.Pagos;
using LogiMarket.Infrastructure.Pagos.Legado;
using LogiMarket.Infrastructure.Persistencia;
using LogiMarket.Infrastructure.Salidas;
using LogiMarket.Infrastructure.Sistema;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace LogiMarket.Tests.Aplicacion;

[CollectionDefinition("SinParalelismo", DisableParallelization = true)]
public sealed class SinParalelismo;

public class PatronesEstructuralesTests(ITestOutputHelper salida)
{
    private static string Secuencia(BitacoraOperaciones b) =>
        string.Join(" -> ", b.Entradas.Select(x => $"{x.Etapa}[{x.Correlacion}]"));

    // ---------- P15 Singleton ----------
    [Fact]
    public async Task P15_Singleton_dos_consumidores_reciben_la_misma_instancia_y_cuentan_sin_perder_incrementos()
    {
        using var e = await Entorno.CrearAsync();
        using var vistaA = e.NuevoAlcance();
        using var vistaB = e.NuevoAlcance();
        var a = vistaA.ServiceProvider.GetRequiredService<MetricasSesion>();
        var b = vistaB.ServiceProvider.GetRequiredService<MetricasSesion>();

        Assert.Same(a, b);
        Parallel.For(0, 1000, _ => a.RegistrarPedidoPagado());
        Assert.Equal(1000, b.PedidosPagados); // contadores coherentes y thread-safe
    }

    // ---------- P16 Adapter ----------
    [Fact]
    public async Task P16_Adapter_ambos_proveedores_cumplen_el_mismo_contrato_y_el_legado_recibe_centavos()
    {
        using var e = await Entorno.CrearAsync();
        await e.IniciarSesionAsync("ventas");

        var simulada = await e.Obtener<IPasarelaPago>().CobrarAsync(59.00m, "k-sim");
        Assert.True(simulada.Aprobado);
        Assert.NotEmpty(simulada.Referencia);
        Assert.Equal("SIMULADA", e.Obtener<IPasarelaPago>().Proveedor);

        using var otra = e.AbrirOtraSesion();
        await otra.IniciarSesionAsync("ventas");
        otra.Obtener<ConfiguracionDemo>().ProveedorPago = "LEGADA";
        var legada = await otra.Obtener<IPasarelaPago>().CobrarAsync(59.00m, "k-leg");
        Assert.True(legada.Aprobado);
        Assert.NotEmpty(legada.Referencia);
        Assert.Equal("LEGADA", otra.Obtener<IPasarelaPago>().Proveedor);
        var legado = otra.Obtener<PasarelaLegadaSimulada>();
        Assert.Equal((1, 5900L, "k-leg"), (legado.Llamadas, legado.UltimosCentavos, legado.UltimaTransaccion));
    }

    [Fact]
    public async Task P16_Adapter_flujo_completo_con_proveedor_legado_da_el_mismo_resultado_de_negocio()
    {
        using var e = await Entorno.CrearAsync();
        e.Obtener<ConfiguracionDemo>().ProveedorPago = "LEGADA";
        var id = await Escenarios.PagarAnaAsync(e);

        var db = e.Obtener<LogiMarketDbContext>();
        var pago = await db.Pagos.SingleAsync(p => p.PedidoId == id);
        Assert.Equal(("LEGADA", 59.00m), (pago.Proveedor, pago.Monto));
        Assert.Equal(7, (await e.StockAsync())["P01"]);
    }

    [Fact]
    public async Task P16_Adapter_el_rechazo_del_legado_se_traduce_al_contrato_y_la_aplicacion_no_conoce_CobrarCentavos()
    {
        using var e = await Entorno.CrearAsync();
        await e.IniciarSesionAsync("ventas");
        var demo = e.Obtener<ConfiguracionDemo>();
        demo.ProveedorPago = "LEGADA";
        demo.FallarCobro = true;

        var r = await e.Obtener<IPasarelaPago>().CobrarAsync(10m, "k-x");

        Assert.False(r.Aprobado);
        Assert.Contains("legado", r.Motivo);
        var tipos = typeof(IPasarelaPago).Assembly.GetTypes();
        Assert.DoesNotContain(tipos.SelectMany(t => t.GetMethods()), m => m.Name == "CobrarCentavos");
    }

    // ---------- P17 Bridge ----------
    [Theory]
    [InlineData("VISTA_PREVIA")]
    [InlineData("TXT")]
    public async Task P17_Bridge_venta_y_devolucion_por_cada_medio_de_salida(string formato)
    {
        var directorio = Path.Combine(Path.GetTempPath(), $"logimarket_txt_{Guid.NewGuid():N}");
        try
        {
            using var e = await Entorno.CrearAsync(s => s.AddSingleton(new OpcionesSalidaDocumento(directorio)));
            var id = await Escenarios.EntregarAnaAsync(e);
            Assert.True((await e.Obtener<ServicioDevoluciones>().SolicitarAsync(new(id, MotivoDevolucion.Defecto, false))).Exito);
            var servicio = e.Obtener<ServicioComprobantes>();

            var venta = await servicio.EmitirVentaAsync(id, formato);
            var devolucion = await servicio.EmitirDevolucionAsync(id, formato);

            Assert.Contains("Total: USD 59.00", venta.Contenido);
            Assert.Contains("Descuento (VIP): USD 6.00", venta.Contenido);
            Assert.Contains("Reembolso simulado: USD 54.00", devolucion.Contenido);
            if (formato == "TXT")
            {
                Assert.Equal(venta.Contenido, await File.ReadAllTextAsync(venta.Destino)); // archivo real
                Assert.Equal(devolucion.Contenido, await File.ReadAllTextAsync(devolucion.Destino));
                Assert.NotEqual(venta.Destino, devolucion.Destino);
            }
            else
            {
                Assert.Equal(devolucion.Contenido, e.Obtener<VistaPreviaSalida>().UltimaVista); // vista visible
                Assert.False(Directory.Exists(directorio));
            }
        }
        finally
        {
            if (Directory.Exists(directorio)) Directory.Delete(directorio, true);
        }
    }

    [Fact]
    public async Task P17_Bridge_formato_desconocido_se_rechaza()
    {
        using var e = await Entorno.CrearAsync();
        var id = await Escenarios.PagarAnaAsync(e);
        await Assert.ThrowsAsync<LogiMarket.Domain.Common.DomainException>(() => e.Obtener<ServicioComprobantes>().EmitirVentaAsync(id, "PDF"));
    }

    // ---------- P19 Decorator ----------
    [Fact]
    public async Task P19_Decorator_cobro_exitoso_registra_INICIO_PASARELA_FIN_con_la_misma_correlacion()
    {
        using var e = await Entorno.CrearAsync();
        await e.IniciarSesionAsync("ventas");

        var r = await e.Obtener<IPasarelaPago>().CobrarAsync(59.00m, "k-log");

        var entradas = e.Obtener<BitacoraOperaciones>().Entradas;
        salida.WriteLine("Secuencia P19 (exito): " + Secuencia(e.Obtener<BitacoraOperaciones>()));
        Assert.True(r.Aprobado);
        Assert.Equal(new[] { "INICIO", "PASARELA", "FIN" }, entradas.Select(x => x.Etapa));
        Assert.Single(entradas.Select(x => x.Correlacion).Distinct());
        Assert.Contains("APROBADO", entradas[^1].Detalle);
    }

    [Fact]
    public async Task P19_Decorator_cobro_rechazado_se_registra_sin_alterar_el_resultado()
    {
        using var e = await Entorno.CrearAsync();
        await e.IniciarSesionAsync("ventas");
        e.Obtener<ConfiguracionDemo>().FallarCobro = true;

        var r = await e.Obtener<IPasarelaPago>().CobrarAsync(59.00m, "k-log2");

        salida.WriteLine("Secuencia P19 (rechazo): " + Secuencia(e.Obtener<BitacoraOperaciones>()));
        Assert.False(r.Aprobado);
        Assert.Equal(new[] { "INICIO", "PASARELA", "FIN" }, e.Obtener<BitacoraOperaciones>().Entradas.Select(x => x.Etapa));
        Assert.Contains("RECHAZADO", e.Obtener<BitacoraOperaciones>().Entradas[^1].Detalle);
    }

    [Fact]
    public async Task P19_Decorator_un_error_del_cobro_registra_ERROR_y_propaga_la_misma_excepcion()
    {
        var bitacora = new BitacoraOperaciones();
        var decorador = new AuditoriaPagoDecorator(new PasarelaQueFalla(), bitacora);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => decorador.CobrarAsync(10m, "k-err"));

        salida.WriteLine("Secuencia P19 (error): " + Secuencia(bitacora));
        Assert.Equal("fallo del proveedor", ex.Message);
        Assert.Equal(new[] { "INICIO", "ERROR" }, bitacora.Entradas.Select(x => x.Etapa));
    }

    // ---------- P22 Proxy ----------
    [Theory]
    [InlineData("consulta")]
    [InlineData("bodega")]
    public async Task P22_Proxy_deniega_a_CONSULTA_y_BODEGA_antes_de_Decorator_y_proveedor(string rol)
    {
        using var e = await Entorno.CrearAsync();
        await e.IniciarSesionAsync(rol);
        var espia = new PasarelaEspia();
        var bitacora = new BitacoraOperaciones();
        var proxy = new PagoAutorizadoProxy(e.Obtener<IAutorizador>(), new AuditoriaPagoDecorator(espia, bitacora));

        await Assert.ThrowsAsync<AccesoDenegadoException>(() => proxy.CobrarAsync(59m, "k-denegado"));

        salida.WriteLine($"P22 [{rol}]: llamadas al proveedor={espia.Llamadas}, entradas de LOG={bitacora.Entradas.Count}");
        Assert.Equal(0, espia.Llamadas);
        Assert.Empty(bitacora.Entradas);
        Assert.Equal(1, await e.Obtener<LogiMarketDbContext>().Auditoria.CountAsync(a => a.Accion == "ACCESO_DENEGADO"));
    }

    [Theory]
    [InlineData("ventas")]
    [InlineData("admin")]
    public async Task P22_Proxy_deja_pasar_a_VENTAS_y_ADMIN(string rol)
    {
        using var e = await Entorno.CrearAsync();
        await e.IniciarSesionAsync(rol);
        var espia = new PasarelaEspia();
        var bitacora = new BitacoraOperaciones();
        var proxy = new PagoAutorizadoProxy(e.Obtener<IAutorizador>(), new AuditoriaPagoDecorator(espia, bitacora));

        Assert.True((await proxy.CobrarAsync(59m, "k-ok")).Aprobado);

        Assert.Equal(1, espia.Llamadas);
        Assert.Equal(new[] { "INICIO", "FIN" }, bitacora.Entradas.Select(x => x.Etapa));
    }

    [Fact]
    public async Task P22_Proxy_en_la_cadena_real_CONSULTA_no_alcanza_la_pasarela_legada_ni_deja_LOG()
    {
        using var e = await Entorno.CrearAsync();
        e.Obtener<ConfiguracionDemo>().ProveedorPago = "LEGADA";
        await e.IniciarSesionAsync("consulta");

        await Assert.ThrowsAsync<AccesoDenegadoException>(() => e.Obtener<IPasarelaPago>().CobrarAsync(59m, "k-x"));

        Assert.Equal(0, e.Obtener<PasarelaLegadaSimulada>().Llamadas);
        Assert.Empty(e.Obtener<BitacoraOperaciones>().Entradas);
    }

    // ---------- P21 Flyweight ----------
    [Collection("SinParalelismo")]
    public class Flyweight(ITestOutputHelper salida)
    {
        private const int Filas = 1000;

        [Fact]
        public async Task P21_Flyweight_1000_filas_comparten_metadatos_y_ocupan_menos_que_la_copia_ingenua()
        {
            using var e = await Entorno.CrearAsync();
            await e.IniciarSesionAsync("consulta");
            var catalogo = await e.Obtener<IRepositorioCatalogo>().ObtenerTodosAsync();
            var factory = new CatalogoVisualFlyweightFactory();

            // Condiciones: mismas 1000 filas (7 SKU del catalogo oficial, ciclico), mismo contenido, GC forzado antes y despues.
            var antesFly = GC.GetTotalMemory(true);
            var filasFly = Enumerable.Range(0, Filas).Select(i => factory.CrearFila(catalogo[i % catalogo.Count], i)).ToList();
            var bytesFly = GC.GetTotalMemory(true) - antesFly;

            var antesNaive = GC.GetTotalMemory(true);
            var filasNaive = Enumerable.Range(0, Filas).Select(i =>
            {
                var el = catalogo[i % catalogo.Count];
                var meta = new MetadatosSku(new string(el.Sku), new string(el.Nombre), new string(el.Categoria), new string(el.Descripcion));
                return new FilaCatalogoVista(meta, el is ProductoSimple p ? p.Stock : 0, el.Precio, i);
            }).ToList();
            var bytesNaive = GC.GetTotalMemory(true) - antesNaive;
            GC.KeepAlive(filasFly); GC.KeepAlive(filasNaive);

            var distintos = filasFly.Select(f => f.Metadatos).Distinct(ReferenceEqualityComparer.Instance).Count();
            salida.WriteLine($"P21: filas={Filas}, SKU distintos={catalogo.Count}, instancias de metadatos con Flyweight={distintos}, sin Flyweight={Filas}");
            salida.WriteLine($"P21: memoria retenida con Flyweight={bytesFly} B, sin Flyweight={bytesNaive} B (misma carga, GC forzado)");

            Assert.Equal(catalogo.Count, distintos);
            Assert.Equal(catalogo.Count, factory.InstanciasCompartidas);
            var porSku = filasFly.GroupBy(f => f.Metadatos.Sku);
            Assert.All(porSku, g => Assert.All(g, f => Assert.True(ReferenceEquals(f.Metadatos, g.First().Metadatos))));
            Assert.True(bytesFly < bytesNaive, $"Flyweight {bytesFly} B no es menor que {bytesNaive} B");
            Assert.Equal(Filas, filasFly.Select(f => f.Cantidad).Distinct().Count()); // lo extrinseco es por fila
        }
    }

    private sealed class PasarelaEspia : IPasarelaPago
    {
        public int Llamadas { get; private set; }
        public string Proveedor => "ESPIA";

        public Task<ResultadoCobro> CobrarAsync(decimal monto, string claveIdempotencia, CancellationToken ct = default)
        {
            Llamadas++;
            return Task.FromResult(new ResultadoCobro(true, "ESP-1", null));
        }
    }

    private sealed class PasarelaQueFalla : IPasarelaPago
    {
        public string Proveedor => "FALLA";
        public Task<ResultadoCobro> CobrarAsync(decimal monto, string claveIdempotencia, CancellationToken ct = default) =>
            throw new InvalidOperationException("fallo del proveedor");
    }
}