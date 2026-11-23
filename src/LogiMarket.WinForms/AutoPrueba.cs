using System.Drawing.Imaging;
using System.Text;
using LogiMarket.Application.Comprobantes;
using LogiMarket.Application.Consultas;
using LogiMarket.Application.Coordinacion;
using LogiMarket.Application.Eventos;
using LogiMarket.Application.Seguridad;
using LogiMarket.Application.Servicios;
using LogiMarket.Domain.Devoluciones;
using LogiMarket.Domain.Pedidos;
using LogiMarket.WinForms.Vistas;
using Microsoft.Extensions.DependencyInjection;

namespace LogiMarket.WinForms;

/// <summary>
/// Modo de verificacion (--autoprueba [--capturas carpeta] [--db archivo]): arma un escenario real con los casos de uso,
/// recorre las 12 pantallas por rol, comprueba el menu por permisos y guarda una imagen de cada pantalla.
/// Usa una base aparte: nunca toca la base del usuario.
/// </summary>
internal static class AutoPrueba
{
    public static int Ejecutar(ServiceProvider provider, string? carpeta)
    {
        carpeta ??= Path.Combine(Path.GetTempPath(), "logimarket_autoprueba");
        Directory.CreateDirectory(carpeta);
        var log = new StringBuilder();
        var codigo = 1;
        using var host = new Form { Opacity = 0, ShowInTaskbar = false, FormBorderStyle = FormBorderStyle.None, Size = new Size(1, 1) };
        host.Shown += async (_, _) =>
        {
            try
            {
                await Escenario(provider, carpeta, log);
                codigo = 0;
                log.AppendLine("RESULTADO: OK");
            }
            catch (Exception ex)
            {
                log.AppendLine("RESULTADO: FALLO -> " + ex);
            }
            finally
            {
                host.Close();
            }
        };
        System.Windows.Forms.Application.Run(host);
        File.WriteAllText(Path.Combine(carpeta, "autoprueba.txt"), log.ToString(), new UTF8Encoding(true));
        return codigo;
    }

