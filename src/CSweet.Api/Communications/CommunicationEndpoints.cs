using CSweet.Api.Auth;
using CSweet.Application.Communications;
using CSweet.Application.Core;
using CSweet.Contracts.Communications;

namespace CSweet.Api.Communications;

public static class CommunicationEndpoints
{
    public static IEndpointRouteBuilder MapCommunicationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/organizations/{organizationId:guid}/communications");
        group.AddEndpointFilter(async (context, next) =>
        {
            if (!Guid.TryParse(context.HttpContext.Request.RouteValues["organizationId"]?.ToString(), out var organizationId))
                return Results.NotFound();
            var memory = context.HttpContext.RequestServices.GetRequiredService<IAgentMemoryService>();
            return await memory.CanExploreAsync(organizationId, context.HttpContext.User.GetApplicationUserId(), context.HttpContext.RequestAborted)
                ? await next(context) : Results.Forbid();
        });

        group.MapGet("/discord", async (Guid organizationId, ICommunicationWorkspaceService service, CancellationToken cancellationToken) =>
            await service.GetDiscordAsync(organizationId, cancellationToken) is { } connection ? Results.Ok(connection) : Results.NotFound());

        group.MapGet("/providers/{providerKey}", async (Guid organizationId, string providerKey,
            ICommunicationWorkspaceService service, CancellationToken cancellationToken) =>
            await service.GetAsync(organizationId, providerKey, cancellationToken) is { } connection
                ? Results.Ok(connection) : Results.NotFound());

        group.MapPost("/providers/{providerKey}/connect", async (Guid organizationId, string providerKey,
            ConnectCommunicationWorkspaceRequest request, ICommunicationWorkspaceService service, CancellationToken cancellationToken) =>
        {
            try { return Results.Ok(await service.ConnectAsync(organizationId, providerKey, request, cancellationToken)); }
            catch (ArgumentException exception) { return Results.BadRequest(new CommunicationActionResponse(false, "validation_error", exception.Message)); }
        });

        group.MapGet("/providers/{providerKey}/provisioning-preview", async (Guid organizationId, string providerKey,
            ICommunicationWorkspaceService service, CancellationToken cancellationToken) =>
        {
            var plan = await service.PreviewAsync(organizationId, providerKey, cancellationToken);
            return plan is null ? Results.NotFound() : Results.Ok(new ProvisioningPreviewResponse(plan.OrganizationId, plan.Provider,
                plan.WorkspaceExternalId, plan.Changes.Select(x => new ProvisioningChangeResponse(x.Change.ToString(), x.Kind.ToString(),
                    x.Purpose, x.DesiredName, x.ExternalId, x.Detail)).ToList(), plan.CreatedAt));
        });

        group.MapPost("/providers/{providerKey}/reconcile", async (Guid organizationId, string providerKey,
            ICommunicationWorkspaceService service, CancellationToken cancellationToken) =>
            Results.Accepted(value: await service.QueueReconciliationAsync(organizationId, providerKey, cancellationToken)));

        group.MapDelete("/providers/{providerKey}", async (Guid organizationId, string providerKey,
            ICommunicationWorkspaceService service, CancellationToken cancellationToken) =>
        {
            var result = await service.DisconnectAsync(organizationId, providerKey, cancellationToken);
            return result.Succeeded ? Results.Accepted(value: result) : Results.BadRequest(result);
        });

        group.MapPost("/discord/connect", async (Guid organizationId, ConnectDiscordWorkspaceRequest request,
            ICommunicationWorkspaceService service, CancellationToken cancellationToken) =>
        {
            try { return Results.Ok(await service.ConnectDiscordAsync(organizationId, request, cancellationToken)); }
            catch (ArgumentException exception) { return Results.BadRequest(new CommunicationActionResponse(false, "validation_error", exception.Message)); }
        });

        group.MapGet("/discord/provisioning-preview", async (Guid organizationId, ICommunicationWorkspaceService service, CancellationToken cancellationToken) =>
        {
            var plan = await service.PreviewAsync(organizationId, cancellationToken);
            return plan is null ? Results.NotFound() : Results.Ok(new ProvisioningPreviewResponse(plan.OrganizationId, plan.Provider,
                plan.WorkspaceExternalId, plan.Changes.Select(x => new ProvisioningChangeResponse(x.Change.ToString(), x.Kind.ToString(),
                    x.Purpose, x.DesiredName, x.ExternalId, x.Detail)).ToList(), plan.CreatedAt));
        });

        group.MapPost("/discord/reconcile", async (Guid organizationId, ICommunicationWorkspaceService service, CancellationToken cancellationToken) =>
            Results.Accepted(value: await service.QueueReconciliationAsync(organizationId, cancellationToken)));

        group.MapDelete("/discord", async (Guid organizationId, ICommunicationWorkspaceService service, CancellationToken cancellationToken) =>
        {
            var result = await service.DisconnectDiscordAsync(organizationId, cancellationToken);
            return result.Succeeded ? Results.Accepted(value: result) : Results.BadRequest(result);
        });

        group.MapPost("/discord/link-code", async (Guid organizationId, HttpContext http,
            ICommunicationWorkspaceService service, CancellationToken cancellationToken) =>
            await service.CreateLinkCodeAsync(organizationId, http.User.GetApplicationUserId()!.Value, cancellationToken) is { } code
                ? Results.Ok(code) : Results.NotFound());

        group.MapPost("/discord/direct-agent", async (Guid organizationId, SelectDirectAgentRequest request, HttpContext http,
            ICommunicationWorkspaceService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.SelectDirectAgentAsync(organizationId, http.User.GetApplicationUserId()!.Value, request.AgentOrganizationUserId, cancellationToken)));

        group.MapGet("/notifications", async (Guid organizationId, HttpContext http, INotificationService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.ListAsync(organizationId, http.User.GetApplicationUserId()!.Value, cancellationToken)));
        group.MapPost("/notifications/{notificationId:guid}/read", async (Guid organizationId, Guid notificationId, HttpContext http,
            INotificationService service, CancellationToken cancellationToken) =>
            await service.MarkReadAsync(organizationId, http.User.GetApplicationUserId()!.Value, notificationId, cancellationToken) ? Results.NoContent() : Results.NotFound());
        return endpoints;
    }
}
