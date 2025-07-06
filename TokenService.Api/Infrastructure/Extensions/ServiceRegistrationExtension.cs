using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TokenService.Api.Infrastructure.Database;
using TokenService.Api.Services;

namespace TokenService.Api.Infrastructure.Extensions;

public static class ServiceRegistrationExtension
{
    public static IServiceCollection AddTokenServiceDependencies(this IServiceCollection services, IConfiguration configuration)
    {
        // Add DbContext for Cosmos DB
        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseCosmos(
                configuration["CosmosDB:AccountEndpoint"] ?? throw new InvalidOperationException(),
                configuration["CosmosDB:AccountKey"] ?? throw new InvalidOperationException(),
                configuration["CosmosDB:DatabaseName"] ?? throw new InvalidOperationException()
            );
        });
        
        services.AddScoped<ITokenService, Services.TokenService>();
        services.AddScoped<Services.TokenService>();

        return services;
    }
}