    private static async Task Escenario(ServiceProvider provider, string carpeta, StringBuilder log)
    {
        var app = provider.GetRequiredService<Infraestructura.Aplicacion>();

        // ---- 01 Acceso (con error y con exito) ----
        using (var acceso = provider.GetRequiredService<FormAcceso>())
        {
            acceso.Show();
            await acceso.IntentarAsync("ventas", "incorrecta");
            await Pausa();
            Capturar(acceso, carpeta, "01_acceso_error.png");
            log.AppendLine("Acceso con credenciales incorrectas: mensaje mostrado, sesion autenticada = " + app.Sesion.Autenticado);
            if (app.Sesion.Autenticado) throw new InvalidOperationException("La sesion no debia iniciarse.");
        }

        // ---- Escenario de datos con los casos de uso (como lo haria un usuario) ----
        await IniciarSesion(app, "ventas", "Ventas123*");
        int ana = await Cliente(app, "Ana"), luis = await Cliente(app, "Luis"), carmen = await Cliente(app, "Carmen");
        var pedidoAna = await Crear(app, ana, TipoEntrega.Estandar, ("KIT_TELETRABAJO", 1));
        var pedidoStock = await Crear(app, carmen, TipoEntrega.Estandar, ("KIT_TELETRABAJO", 6));
        var pedidoLuis = await Crear(app, luis, TipoEntrega.Expres, ("P04", 2), ("P05", 2));
        var pedidoEntregado = await Crear(app, luis, TipoEntrega.Estandar, ("P05", 1));
        var borradorEditado = await Crear(app, ana, TipoEntrega.Estandar, ("P01", 1));

        var confAna = await app.EjecutarAsync<ConfirmacionPedidoFacade, ResultadoConfirmacion>(f => f.ConfirmarAsync(pedidoAna, "AUTO-ANA"));
        var confRechazo = await app.EjecutarAsync<ConfirmacionPedidoFacade, ResultadoConfirmacion>(f => f.ConfirmarAsync(pedidoStock, "AUTO-STOCK"));
        var confEntregado = await app.EjecutarAsync<ConfirmacionPedidoFacade, ResultadoConfirmacion>(f => f.ConfirmarAsync(pedidoEntregado, "AUTO-ENT"));
        Exigir(confAna is { Exito: true, Total: 59.00m }, "P02: Ana debia pagar 59.00");
        Exigir(confRechazo is { Exito: false, Eslabon: "Stock" }, "P05: el kit x6 debia rechazarse por stock");
        log.AppendLine($"P02 Ana: {confAna.Total:0.00} | P05 rechazo: {confRechazo.Mensaje}");

        await app.EjecutarAsync<ServicioEdicionBorrador, LogiMarket.Application.Comun.ResultadoOperacion>(s => s.AgregarLineaAsync(borradorEditado, "P05", 2));
        await app.EjecutarAsync<ServicioEdicionBorrador, LogiMarket.Application.Comun.ResultadoOperacion>(s => s.CambiarCantidadAsync(borradorEditado, "P01", 3));

        await IniciarSesion(app, "bodega", "Bodega123*");
        await app.EjecutarAsync<ServicioDespacho, LogiMarket.Application.Comun.ResultadoOperacion>(s => s.DespacharAsync(pedidoAna));
        await app.EjecutarAsync<ServicioDespacho, LogiMarket.Application.Comun.ResultadoOperacion>(s => s.EntregarAsync(pedidoAna));
        await app.EjecutarAsync<ServicioDespacho, LogiMarket.Application.Comun.ResultadoOperacion>(s => s.DespacharAsync(pedidoEntregado));
        await app.EjecutarAsync<ServicioDespacho, LogiMarket.Application.Comun.ResultadoOperacion>(s => s.EntregarAsync(pedidoEntregado));

        await IniciarSesion(app, "ventas", "Ventas123*");
        var dev = await app.EjecutarAsync<ServicioDevoluciones, ResultadoDevolucion>(s => s.SolicitarAsync(new SolicitudDevolucion(pedidoAna, MotivoDevolucion.Defecto, false)));
        Exigir(dev is { Exito: true, Reembolso: 54.00m }, "P35: el reembolso de Ana debia ser 54.00");
        log.AppendLine($"P35 devolucion Ana: reembolso {dev.Reembolso:0.00}");
        var procesados = await app.EjecutarAsync<ProcesadorOutbox, int>(p => p.ProcesarPendientesAsync());
        log.AppendLine($"Outbox procesado: {procesados} evento(s)");

        // ---- Menu por rol ----
        var esperado = new Dictionary<string, int> { ["admin"] = 11, ["ventas"] = 9, ["bodega"] = 7, ["consulta"] = 7 };
        var claves = new Dictionary<string, string> { ["admin"] = "Admin123*", ["ventas"] = "Ventas123*", ["bodega"] = "Bodega123*", ["consulta"] = "Consulta123*" };
        foreach (var rol in new[] { "ventas", "bodega", "consulta" })
        {
            await IniciarSesion(app, rol, claves[rol]);
            using var principal = provider.GetRequiredService<FormPrincipal>();
            principal.Show();
            await Pausa(600);
            Capturar(principal, carpeta, $"rol_{rol}.png");
            log.AppendLine($"Menu de {rol}: " + string.Join(", ", principal.Vistas.Select(v => v.Titulo)));
            Exigir(principal.Vistas.Count == esperado[rol], $"El rol {rol} debia ver {esperado[rol]} pantallas y ve {principal.Vistas.Count}");
        }

        // ---- Las 12 pantallas con el rol ADMIN ----
        await IniciarSesion(app, "admin", claves["admin"]);
        using (var principal = provider.GetRequiredService<FormPrincipal>())
        {
            principal.Show();
            await Pausa(600);
            Exigir(principal.Vistas.Count == esperado["admin"], "ADMIN debia ver las 11 pantallas internas");
            var nombres = new Dictionary<TipoVista, string>
            {
                [TipoVista.Panel] = "02_panel",
                [TipoVista.Catalogo] = "03_catalogo",
                [TipoVista.Clientes] = "04_clientes",
                [TipoVista.Carrito] = "05_carrito",
                [TipoVista.Pedidos] = "06_pedidos",
                [TipoVista.Cobro] = "07_cobro",
                [TipoVista.Despacho] = "08_despacho",
                [TipoVista.Devoluciones] = "09_devoluciones",
                [TipoVista.Inventario] = "10_inventario",
                [TipoVista.Reportes] = "11_reportes",
                [TipoVista.Auditoria] = "12_auditoria"
            };
            foreach (var vista in principal.Vistas)
            {
                await principal.MostrarAsync(vista.Tipo);
                if (vista is VistaCarrito carrito) await carrito.CargarAsync(borradorEditado);
                if (vista is VistaReportes reportes) await reportes.DemostrarAsync("ESTADO = ENTREGADO OR TOTAL >= 50", pedidoAna);
                await Pausa(500);
                Capturar(principal, carpeta, nombres[vista.Tipo] + ".png");
                log.AppendLine($"Pantalla '{vista.Titulo}' refrescada y capturada");
                if (vista is VistaAuditoria auditoria)
                {
                    auditoria.SeleccionarPestana(1);
                    await Pausa(300);
                    Capturar(principal, carpeta, "12b_metricas.png");
                    auditoria.SeleccionarPestana(2);
                    await Pausa(300);
                    Capturar(principal, carpeta, "12c_demostracion.png");
                }
            }

            // Mediator: tras una operacion, las demas pantallas se refrescan sin conocerse entre si
            await app.Mediador.NotificarAsync(TipoVista.Cobro, OperacionUI.PedidoConfirmado);
            log.AppendLine("Mediator: PedidoConfirmado notificado a las vistas registradas");
        }

        await RecorridoPorBotones(provider, app, carpeta, log);

        var resumen = await app.EjecutarAsync<ConsultasPedidos, IReadOnlyList<PedidoResumenDto>>(c => c.ListarAsync());
        log.AppendLine("Pedidos finales: " + string.Join(" | ", resumen.OrderBy(p => p.Id).Select(p => $"#{p.Id} {p.Cliente} {p.Estado} {p.Total:0.00}")));
    }

