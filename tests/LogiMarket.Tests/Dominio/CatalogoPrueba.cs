using LogiMarket.Domain.Catalogo;
using LogiMarket.Domain.Clientes;
using LogiMarket.Domain.Pedidos;

namespace LogiMarket.Tests.Dominio;

/// <summary>Catalogo oficial de la guia armado solo con el dominio (sin base de datos).</summary>
internal sealed class CatalogoPrueba
{
    public static readonly DateTime Ahora = new(2026, 11, 21, 12, 0, 0, DateTimeKind.Utc);
    public static readonly ModalidadEntrega Estandar = new(TipoEntrega.Estandar, 5.00m, 3);
    public static readonly ModalidadEntrega Expres = new(TipoEntrega.Expres, 12.00m, 1);

    public ProductoSimple P01 { get; } = new("P01", "Mouse USB", 20.00m, 0.20m, 8);
    public ProductoSimple P02 { get; } = new("P02", "Teclado", 30.00m, 0.70m, 5);
    public ProductoSimple P03 { get; } = new("P03", "Memoria USB", 10.00m, 0.10m, 20);
    public ProductoSimple P04 { get; } = new("P04", "Webcam", 40.00m, 0.35m, 4);
    public ProductoSimple P05 { get; } = new("P05", "Alfombrilla", 5.00m, 0.10m, 30);
    public KitCompuesto KitOficina { get; } = new("KIT_OFICINA", "Kit Oficina");
    public KitCompuesto KitTeletrabajo { get; } = new("KIT_TELETRABAJO", "Kit Teletrabajo");

    public Cliente Ana { get; } = new("Ana", "ana@example.test", CategoriaCliente.Vip);
    public Cliente Luis { get; } = new("Luis", "luis@example.test", CategoriaCliente.Normal);
    public Cliente Carmen { get; } = new("Carmen", "carmen@example.test", CategoriaCliente.Normal);

    public CatalogoPrueba()
    {
        KitOficina.Agregar(P01, 1);
        KitOficina.Agregar(P02, 1);
        KitTeletrabajo.Agregar(KitOficina, 1);
        KitTeletrabajo.Agregar(P03, 1);
    }
}