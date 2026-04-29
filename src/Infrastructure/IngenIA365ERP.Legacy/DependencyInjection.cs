using IngenIA365ERP.Legacy.Adapters;
using Microsoft.Extensions.DependencyInjection;

namespace IngenIA365ERP.Legacy;

public static class DependencyInjection
{
    public static IServiceCollection AddLegacyServices(this IServiceCollection services)
    {
        services.AddScoped<ILegacyAdapter, AccountingLegacyAdapter>();
        services.AddScoped<AccountingLegacyAdapter>();
        services.AddScoped<LendingLegacyAdapter>();

        return services;
    }
}