    /// <summary>Segunda fase: el mismo flujo pero pulsando los botones reales de las pantallas, como lo haria una persona.</summary>
    private static async Task RecorridoPorBotones(ServiceProvider provider, Infraestructura.Aplicacion app, string carpeta, StringBuilder log)
    {
        Ui.Ui.ConfirmadorAutomatico = (_, mensaje) => { log.AppendLine("   [confirmacion aceptada] " + mensaje); return true; };
        Ui.Ui.AvisoAutomatico = mensaje => log.AppendLine("   [aviso] " + mensaje);
        try
        {
            log.AppendLine("--- Recorrido por botones (rol VENTAS) ---");
            await IniciarSesion(app, "ventas", "Ventas123*");
            int pedido;
            using (var principal = provider.GetRequiredService<FormPrincipal>())
            {
                principal.Show();
                await Pausa(400);

                // Carrito: crear borrador, editar con deshacer/rehacer y validar
                await principal.MostrarAsync(TipoVista.Carrito);
                var carrito = principal.Vistas.OfType<VistaCarrito>().Single();
                Elegir(Hallar<ComboBox>(carrito, "Cliente del pedido"), "Luis");
                Elegir(Hallar<ComboBox>(carrito, "Producto o kit"), "P04 -");
                Hallar<NumericUpDown>(carrito, "Cantidad").Value = 2;
                await Presionar(carrito, "Agregar linea");
                Exigir(Hallar<Label>(carrito, "Estado del borrador").Text.Contains("BORRADOR"), "El borrador debia crearse desde la pantalla Carrito");
                Elegir(Hallar<ComboBox>(carrito, "Producto o kit"), "P05 -");
                await Presionar(carrito, "Agregar linea");
                var total = Hallar<Label>(carrito, "Total del borrador");
                Exigir(total.Text == "USD 95.00", "Total esperado USD 95.00 y fue " + total.Text);
                await Presionar(carrito, "Deshacer");
                Exigir(total.Text == "USD 85.00", "Tras deshacer se esperaba USD 85.00 y fue " + total.Text);
                await Presionar(carrito, "Rehacer");
                Exigir(total.Text == "USD 95.00", "Tras rehacer se esperaba USD 95.00 y fue " + total.Text);
                var original = int.Parse(Hallar<Label>(carrito, "Estado del borrador").Text.Split(' ')[0].TrimStart('#'));
                await Presionar(carrito, "Duplicar como plantilla");
                Exigir(Hallar<Label>(carrito, "Resultado del carrito").Text.Contains("Plantilla creada"), "La plantilla debia crearse");
                await Presionar(carrito, "Cancelar pedido"); // cancela la copia, no el original
                Exigir(Hallar<Label>(carrito, "Resultado del carrito").Text.Contains("cancelado"), "La copia debia cancelarse");
                Exigir(!Hallar<ComboBox>(carrito, "Borradores existentes").Items.Cast<object>().Any(i => i.ToString()!.StartsWith($"#{original + 1} ")), "La copia cancelada no debia seguir en la lista de borradores");
                Elegir(Hallar<ComboBox>(carrito, "Borradores existentes"), $"#{original} ");
                await Presionar(carrito, "Cargar");
                await Presionar(carrito, "Validar pedido");
                Exigir(Hallar<Label>(carrito, "Resultado del carrito").Text.Contains("VALIDADO"), "El pedido debia validarse: " + Hallar<Label>(carrito, "Resultado del carrito").Text);
                pedido = int.Parse(Hallar<Label>(carrito, "Estado del borrador").Text.Split(' ')[0].TrimStart('#'));
                log.AppendLine($"Carrito: pedido #{pedido} creado, editado (deshacer/rehacer), duplicado y validado por botones");

                // Cobro: cobrar con confirmacion
                await principal.MostrarAsync(TipoVista.Cobro);
                var cobro = principal.Vistas.OfType<VistaCobro>().Single();
                Elegir(Hallar<ComboBox>(cobro, "Pedido a cobrar"), $"#{pedido} ");
                await Presionar(cobro, "Cobrar pedido");
                var resultadoCobro = Hallar<Label>(cobro, "Resultado del cobro").Text;
                Exigir(resultadoCobro.Contains("pagado") && resultadoCobro.Contains("95.00"), "El cobro debia aprobarse: " + resultadoCobro);
                Capturar(principal, carpeta, "07b_cobro_aprobado.png");
                log.AppendLine("Cobro: " + resultadoCobro);

                // Pedidos: filtro por estado y detalle
                await principal.MostrarAsync(TipoVista.Pedidos);
                var pedidos = principal.Vistas.OfType<VistaPedidos>().Single();
                Elegir(Hallar<ComboBox>(pedidos, "Filtrar por estado"), EstadosPedidoTexto.Pagado);
                await Pausa(500);
                Exigir(Hallar<DataGridView>(pedidos, "Pedidos").Rows.Count == 1, "El filtro Pagado debia mostrar 1 pedido");
                Capturar(principal, carpeta, "06b_pedidos_filtrado.png");
            }

            log.AppendLine("--- Recorrido por botones (rol BODEGA) ---");
            await IniciarSesion(app, "bodega", "Bodega123*");
            using (var principal = provider.GetRequiredService<FormPrincipal>())
            {
                principal.Show();
                await Pausa(400);
                await principal.MostrarAsync(TipoVista.Despacho);
                var despacho = principal.Vistas.OfType<VistaDespacho>().Single();
                Seleccionar(Hallar<DataGridView>(despacho, "Pedidos por despachar o entregar"), pedido);
                await Presionar(despacho, "Despachar pedido");
                var r1 = Hallar<Label>(despacho, "Resultado del despacho").Text;
                Exigir(r1.Contains("guia"), "El despacho debia generar guia: " + r1);
                Seleccionar(Hallar<DataGridView>(despacho, "Pedidos por despachar o entregar"), pedido);
                await Presionar(despacho, "Marcar entregado");
                Exigir(Hallar<Label>(despacho, "Resultado del despacho").Text.Contains("entregado"), "La entrega debia registrarse");
                log.AppendLine("Despacho: " + r1);
            }

            log.AppendLine("--- Recorrido por botones (rol VENTAS: devolucion y reportes) ---");
            await IniciarSesion(app, "ventas", "Ventas123*");
            using (var principal = provider.GetRequiredService<FormPrincipal>())
            {
                principal.Show();
                await Pausa(400);
                await principal.MostrarAsync(TipoVista.Devoluciones);
                var devoluciones = principal.Vistas.OfType<VistaDevoluciones>().Single();
                Seleccionar(Hallar<DataGridView>(devoluciones, "Pedidos entregados"), pedido);
                await Presionar(devoluciones, "Solicitar devolucion");
                var r2 = Hallar<Label>(devoluciones, "Resultado de la devolucion").Text;
                Exigir(r2.Contains("aceptada") && r2.Contains("90.00"), "La devolucion debia aceptarse con reembolso 90.00: " + r2);
                Seleccionar(Hallar<DataGridView>(devoluciones, "Pedidos entregados"), pedido);
                await Presionar(devoluciones, "Solicitar devolucion"); // repetida
                var r3 = Hallar<Label>(devoluciones, "Resultado de la devolucion").Text;
                Exigir(r3.Contains("rechazada") && r3.Contains("YA_DEVUELTO"), "La devolucion repetida debia rechazarse: " + r3);
                Capturar(principal, carpeta, "09b_devolucion_repetida_rechazada.png");
                log.AppendLine("Devolucion: " + r2);
                log.AppendLine("Devolucion repetida: " + r3);

                await principal.MostrarAsync(TipoVista.Reportes);
                var reportes = principal.Vistas.OfType<VistaReportes>().Single();
                Hallar<TextBox>(reportes, "Filtro de consulta").Text = "ESTADO = PAGADO AND";
                await Presionar(reportes, "Aplicar");
                var error = reportes.Controls.OfType<Control>().SelectMany(Descendientes).OfType<Label>().First(l => l.Text.StartsWith("Consulta no valida")).Text;
                Capturar(principal, carpeta, "11b_reportes_error_sintaxis.png");
                log.AppendLine("Reportes: " + error);
            }
        }
        finally
        {
            Ui.Ui.ConfirmadorAutomatico = null;
            Ui.Ui.AvisoAutomatico = null;
        }
    }

