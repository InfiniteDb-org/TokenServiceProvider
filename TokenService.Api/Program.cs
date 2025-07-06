using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TokenService.Api.Infrastructure.Extensions;


var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights()
    .AddTokenServiceDependencies(builder.Configuration)
    .AddTokenRepositoryDependencies(builder.Configuration);

var app = builder.Build();

var envName = Environment.GetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT");
await app.Services.EnsureCosmosContainersCreatedAsync(envName);

app.Run();

