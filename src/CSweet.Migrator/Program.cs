using CSweet.Application.Setup;
using CSweet.Infrastructure;
using CSweet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.AddCSweetInfrastructure();

using var host = builder.Build();
using var scope = host.Services.CreateScope();

var logger = scope.ServiceProvider
    .GetRequiredService<ILoggerFactory>()
    .CreateLogger("CSweet.Migrator");

try
{
    logger.LogInformation("Preparing C-Sweet database and setup seed data.");
    await CSweetDatabaseInitializer.EnsureDatabaseReadyAsync(host.Services);

    logger.LogInformation("C-Sweet database migration and setup seeding completed.");
    return 0;
}
catch (Exception ex)
{
    logger.LogCritical(ex, "C-Sweet database migration failed.");
    return 1;
}
