using LogiMarket.Infrastructure;
using LogiMarket.Infrastructure.Persistencia;
using LogiMarket.WinForms.Infraestructura;
using LogiMarket.WinForms.Ui;
using Microsoft.Extensions.DependencyInjection;

namespace LogiMarket.WinForms;

static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        System.Windows.Forms.Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        System.Windows.Forms.Application.ThreadException += (_, e) =>
            MessageBox.Show(e.Exception.Message, "LogiMarket 360", MessageBoxButtons.OK, MessageBoxIcon.Error);
        // Formato decimal consistente en toda la interfaz (dos decimales con punto, como en los importes USD).
        var cultura = (System.Globalization.CultureInfo)new System.Globalization.CultureInfo("es-EC").Clone();
        cultura.NumberFormat.NumberDecimalSeparator = ".";
        cultura.NumberFormat.NumberGroupSeparator = ",";
        cultura.NumberFormat.CurrencyDecimalSeparator = ".";
        cultura.NumberFormat.PercentDecimalSeparator = ".";
        System.Globalization.CultureInfo.DefaultThreadCurrentCulture = cultura;
        System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = cultura;
        Thread.CurrentThread.CurrentCulture = cultura;
        ApplicationConfiguration.Initialize();

        using var provider = ComposeServices(ValorDe(args, "--db") ?? RutaPredeterminada());
        using (var scope = provider.CreateScope())
            scope.ServiceProvider.GetRequiredService<InicializadorBaseDatos>().InicializarAsync().GetAwaiter().GetResult();

        if (args.Contains("--autoprueba"))
            return AutoPrueba.Ejecutar(provider, ValorDe(args, "--capturas"));

        while (true)
        {
            using var acceso = provider.GetRequiredService<FormAcceso>();
            if (acceso.ShowDialog() != DialogResult.OK) break;
            using var principal = provider.GetRequiredService<FormPrincipal>();
            principal.ShowDialog();
            if (!principal.CerroSesion) break; // se cerro la ventana: salir; si cerro sesion: volver al acceso
        }
        return 0;
    }

    // Composition root: unico lugar donde se registran dependencias (Application, Infrastructure, formularios).
    static ServiceProvider ComposeServices(string rutaBase)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(rutaBase))!);
        var services = new ServiceCollection();
        services.AddInfrastructure($"Data Source={rutaBase}");
        services.AddAplicacion();
        services.AddSingleton<Aplicacion>();
        services.AddTransient<FormAcceso>();
        services.AddTransient<FormPrincipal>();
        foreach (var vista in typeof(VistaBase).Assembly.GetTypes().Where(t => !t.IsAbstract && t.IsSubclassOf(typeof(VistaBase))))
            services.AddTransient(typeof(VistaBase), vista);
        return services.BuildServiceProvider();
    }

    static string RutaPredeterminada() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LogiMarket360", "logimarket.db");

    static string? ValorDe(string[] args, string clave)
    {
        var i = Array.IndexOf(args, clave);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }
}
