using CSweet.Application.Setup;
using CSweet.Infrastructure.Persistence;
using CSweet.Infrastructure.Setup;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CSweet.Infrastructure;

public static class DependencyInjection
{
    public static IHostApplicationBuilder AddCSweetInfrastructure(this IHostApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString("Postgres")
            ?? builder.Configuration.GetConnectionString("csweet");

        builder.Services.AddDbContext<CSweetDbContext>(options =>
        {
            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                options.UseNpgsql(connectionString);
                return;
            }

            if (builder.Environment.IsProduction())
            {
                throw new InvalidOperationException("ConnectionStrings:Postgres must be configured in production.");
            }

            options.UseInMemoryDatabase("CSweetDevelopment");
        });

        builder.Services.AddScoped<ISetupService, SetupService>();
        builder.Services.AddScoped<IAuditEventWriter, AuditEventWriter>();

        return builder;
    }
}
