using LogiMarket.Domain.Pedidos;

namespace LogiMarket.Application.Pedidos;

public static class Requerimientos
{
    /// <summary>Unidades por SKU final que exige un pedido (kits expandidos por la instantanea).</summary>
    public static IReadOnlyDictionary<string, int> PorSku(Pedido pedido) =>
        pedido.Detalles
            .SelectMany(d => d.Componentes.Select(c => (c.Sku, Cantidad: c.CantidadPorUnidad * d.Cantidad)))
            .GroupBy(x => x.Sku)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Cantidad));
}