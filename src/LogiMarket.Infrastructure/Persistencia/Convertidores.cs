using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace LogiMarket.Infrastructure.Persistencia;

public sealed class DecimalACentavosConverter : ValueConverter<decimal, long>
{
    public DecimalACentavosConverter()
        : base(v => (long)Math.Round(v * 100m, 0, MidpointRounding.AwayFromZero), v => v / 100m) { }
}

public sealed class DecimalAGramosConverter : ValueConverter<decimal, long>
{
    public DecimalAGramosConverter()
        : base(v => (long)Math.Round(v * 1000m, 0, MidpointRounding.AwayFromZero), v => v / 1000m) { }
}

public sealed class FechaUtcConverter : ValueConverter<DateTime, DateTime>
{
    public FechaUtcConverter()
        : base(v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(), v => DateTime.SpecifyKind(v, DateTimeKind.Utc)) { }
}