    private static IEnumerable<Control> Descendientes(Control raiz)
    {
        foreach (Control hijo in raiz.Controls)
        {
            yield return hijo;
            foreach (var nieto in Descendientes(hijo)) yield return nieto;
        }
    }

    private static T Hallar<T>(Control raiz, string accesible) where T : Control =>
        Descendientes(raiz).OfType<T>().FirstOrDefault(c => c.AccessibleName == accesible || c.Name == accesible)
        ?? throw new InvalidOperationException($"No se encontro el control '{accesible}' ({typeof(T).Name}).");

    private static void Elegir(ComboBox combo, string fragmento)
    {
        var indice = combo.Items.Cast<object>().ToList().FindIndex(i => i.ToString()!.Contains(fragmento, StringComparison.OrdinalIgnoreCase));
        if (indice < 0) throw new InvalidOperationException($"'{fragmento}' no esta en '{combo.AccessibleName}'.");
        combo.SelectedIndex = indice;
    }

    private static void Seleccionar(DataGridView grid, int pedidoId)
    {
        foreach (DataGridViewRow fila in grid.Rows)
            if (fila.DataBoundItem is PedidoResumenDto p && p.Id == pedidoId) { fila.Selected = true; grid.CurrentCell = fila.Cells[0]; return; }
        throw new InvalidOperationException($"El pedido #{pedidoId} no esta en '{grid.AccessibleName}'.");
    }

