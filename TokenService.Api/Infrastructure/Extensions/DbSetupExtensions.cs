using Microsoft.Extensions.DependencyInjection;

namespace TokenService.Api.Infrastructure.Extensions;

public static class DbSetupExtensions
{
    public static async Task EnsureCosmosContainersCreatedAsync(this IServiceProvider services, string? environmentName)
    {
        if (environmentName != "Development" && environmentName != "Staging")
            return;

        try
        {
            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<Database.AppDbContext>();
            await db.Database.EnsureCreatedAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[EnsureCosmosContainersCreatedAsync] Exception: {ex}");
        }
    }
    
}