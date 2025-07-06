using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace TokenService.Api.Infrastructure.Extensions;

public static class RepositoryRegistrationExtension
{
    public static IServiceCollection AddTokenRepositoryDependencies(this IServiceCollection services, IConfiguration configuration)
    {
        // services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();

        return services;
    }
}