    /// <summary>Pulsa un boton real y espera a que termine la operacion asincrona de la pantalla.</summary>
    private static async Task Presionar(Control vista, string boton)
    {
        var b = Hallar<Button>(vista, boton);
        if (!b.Enabled) throw new InvalidOperationException($"El boton '{boton}' esta deshabilitado.");
        b.PerformClick();
        for (var i = 0; i < 250 && vista.UseWaitCursor; i++) await Task.Delay(20);
        await Pausa(120);
    }

    private static async Task IniciarSesion(Infraestructura.Aplicacion app, string usuario, string clave) =>
        Exigir(await app.EjecutarAsync<ServicioAutenticacion, bool>(s => s.IniciarSesionAsync(usuario, clave)), $"No se pudo iniciar sesion como {usuario}");

    private static async Task<int> Cliente(Infraestructura.Aplicacion app, string nombre) =>
        (await app.EjecutarAsync<ServicioClientes, IReadOnlyList<ClienteDto>>(s => s.ListarAsync(nombre))).Single(c => c.Nombre == nombre).Id;

    private static Task<int> Crear(Infraestructura.Aplicacion app, int cliente, TipoEntrega tipo, params (string Sku, int Cantidad)[] lineas) =>
        app.EjecutarAsync<ServicioPedidos, int>(s => s.CrearBorradorAsync(cliente, tipo, lineas.Select(l => new LineaSolicitada(l.Sku, l.Cantidad)).ToList()));

    private static void Exigir(bool condicion, string mensaje)
    {
        if (!condicion) throw new InvalidOperationException(mensaje);
    }

    private static async Task Pausa(int milisegundos = 250)
    {
        await Task.Delay(milisegundos);
        System.Windows.Forms.Application.DoEvents();
    }

    private static void Capturar(Form formulario, string carpeta, string archivo)
    {
        using var imagen = new Bitmap(formulario.ClientSize.Width, formulario.ClientSize.Height);
        formulario.DrawToBitmap(imagen, new Rectangle(Point.Empty, formulario.ClientSize));
        imagen.Save(Path.Combine(carpeta, archivo), ImageFormat.Png);
    }
}
