using CSweet.Application.Setup;
using CSweet.AI.Providers;
using CSweet.Contracts.Llm;
using CSweet.Contracts.Setup;
using CSweet.Api.Auth;
using System.Security.Claims;

namespace CSweet.Api.Setup;

public static class SetupEndpoints
{
    public static IEndpointRouteBuilder MapSetupEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/setup");

        group.MapGet("/status", async (ISetupService setupService, CancellationToken cancellationToken) =>
            Results.Ok(await setupService.GetStatusAsync(cancellationToken)));

        group.MapPost("/steps/{key}/complete", async (string key, ISetupService setupService, CancellationToken cancellationToken) =>
        {
            var result = await setupService.CompleteStepAsync(key, cancellationToken);
            return result.Succeeded
                ? Results.Ok(result)
                : Results.BadRequest(result);
        });

        group.MapPost("/complete", async (ISetupService setupService, CancellationToken cancellationToken) =>
        {
            var result = await setupService.CompleteFirstRunAsync(cancellationToken);
            return result.Succeeded
                ? Results.Ok(result)
                : Results.BadRequest(result);
        });

        group.MapGet("/email-delivery", async (
            IEmailDeliverySettingsService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.GetAsync(cancellationToken)));

        group.MapGet("/communications/options", (IConfiguration configuration) =>
        {
            var installUrl = configuration["Communications:Discord:InstallUrl"]
                ?? configuration["Communications:Relay:PublicInstallUrl"];
            return Results.Ok(new CommunicationSetupOptionsResponse(installUrl, !string.IsNullOrWhiteSpace(installUrl)));
        });

        group.MapPut("/email-delivery", async (
            UpdateEmailDeliverySettingsRequest request,
            IEmailDeliverySettingsService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.UpdateAsync(request, cancellationToken);
            return result.Succeeded ? Results.Ok(result) : Results.BadRequest(result);
        });

        group.MapPost("/email-delivery/test", async (
            ClaimsPrincipal principal,
            IEmailDeliverySettingsService service,
            CancellationToken cancellationToken) =>
        {
            var userId = principal.GetApplicationUserId();
            if (!userId.HasValue) return Results.Unauthorized();
            var result = await service.TestAsync(userId.Value, cancellationToken);
            return result.Succeeded ? Results.Ok(result) : Results.BadRequest(result);
        });

        group.MapPost("/default-chat-provider", async (
            SetDefaultChatProviderRequest request,
            ILlmProviderProfileService providerService,
            CancellationToken cancellationToken) =>
        {
            var result = await providerService.SetDefaultChatProviderAsync(request.ProviderProfileId, cancellationToken);
            return result.Succeeded
                ? Results.Ok(result)
                : Results.BadRequest(result);
        });

        return endpoints;
    }
}
