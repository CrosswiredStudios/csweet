using CSweet.Application.Setup;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CSweet.Infrastructure.Persistence;

public static class CSweetDatabaseInitializer
{
    public static async Task EnsureDatabaseReadyAsync(
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CSweetDbContext>();

        if (dbContext.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
        {
            await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        }
        else if (dbContext.Database.IsRelational())
        {
            await dbContext.Database.MigrateAsync(cancellationToken);
        }
        else
        {
            await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        }

        var setupService = scope.ServiceProvider.GetRequiredService<ISetupService>();
        await setupService.EnsureSeededAsync(cancellationToken);
    }
}
