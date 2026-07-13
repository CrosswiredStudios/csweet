using CSweet.Application.Core;
using CSweet.Contracts.Core;

namespace CSweet.Api.Core;

public static class ConversationEndpoints
{
    public static IEndpointRouteBuilder MapConversationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/core/organizations/{organizationId:guid}/conversations");

        group.MapPost("", async (
            Guid organizationId,
            StartConversationRequest request,
            IConversationService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.StartAsync(organizationId, request, cancellationToken);
            return result.Succeeded
                ? Results.Created($"/api/core/organizations/{organizationId}/conversations/{result.Conversation!.Id}", result.Conversation)
                : Results.BadRequest(result);
        });

        group.MapGet("/{conversationId:guid}", async (
            Guid conversationId,
            IConversationService service,
            CancellationToken cancellationToken) =>
        {
            var conversation = await service.GetAsync(conversationId, cancellationToken);
            return conversation is null ? Results.NotFound() : Results.Ok(conversation);
        });

        group.MapGet("/{conversationId:guid}/messages", async (
            Guid conversationId,
            IConversationService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.ListMessagesAsync(conversationId, cancellationToken)));

        return endpoints;
    }
}
