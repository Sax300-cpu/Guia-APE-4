using System.Reflection;
using System.Xml.Linq;
using LogiMarket.Application.Comprobantes;
using LogiMarket.Application.Demo;
using LogiMarket.Application.Pagos;
using LogiMarket.Application.Puertos;
using LogiMarket.Application.Servicios;
using LogiMarket.Domain.Catalogo;
using LogiMarket.Domain.Pedidos;
using LogiMarket.Infrastructure.Pagos;
using LogiMarket.Infrastructure.Salidas;
using LogiMarket.Tests.Aplicacion;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace LogiMarket.Tests.Arquitectura;

/// <summary>P38: dependencias reales (.csproj y ensamblados), DIP, ISP y LSP comprobados con codigo.</summary>
public class ArquitecturaTests(ITestOutputHelper salida)
{
    private static readonly string Raiz = BuscarRaiz();

    private static string BuscarRaiz()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "LogiMarket360.sln"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("No se encontro LogiMarket360.sln");
    }

    private static string Csproj(string proyecto) =>
        Directory.GetFiles(Path.Combine(Raiz, proyecto.StartsWith("LogiMarket.Tests") ? "tests" : "src", proyecto), "*.csproj").Single();

    private static string[] ReferenciasDeProyecto(string proyecto) =>
        XDocument.Load(Csproj(proyecto)).Descendants("ProjectReference")
            .Select(r => Path.GetFileNameWithoutExtension((string)r.Attribute("Include")!)).OrderBy(x => x).ToArray();

    private static string[] Paquetes(string proyecto) =>
        XDocument.Load(Csproj(proyecto)).Descendants("PackageReference").Select(r => (string)r.Attribute("Include")!).ToArray();

    [Fact]
    public void Referencias_de_proyecto_cumplen_la_regla_de_dependencia_de_la_guia()
    {
        Assert.Empty(ReferenciasDeProyecto("LogiMarket.Domain"));
        Assert.Equal(new[] { "LogiMarket.Domain" }, ReferenciasDeProyecto("LogiMarket.Application"));
        Assert.Equal(new[] { "LogiMarket.Application", "LogiMarket.Domain" }, ReferenciasDeProyecto("LogiMarket.Infrastructure"));
        Assert.Equal(new[] { "LogiMarket.Application", "LogiMarket.Infrastructure" }, ReferenciasDeProyecto("LogiMarket.WinForms"));
        Assert.Equal(new[] { "LogiMarket.Application", "LogiMarket.Domain", "LogiMarket.Infrastructure" }, ReferenciasDeProyecto("LogiMarket.Tests"));
    }

    [Fact]
    public void Domain_y_Application_no_tienen_paquetes_de_implementacion()
    {
        Assert.Empty(Paquetes("LogiMarket.Domain"));
        Assert.Empty(Paquetes("LogiMarket.Application"));
        Assert.Contains(Paquetes("LogiMarket.Infrastructure"), p => p.StartsWith("Microsoft.EntityFrameworkCore"));
    }

    [Fact]
    public void Ninguna_capa_interna_referencia_EF_SQLite_ni_WinForms_en_compilacion()
    {
        var prohibidosDomain = new[] { "LogiMarket.Application", "LogiMarket.Infrastructure", "LogiMarket.WinForms", "EntityFramework", "Sqlite", "System.Windows.Forms" };
        var prohibidosApp = new[] { "LogiMarket.Infrastructure", "LogiMarket.WinForms", "EntityFramework", "Sqlite", "System.Windows.Forms" };

        Verificar(typeof(ProductoSimple).Assembly, prohibidosDomain);
        Verificar(typeof(IUnitOfWork).Assembly, prohibidosApp);

        static void Verificar(Assembly ensamblado, string[] prohibidos)
        {
            var referencias = ensamblado.GetReferencedAssemblies().Select(r => r.Name!).ToArray();
            Assert.DoesNotContain(referencias, r => prohibidos.Any(p => r.Contains(p)));
        }
    }

    [Theory]
    [InlineData("LogiMarket.Domain")]
    [InlineData("LogiMarket.Application")]
    public void El_codigo_fuente_interno_no_usa_EF_SQL_ni_Windows_Forms(string proyecto)
    {
        var prohibido = new[] { "Microsoft.EntityFrameworkCore", "Microsoft.Data.Sqlite", "System.Windows.Forms", "FromSqlRaw", "ExecuteSql", "SELECT ", "INSERT INTO" };
        var archivos = Directory.GetFiles(Path.Combine(Raiz, "src", proyecto), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));
        foreach (var archivo in archivos)
        {
            var texto = File.ReadAllText(archivo);
            foreach (var termino in prohibido)
                Assert.False(texto.Contains(termino), $"{Path.GetFileName(archivo)} contiene '{termino}'");
        }
    }

    [Fact] // DIP: los casos de uso dependen de abstracciones, no de repositorios concretos
    public void DIP_los_servicios_de_Application_reciben_interfaces_de_repositorio_por_constructor()
    {
        var servicios = typeof(ServicioPedidos).Assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.Namespace is not null && t.Namespace.StartsWith("LogiMarket.Application.Servicios") && t.GetConstructors().Length > 0);
        foreach (var tipo in servicios)
            foreach (var parametro in tipo.GetConstructors().Single().GetParameters())
            {
                var t = parametro.ParameterType;
                Assert.False(t.Name.StartsWith("Repositorio"), $"{tipo.Name} depende del repositorio concreto {t.Name}");
                if (t.Name.StartsWith("IRepositorio") || t.Name == "IUnitOfWork") Assert.True(t.IsInterface);
                Assert.DoesNotContain("Infrastructure", t.Namespace ?? "");
            }
    }

    [Fact] // ISP: interfaces pequenas y sin operaciones no aplicables
    public void ISP_las_interfaces_son_pequenas_y_ninguna_implementacion_lanza_NotImplemented()
    {
        var interfaces = new[] { typeof(ProductoSimple).Assembly, typeof(IUnitOfWork).Assembly }
            .SelectMany(a => a.GetTypes()).Where(t => t.IsInterface).ToList();
        foreach (var contrato in interfaces)
        {
            var miembros = contrato.GetMembers().Count(m => m is MethodInfo { IsSpecialName: false } or PropertyInfo or EventInfo);
            salida.WriteLine($"ISP {contrato.Name}: {miembros} miembros");
            Assert.True(miembros <= 7, $"{contrato.Name} tiene {miembros} miembros");
        }
        Assert.True(interfaces.Count >= 20);

        var fuentes = Directory.GetFiles(Path.Combine(Raiz, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"));
        Assert.DoesNotContain(fuentes, f => File.ReadAllText(f).Contains("NotImplementedException"));
    }

    // ---- LSP: dos implementaciones de IPasarelaPago son sustituibles ----
    [Theory]
    [InlineData("SIMULADA")]
    [InlineData("LEGADA")]
    public async Task LSP_IPasarelaPago_condiciones_de_entrada_resultado_y_excepciones_compatibles(string proveedor)
    {
        using var e = await Entorno.CrearAsync();
        await e.IniciarSesionAsync("ventas");
        e.Obtener<ConfiguracionDemo>().ProveedorPago = proveedor;
        var pasarela = e.Obtener<IPasarelaPago>();

        // Entrada invalida: misma excepcion en ambos
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => pasarela.CobrarAsync(0m, "k-0"));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => pasarela.CobrarAsync(-5m, "k-neg"));

        // Resultado aprobado: referencia no vacia, sin motivo de rechazo
        var ok = await pasarela.CobrarAsync(59.00m, "k-ok");
        Assert.True(ok.Aprobado);
        Assert.NotEmpty(ok.Referencia);
        Assert.Null(ok.Motivo);
        Assert.Equal(proveedor, pasarela.Proveedor);

        // Resultado rechazado: sin referencia y con motivo
        e.Obtener<ConfiguracionDemo>().FallarCobro = true;
        var rechazado = await pasarela.CobrarAsync(59.00m, "k-no");
        Assert.False(rechazado.Aprobado);
        Assert.Empty(rechazado.Referencia);
        Assert.NotEmpty(rechazado.Motivo!);
    }

    [Fact] // LSP: ISalidaDocumento (vista previa vs TXT) devuelven el mismo contenido y la misma excepcion de entrada
    public async Task LSP_ISalidaDocumento_ambas_salidas_son_sustituibles()
    {
        var directorio = Path.Combine(Path.GetTempPath(), $"logimarket_lsp_{Guid.NewGuid():N}");
        try
        {
            var documento = new DocumentoTexto("PRUEBA", new[] { "linea 1", "linea 2" });
            ISalidaDocumento[] salidas = { new VistaPreviaSalida(), new ArchivoTxtSalida(new OpcionesSalidaDocumento(directorio)) };
            var resultados = new List<ResultadoSalida>();
            foreach (var s in salidas) resultados.Add(await s.EscribirAsync("lsp", documento));

            Assert.All(resultados, r => Assert.Equal(documento.ComoTexto(), r.Contenido));
            Assert.All(resultados, r => Assert.NotEmpty(r.Destino));
            foreach (var s in salidas)
                await Assert.ThrowsAnyAsync<NullReferenceException>(() => s.EscribirAsync("lsp", null!));
        }
        finally
        {
            if (Directory.Exists(directorio)) Directory.Delete(directorio, true);
        }
    }

    // ---- OCP/DIP: cambiar de proveedor no toca el caso de uso ----
    [Fact]
    public async Task DIP_el_reemplazo_de_proveedor_se_hace_solo_por_DI_sin_cambiar_el_caso_de_uso()
    {
        using var e = await Entorno.CrearAsync(s => s.AddScoped<IPasarelaPago, ProveedorDeSustitucion>());
        await e.IniciarSesionAsync("ventas");
        var id = await e.CrearPedidoAsync("Ana", TipoEntrega.Estandar, ("P01", 1));

        var r = await e.Obtener<ConfirmacionPedidoFacade>().ConfirmarAsync(id, "k-sust");

        Assert.True(r.Exito);
        Assert.Equal("SUSTITUTO", e.Obtener<LogiMarket.Infrastructure.Persistencia.LogiMarketDbContext>().Pagos.Single().Proveedor);
    }

    private sealed class ProveedorDeSustitucion : IPasarelaPago
    {
        public string Proveedor => "SUSTITUTO";
        public Task<ResultadoCobro> CobrarAsync(decimal monto, string claveIdempotencia, CancellationToken ct = default) =>
            Task.FromResult(new ResultadoCobro(true, "SUS-1", null));
    }
}
