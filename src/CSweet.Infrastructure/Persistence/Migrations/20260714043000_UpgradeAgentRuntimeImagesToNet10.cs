using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CSweet.Infrastructure.Persistence.Migrations;

[DbContext(typeof(CSweetDbContext))]
[Migration("20260714043000_UpgradeAgentRuntimeImagesToNet10")]
public sealed class UpgradeAgentRuntimeImagesToNet10 : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            UPDATE "AgentRuntimeGlobalSettings"
            SET "DotNetBuilderImage" = 'mcr.microsoft.com/dotnet/sdk:10.0'
            WHERE "DotNetBuilderImage" = 'mcr.microsoft.com/dotnet/sdk:9.0';

            UPDATE "AgentRuntimeGlobalSettings"
            SET "DotNetRuntimeBaseImage" = 'mcr.microsoft.com/dotnet/runtime:10.0'
            WHERE "DotNetRuntimeBaseImage" = 'mcr.microsoft.com/dotnet/runtime:9.0';
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            UPDATE "AgentRuntimeGlobalSettings"
            SET "DotNetBuilderImage" = 'mcr.microsoft.com/dotnet/sdk:9.0'
            WHERE "DotNetBuilderImage" = 'mcr.microsoft.com/dotnet/sdk:10.0';

            UPDATE "AgentRuntimeGlobalSettings"
            SET "DotNetRuntimeBaseImage" = 'mcr.microsoft.com/dotnet/runtime:9.0'
            WHERE "DotNetRuntimeBaseImage" = 'mcr.microsoft.com/dotnet/runtime:10.0';
            """);
    }
}
