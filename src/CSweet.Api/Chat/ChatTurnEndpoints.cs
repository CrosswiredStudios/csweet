using System.Text.Json;
using CSweet.Application.Core;
using CSweet.Api.Auth;
using CSweet.Contracts.Core;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;

namespace CSweet.Api.Chat;

public static class ChatTurnEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static IEndpointRouteBuilder MapChatTurnEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/core/organizations/{organizationId:guid}");
        group.AddEndpointFilter(async (context, next) =>
        {
            if (!Guid.TryParse(context.HttpContext.Request.RouteValues["organizationId"]?.ToString(), out var organizationId))
                return Results.NotFound();
            var memory = context.HttpContext.RequestServices.GetRequiredService<IAgentMemoryService>();
            return await memory.CanExploreAsync(organizationId, context.HttpContext.User.GetApplicationUserId(), context.HttpContext.RequestAborted)
                ? await next(context)
                : Results.Forbid();
        });
        group.MapPost("/conversations/{conversationId:guid}/turns", StartAsync);
        group.MapGet("/turns/{turnId:guid}", GetAsync);
        group.MapGet("/turns/{turnId:guid}/events", StreamEventsAsync);
        group.MapGet("/turns/{turnId:guid}/trace", async (Guid organizationId, Guid turnId, long? afterSequence, IChatTurnService turns, CancellationToken cancellationToken) =>
        {
            var turn = await turns.GetAsync(organizationId, turnId, cancellationToken);
            return turn is null ? Results.NotFound() : Results.Ok(await turns.ListEventsAsync(organizationId, turnId, afterSequence ?? -1, cancellationToken));
        });
        group.MapPost("/turns/{turnId:guid}/retry", RetryAsync);
        group.MapPost("/turns/{turnId:guid}/cancel", CancelAsync);
        return endpoints;
    }

    private static async Task<IResult> StartAsync(Guid organizationId, Guid conversationId, StartChatTurnRequest request, IChatTurnService turns, CancellationToken cancellationToken)
    {
        try
        {
            var result = await turns.StartAsync(organizationId, conversationId, request.Message, cancellationToken: cancellationToken);
            return result is null ? Results.NotFound() : Results.Accepted($"/api/core/organizations/{organizationId}/turns/{result.Turn.Id}", result);
        }
        catch (InvalidOperationException exception)
        {
            return Results.Conflict(new { error = exception.Message });
        }
    }

    private static async Task<IResult> GetAsync(Guid organizationId, Guid turnId, IChatTurnService turns, CancellationToken cancellationToken)
    {
        var turn = await turns.GetAsync(organizationId, turnId, cancellationToken);
        return turn is null ? Results.NotFound() : Results.Ok(turn);
    }

    private static async Task<IResult> RetryAsync(Guid organizationId, Guid turnId, IChatTurnService turns, CancellationToken cancellationToken)
    {
        var result = await turns.RetryAsync(organizationId, turnId, cancellationToken);
        return result is null ? Results.Conflict(new { error = "Only failed, cancelled, or warning turns can be retried." })
            : Results.Accepted($"/api/core/organizations/{organizationId}/turns/{result.Turn.Id}", result);
    }

    private static async Task<IResult> CancelAsync(
        Guid organizationId, Guid turnId, IChatTurnService turns, IChatTurnEventRouter router, CancellationToken cancellationToken)
    {
        var snapshot = await turns.GetAsync(organizationId, turnId, cancellationToken);
        if (snapshot is null || IsTerminal(snapshot.Status)) return Results.NotFound();
        var traceEvent = await turns.TraceAsync(turnId, "system", "turn.cancelled", "cancelled", "Turn cancelled",
            "The user cancelled this turn.", cancellationToken: cancellationToken);
        router.Publish(traceEvent);
        return await turns.CancelAsync(organizationId, turnId, cancellationToken) ? Results.Accepted() : Results.NotFound();
    }

    private static async Task StreamEventsAsync(
        Guid organizationId, Guid turnId, long? afterSequence, HttpContext http,
        IChatTurnService turns, IChatTurnEventRouter router, IOptions<ChatTurnOptions> options, CancellationToken cancellationToken)
    {
        var snapshot = await turns.GetAsync(organizationId, turnId, cancellationToken);
        if (snapshot is null) { http.Response.StatusCode = StatusCodes.Status404NotFound; return; }
        http.Response.ContentType = "text/event-stream";
        http.Response.Headers.CacheControl = "no-cache, no-transform";
        http.Response.Headers["X-Accel-Buffering"] = "no";
        http.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();
        await http.Response.StartAsync(cancellationToken);

        var cursor = afterSequence ?? ParseLastEventId(http.Request.Headers["Last-Event-ID"].FirstOrDefault());
        var reader = router.Subscribe(turnId);
        foreach (var traceEvent in await turns.ListEventsAsync(organizationId, turnId, cursor, cancellationToken))
        {
            await WriteAsync(http, traceEvent, cancellationToken);
            cursor = traceEvent.Sequence;
        }
        snapshot = await turns.GetAsync(organizationId, turnId, cancellationToken);
        if (snapshot is null || IsTerminal(snapshot.Status)) return;

        while (!cancellationToken.IsCancellationRequested)
        {
            var wait = reader.WaitToReadAsync(cancellationToken).AsTask();
            var completed = await Task.WhenAny(wait, Task.Delay(options.Value.StreamHeartbeatInterval, cancellationToken));
            if (completed != wait)
            {
                await http.Response.WriteAsync(": heartbeat\n\n", cancellationToken);
                await http.Response.Body.FlushAsync(cancellationToken);
                snapshot = await turns.GetAsync(organizationId, turnId, cancellationToken);
                if (snapshot is null || IsTerminal(snapshot.Status)) return;
                continue;
            }
            if (!await wait) return;
            while (reader.TryRead(out var traceEvent))
            {
                if (traceEvent.Sequence <= cursor) continue;
                await WriteAsync(http, traceEvent, cancellationToken);
                cursor = traceEvent.Sequence;
            }
        }
    }

    private static async Task WriteAsync(HttpContext http, ChatTurnTraceEventResponse traceEvent, CancellationToken cancellationToken)
    {
        await http.Response.WriteAsync($"id: {traceEvent.Sequence}\nevent: trace\ndata: {JsonSerializer.Serialize(traceEvent, JsonOptions)}\n\n", cancellationToken);
        await http.Response.Body.FlushAsync(cancellationToken);
    }

    private static long ParseLastEventId(string? value) => long.TryParse(value, out var parsed) ? parsed : -1;
    private static bool IsTerminal(string status) => status is "Completed" or "CompletedWithWarnings" or "Failed" or "Cancelled";
}
