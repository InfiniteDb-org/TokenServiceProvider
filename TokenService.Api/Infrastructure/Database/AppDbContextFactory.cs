using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace TokenService.Api.Infrastructure.Database;

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("local.settings.json", optional: false, reloadOnChange: true)
            .AddJsonFile("local.settings.development.json", optional: true)
            .Build();

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        
        // Default: SqlServer locally, Cosmos DB on Azure
        var useCosmosDb = configuration.GetValue("UseCosmosDb", false);
        
        if (useCosmosDb)
        {
            // use Cosmos DB on Azure
            var cosmosConnectionString = configuration.GetConnectionString("CosmosDb")
                                         ?? throw new InvalidOperationException("Connection string 'CosmosDb' not found.");
            var databaseName = configuration["CosmosDB:Database"] 
                               ?? throw new InvalidOperationException("CosmosDB:Database not found in configuration.");
                
            optionsBuilder.UseCosmos(
                connectionString: cosmosConnectionString,
                databaseName: databaseName);
        }
        else
        {
            // use SqlServer locally
            var sqlConnectionString = configuration.GetConnectionString("SqlServer")
                                      ?? throw new InvalidOperationException("Connection string 'SqlServer' not found.");
                
            optionsBuilder.UseSqlServer(sqlConnectionString);
        }
        
        return new AppDbContext(optionsBuilder.Options);
    }
}