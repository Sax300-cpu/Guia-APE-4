using LogiMarket.Domain.Catalogo;
using LogiMarket.Domain.Common;

namespace LogiMarket.Tests.Dominio;

public class CatalogoTests
{
    private readonly CatalogoPrueba _c = new();

    [Fact] // P18 Composite: precios oficiales
    public void P18_Composite_kits_tienen_precio_50_y_60()
    {
        Assert.Equal(50.00m, _c.KitOficina.Precio);
        Assert.Equal(60.00m, _c.KitTeletrabajo.Precio);
    }

    [Fact] // P18 Composite: ciclos rechazados
    public void P18_Composite_rechaza_ciclos_directos_e_indirectos()
    {
        Assert.Throws<DomainException>(() => _c.KitOficina.Agregar(_c.KitOficina, 1));
        Assert.Throws<DomainException>(() => _c.KitOficina.Agregar(_c.KitTeletrabajo, 1));
    }

    [Fact] // P27 Iterator
    public void P27_Iterator_recorre_kit_anidado_con_cantidades_y_termina()
    {
        using var it = _c.KitTeletrabajo.CrearIterador(cantidad: 2);
        var visitados = new List<(string, int)>();
        while (it.MoveNext()) visitados.Add((it.Current.Producto.Sku, it.Current.Cantidad));

        Assert.Equal(new[] { ("P01", 2), ("P02", 2), ("P03", 2) }, visitados);
        Assert.False(it.MoveNext());
    }

    [Fact] // P34 Visitor: desglose y peso
    public void P34_Visitor_desglosa_skus_y_pesa_1kg()
    {
        var desglose = new DesgloseFisicoVisitor();
        var peso = new PesoLogisticoVisitor();
        _c.KitTeletrabajo.Aceptar(desglose);
        _c.KitTeletrabajo.Aceptar(peso);

        Assert.Equal(new[] { "P01", "P02", "P03" }, desglose.PorSku.Keys);
        Assert.All(desglose.PorSku.Values, q => Assert.Equal(1, q));
        Assert.Equal(1.00m, peso.PesoTotalKg);
    }

    [Fact]
    public void Producto_no_permite_stock_negativo_ni_precio_invalido()
    {
        Assert.Throws<DomainException>(() => _c.P02.Descontar(6));
        Assert.Throws<DomainException>(() => new ProductoSimple("PX", "Malo", 0m, 0.1m, 1));
        Assert.Throws<DomainException>(() => new ProductoSimple("PX", "Malo", 1m, -0.1m, 1));
        Assert.Throws<DomainException>(() => new ProductoSimple("PX", "Malo", 1m, 0.1m, -1));
    }
}