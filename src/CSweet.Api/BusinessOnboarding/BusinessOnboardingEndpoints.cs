using CSweet.Application.BusinessOnboarding;
using CSweet.Contracts.BusinessOnboarding;

namespace CSweet.Api.BusinessOnboarding;

public static class BusinessOnboardingEndpoints
{
    public static IEndpointRouteBuilder MapBusinessOnboardingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/business-onboarding");

        group.MapPost("/complete", async (
            CompleteBusinessOnboardingRequest request,
            IBusinessOnboardingService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.CompleteAsync(request, cancellationToken);
            return result.Succeeded
                ? Results.Ok(result.Onboarding)
                : Results.BadRequest(result);
        });

        return endpoints;
    }
}
