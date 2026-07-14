using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CSweet.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for EF Core CLI tools (migrations).
/// Uses a local placeholder connection string that is not opened during migration scaffolding.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<CSweetDbContext>
{
    public CSweetDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<CSweetDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Database=csweet;Username=postgres;Password=postgres");
        return new CSweetDbContext(optionsBuilder.Options);
    }
}
