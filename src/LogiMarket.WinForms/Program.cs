using LogiMarket.Infrastructure;
using LogiMarket.Infrastructure.Persistencia;
using Microsoft.Extensions.DependencyInjection;

namespace LogiMarket.WinForms;

static class Program
{
    [STAThread]
    static void Main()
    {
        System.Windows.Forms.Application.ThreadException += (_, e) =>
            MessageBox.Show(e.Exception.Message, "LogiMarket 360", MessageBoxButtons.OK, MessageBoxIcon.Error);

        ApplicationConfiguration.Initialize();

        using var provider = ComposeServices();
        using (var scope = provider.CreateScope())
            scope.ServiceProvider.GetRequiredService<InicializadorBaseDatos>().InicializarAsync().GetAwaiter().GetResult();

        System.Windows.Forms.Application.Run(provider.GetRequiredService<Form1>());
    }

    // Composition root: unico lugar donde se registran dependencias (Application, Infrastructure, formularios).
    static ServiceProvider ComposeServices()
    {
        var services = new ServiceCollection();
        var carpeta = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LogiMarket360");
        Directory.CreateDirectory(carpeta);
        services.AddInfrastructure($"Data Source={Path.Combine(carpeta, "logimarket.db")}");
        services.AddAplicacion();
        services.AddTransient<Form1>();
        return services.BuildServiceProvider();
    }
}
