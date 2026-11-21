using LogiMarket.Application.Puertos;

namespace LogiMarket.Infrastructure.Sistema;

public sealed class RelojSistema : IClock
{
    public DateTime UtcAhora => DateTime.UtcNow;
}