using CSweet.Application.Setup;
using CSweet.Contracts.Agents;

namespace CSweet.Api.Agents;

public static class PluginManagementEndpoints
{
    public static IEndpointRouteBuilder MapPluginManagementEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/plugins");

        group.MapPost("/imports/preview", async (PreviewAgentImportRequest request,
            IPluginImportService imports, CancellationToken cancellationToken) =>
        {
            try { return Results.Ok(await imports.PreviewAsync(request, cancellationToken)); }
            catch (AgentImportPreviewException exception) { return Results.BadRequest(new { error = exception.Message }); }
        }).RequireRateLimiting(AgentRateLimiting.ImportPolicy);

        group.MapPost("/imports/{importId:guid}/install", async (Guid importId, InstallAgentRequest request,
            IPluginInstallationService installations, CancellationToken cancellationToken) =>
        {
            try { return Results.Ok(await installations.InstallAsync(importId, request, cancellationToken)); }
            catch (AgentInstallationException exception) { return Results.BadRequest(new { error = exception.Message }); }
        }).RequireRateLimiting(AgentRateLimiting.BuildPolicy);

        group.MapGet("/installations", async (IPluginInstallationService installations, CancellationToken cancellationToken) =>
            Results.Ok(await installations.ListAsync(cancellationToken)));
        group.MapGet("/installations/{installationId:guid}", async (Guid installationId,
            IPluginInstallationService installations, CancellationToken cancellationToken) =>
            await installations.GetAsync(installationId, cancellationToken) is { } value ? Results.Ok(value) : Results.NotFound());
        group.MapPost("/installations/{installationId:guid}/enable", async (Guid installationId,
            IPluginInstallationService installations, CancellationToken cancellationToken) =>
            Results.Ok(await installations.EnableAsync(installationId, cancellationToken)));
        group.MapPost("/installations/{installationId:guid}/disable", async (Guid installationId,
            IPluginInstallationService installations, CancellationToken cancellationToken) =>
            Results.Ok(await installations.DisableAsync(installationId, cancellationToken)));
        group.MapPost("/installations/{installationId:guid}/update", async (Guid installationId,
            UpdateAgentInstallationRequest request, IPluginInstallationService installations, CancellationToken cancellationToken) =>
            Results.Ok(await installations.UpdateAsync(installationId, request, cancellationToken)));
        group.MapGet("/installations/{installationId:guid}/runs", async (Guid installationId,
            IPluginInstallationService installations, CancellationToken cancellationToken) =>
            Results.Ok(await installations.ListRunsAsync(installationId, cancellationToken)));
        group.MapGet("/installations/{installationId:guid}/build-log", async (Guid installationId,
            IPluginInstallationService installations, CancellationToken cancellationToken) =>
            await installations.GetBuildLogAsync(installationId, cancellationToken) is { } value ? Results.Ok(value) : Results.NotFound());
        group.MapDelete("/installations/{installationId:guid}", async (Guid installationId,
            IPluginInstallationService installations, CancellationToken cancellationToken) =>
            Results.Ok(await installations.RemoveAsync(installationId, cancellationToken)));

        group.MapPut("/installations/{installationId:guid}/secrets/{key}", async (Guid installationId, string key,
            SetPluginSecretRequest request, IPluginSecretStore secrets, CancellationToken cancellationToken) =>
        {
            await secrets.SetAsync(installationId, key, request.Value, cancellationToken);
            return Results.NoContent();
        });
        group.MapDelete("/installations/{installationId:guid}/secrets/{key}", async (Guid installationId, string key,
            IPluginSecretStore secrets, CancellationToken cancellationToken) =>
        {
            await secrets.RemoveAsync(installationId, key, cancellationToken);
            return Results.NoContent();
        });

        group.MapGet("/installations/{installationId:guid}/organizations", async (Guid installationId,
            IPluginOrganizationGrantService grants, CancellationToken cancellationToken) =>
            Results.Ok(await grants.ListAsync(installationId, cancellationToken)));
        group.MapPost("/installations/{installationId:guid}/organizations", async (Guid installationId,
            GrantPluginOrganizationRequest request, IPluginOrganizationGrantService grants, CancellationToken cancellationToken) =>
        {
            await grants.GrantAsync(installationId, request.OrganizationId, cancellationToken);
            return Results.NoContent();
        });
        group.MapDelete("/installations/{installationId:guid}/organizations/{organizationId:guid}", async (
            Guid installationId, Guid organizationId, IPluginOrganizationGrantService grants, CancellationToken cancellationToken) =>
        {
            await grants.RevokeAsync(installationId, organizationId, cancellationToken);
            return Results.NoContent();
        });

        return endpoints;
    }
}
