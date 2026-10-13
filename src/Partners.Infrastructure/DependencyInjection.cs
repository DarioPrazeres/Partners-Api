using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Partners.Core.Persistence;
using Partners.Infrastructure.Persistence;

namespace Partners.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddPartnersInfrastructure(
        this IServiceCollection services, Func<IServiceProvider, string> connectionString)
    {
        services.AddDbContext<PartnersDbContext>((sp, options) =>
            options.UseSqlServer(connectionString(sp)));

        services.AddScoped<IPartnerRepository, EfPartnerRepository>();
        return services;
    }
}