using CSweet.Application.BusinessOnboarding;
using CSweet.Contracts.BusinessOnboarding;
using CSweet.Api.Auth;
using System.Security.Claims;

namespace CSweet.Api.BusinessOnboarding;

public static class BusinessOnboardingEndpoints
{
    public static IEndpointRouteBuilder MapBusinessOnboardingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/business-onboarding");

        group.MapPost("/complete", async (
            CompleteBusinessOnboardingRequest request,
            ClaimsPrincipal principal,
            IBusinessOnboardingService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.CompleteAsync(request, cancellationToken, principal.GetApplicationUserId());
            return result.Succeeded
                ? Results.Ok(result.Onboarding)
                : Results.BadRequest(result);
        });

        group.MapPost("/{organizationId:guid}/chief", async (
            Guid organizationId,
            CompleteChiefSetupRequest request,
            IBusinessOnboardingService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.AssignChiefAsync(organizationId, request, cancellationToken);
            return result.Succeeded ? Results.Ok(result.Setup) : Results.BadRequest(result);
        });

        return endpoints;
    }
}
