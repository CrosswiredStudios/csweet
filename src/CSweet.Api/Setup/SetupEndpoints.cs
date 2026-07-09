using CSweet.Application.Setup;
using CSweet.AI.Providers;
using CSweet.Contracts.Llm;

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
