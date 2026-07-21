using System.Text.Json;
using System.Text.Json.Nodes;
using CSweet.Agent.Contracts.Grpc;
using CSweet.Infrastructure.Persistence;
using Google.Protobuf;
using Microsoft.EntityFrameworkCore;
using CSweet.Application.Setup;

namespace CSweet.AgentHost.Broker;

public static class McpGatewayEndpoints
{
    private const string ProtocolVersion = "2025-06-18";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static IEndpointRouteBuilder MapCSweetMcpGateway(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/.well-known/oauth-protected-resource/mcp", (HttpContext http) => Results.Ok(new
        {
            resource = $"{http.Request.Scheme}://{http.Request.Host}/mcp",
            bearer_methods_supported = new[] { "header" },
            resource_documentation = $"{http.Request.Scheme}://{http.Request.Host}/mcp"
        }));
        endpoints.MapPost("/mcp", HandlePostAsync).DisableAntiforgery();
        endpoints.MapGet("/mcp", () => Results.Json(new
        {
            service = "CSweet MCP Gateway",
            transport = "streamable-http",
            protocolVersion = ProtocolVersion
        }));
        endpoints.MapDelete("/mcp", () => Results.NoContent());
        return endpoints;
    }

    private static async Task<IResult> HandlePostAsync(
        HttpContext http,
        AgentSessionRegistry sessions,
        McpToolCatalog catalog,
        IPlatformCapabilityDispatcher dispatcher,
        CSweetDbContext db,
        IAuditEventWriter audit,
        IAuditExecutionContextAccessor auditContext,
        CancellationToken cancellationToken)
    {
        if (http.Request.ContentLength is > 1_048_576)
        {
            var oversizedToken = ReadBearerToken(http.Request.Headers.Authorization);
            var oversizedSession = oversizedToken is null ? null : sessions.FindByMcpAccessToken(oversizedToken);
            await audit.AppendAsync(new AuditEventWriteRequest(
                "mcp.request.rejected", "Mcp", "Inbound", "Rejected",
                oversizedSession is null ? null : BrokerAuditIdentity.OrganizationId(oversizedSession),
                "McpRequest", Summary: "An MCP request exceeded 1 MiB.",
                Actor: oversizedSession is null
                    ? new AuditActor("Unknown", false, RemotePeer: http.Connection.RemoteIpAddress?.ToString())
                    : BrokerAuditIdentity.Actor(oversizedSession),
                ErrorCode: "request_too_large"), cancellationToken);
            return Results.Json(Error(null, -32600, "The MCP request exceeds 1 MiB."), statusCode: StatusCodes.Status413PayloadTooLarge);
        }
        var token = ReadBearerToken(http.Request.Headers.Authorization);
        var session = token is null ? null : sessions.FindByMcpAccessToken(token);
        if (session is null)
        {
            await audit.AppendAsync(new AuditEventWriteRequest(
                "mcp.authentication", "Mcp", "Inbound", "Denied",
                Summary: "An MCP request did not present a valid live-session token.",
                Actor: new AuditActor("Unknown", false, RemotePeer: http.Connection.RemoteIpAddress?.ToString()),
                ErrorCode: "invalid_mcp_token"), cancellationToken);
            return Results.Json(Error(null, -32001, "A live broker session and valid MCP access token are required."), statusCode: StatusCodes.Status401Unauthorized);
        }
        if (!session.TryBeginMcpCall(DateTimeOffset.UtcNow))
        {
            await audit.AppendAsync(new AuditEventWriteRequest(
                "mcp.rate-limit", "Mcp", "Inbound", "Denied", BrokerAuditIdentity.OrganizationId(session),
                "McpSession", Actor: BrokerAuditIdentity.Actor(session), ErrorCode: "mcp_rate_limited"), cancellationToken);
            return Results.Json(Error(null, -32029, "The MCP session rate limit was exceeded."), statusCode: StatusCodes.Status429TooManyRequests);
        }

        JsonDocument document;
        var (rawBody, bodyTooLarge) = await ReadLimitedBodyAsync(
            http.Request.Body,
            1_048_576,
            cancellationToken);
        if (bodyTooLarge)
        {
            await audit.AppendAsync(new AuditEventWriteRequest(
                "mcp.request.rejected", "Mcp", "Inbound", "Rejected", BrokerAuditIdentity.OrganizationId(session),
                "McpRequest", Actor: BrokerAuditIdentity.Actor(session), ErrorCode: "request_too_large"), cancellationToken);
            return Results.Json(Error(null, -32600, "The MCP request exceeds 1 MiB."), statusCode: StatusCodes.Status413PayloadTooLarge);
        }
        try { document = JsonDocument.Parse(rawBody); }
        catch (JsonException)
        {
            await audit.AppendAsync(new AuditEventWriteRequest(
                "mcp.request.invalid-json", "Mcp", "Inbound", "Rejected", BrokerAuditIdentity.OrganizationId(session),
                "McpRequest", Actor: BrokerAuditIdentity.Actor(session), ContentType: "application/json",
                Payload: rawBody, ErrorCode: "invalid_json"), cancellationToken);
            return Results.Json(Error(null, -32700, "Invalid JSON."), statusCode: StatusCodes.Status400BadRequest);
        }

        using (document)
        {
            var root = document.RootElement;
            var id = root.TryGetProperty("id", out var idValue) ? idValue.Clone() : (JsonElement?)null;
            if (!root.TryGetProperty("method", out var methodElement) || methodElement.ValueKind != JsonValueKind.String)
            {
                await audit.AppendAsync(new AuditEventWriteRequest(
                    "mcp.request.missing-method", "Mcp", "Inbound", "Rejected",
                    BrokerAuditIdentity.OrganizationId(session), "McpRequest", ExternalRequestId: id?.ToString(),
                    Actor: BrokerAuditIdentity.Actor(session), ContentType: "application/json", Payload: rawBody,
                    ErrorCode: "method_required"), cancellationToken);
                return Results.Json(Error(id, -32600, "A JSON-RPC method is required."), statusCode: StatusCodes.Status400BadRequest);
            }

            var method = methodElement.GetString();
            var requestAuditId = await audit.AppendAsync(new AuditEventWriteRequest(
                "mcp.request", "Mcp", "Inbound", "Received", BrokerAuditIdentity.OrganizationId(session),
                "McpRequest", Summary: $"Agent invoked MCP method {method}.", ExternalRequestId: id?.ToString(),
                Actor: BrokerAuditIdentity.Actor(session), ContentType: "application/json", Payload: rawBody), cancellationToken);
            if (id is null && method?.StartsWith("notifications/", StringComparison.Ordinal) == true)
            {
                await AuditMcpResultAsync(audit, session, requestAuditId, method, "Accepted", cancellationToken);
                return Results.Accepted();
            }

            http.Response.Headers["Mcp-Session-Id"] = session.SessionId;
            using var contextScope = auditContext.Push(new AuditExecutionContext(
                BrokerAuditIdentity.OrganizationId(session), BrokerAuditIdentity.Actor(session), requestAuditId, Guid.NewGuid()));
            var response = method switch
            {
                "initialize" => Results.Json(Success(id, new
                {
                    protocolVersion = ProtocolVersion,
                    capabilities = new { tools = new { listChanged = false } },
                    serverInfo = new { name = "csweet-broker", version = "1.0.0" }
                })),
                "ping" => Results.Json(Success(id, new { })),
                "tools/list" => await ListToolsAsync(id, session, catalog, db, cancellationToken),
                "tools/call" => await CallToolAsync(id, root, session, catalog, dispatcher, db, audit, requestAuditId, cancellationToken),
                _ => Results.Json(Error(id, -32601, $"Method '{method}' is not supported."), statusCode: StatusCodes.Status404NotFound)
            };
            await AuditMcpResultAsync(audit, session, requestAuditId, method,
                method is "initialize" or "ping" or "tools/list" or "tools/call" ? "Completed" : "Rejected",
                cancellationToken);
            return response;
        }
    }

    private static async Task<IResult> ListToolsAsync(
        JsonElement? id,
        AgentSession session,
        McpToolCatalog catalog,
        CSweetDbContext db,
        CancellationToken cancellationToken)
    {
        var grants = await ReadCurrentGrantsAsync(session, db, cancellationToken);
        if (grants is null)
            return Results.Json(Error(id, -32002, "The installation grant is no longer active."), statusCode: StatusCodes.Status403Forbidden);

        var tools = catalog.List(grants).Select(tool => new
        {
            name = tool.Name,
            title = Humanize(tool.Name),
            description = tool.Description,
            inputSchema = tool.InputSchema,
            outputSchema = tool.OutputSchema,
            annotations = new
            {
                readOnlyHint = tool.ExecutionPolicy == McpToolExecutionPolicy.ReadOnly,
                destructiveHint = false,
                idempotentHint = tool.ExecutionPolicy != McpToolExecutionPolicy.ReadOnly,
                openWorldHint = tool.Name.Contains("search", StringComparison.Ordinal)
            }
        });
        return Results.Json(Success(id, new { tools }));
    }

    private static async Task<IResult> CallToolAsync(
        JsonElement? id,
        JsonElement root,
        AgentSession session,
        McpToolCatalog catalog,
        IPlatformCapabilityDispatcher dispatcher,
        CSweetDbContext db,
        IAuditEventWriter audit,
        Guid parentEventId,
        CancellationToken cancellationToken)
    {
        if (!root.TryGetProperty("params", out var parameters) ||
            !parameters.TryGetProperty("name", out var nameElement) ||
            nameElement.ValueKind != JsonValueKind.String)
            return Results.Json(Error(id, -32602, "Tool name is required."), statusCode: StatusCodes.Status400BadRequest);

        var grants = await ReadCurrentGrantsAsync(session, db, cancellationToken);
        if (grants is null)
            return Results.Json(Error(id, -32002, "The installation grant is no longer active."), statusCode: StatusCodes.Status403Forbidden);

        var tool = catalog.Find(nameElement.GetString()!, grants);
        if (tool is null)
            return Results.Json(Error(id, -32602, "The tool is not available to this installation."), statusCode: StatusCodes.Status403Forbidden);

        var arguments = parameters.TryGetProperty("arguments", out var argumentElement)
            ? argumentElement
            : JsonDocument.Parse("{}").RootElement.Clone();
        if (arguments.ValueKind != JsonValueKind.Object)
            return Results.Json(Error(id, -32602, "Tool arguments must be an object."), statusCode: StatusCodes.Status400BadRequest);
        if (ValidateArguments(arguments, tool.InputSchema) is { } validationError)
            return Results.Json(Error(id, -32602, validationError), statusCode: StatusCodes.Status400BadRequest);

        var request = new RequestCapability
        {
            RequestId = Guid.NewGuid().ToString("N"),
            Capability = tool.Capability,
            ContentType = "application/json",
            Payload = ByteString.CopyFrom(JsonSerializer.SerializeToUtf8Bytes(arguments, JsonOptions))
        };

        var toolAuditId = await audit.AppendAsync(new AuditEventWriteRequest(
            "mcp.tool.call", "Mcp", "Internal", "Authorized", BrokerAuditIdentity.OrganizationId(session),
            "McpTool", Summary: $"Authorized MCP tool {nameElement.GetString()}.", ParentEventId: parentEventId,
            ExternalRequestId: request.RequestId, Actor: BrokerAuditIdentity.Actor(session),
            ContentType: "application/json", Payload: request.Payload.ToByteArray()), cancellationToken);

        CapabilityResult? terminal = null;
        await foreach (var result in dispatcher.InvokeAsync(session, request, cancellationToken))
            terminal = result;

        if (terminal is null)
        {
            await audit.AppendAsync(new AuditEventWriteRequest(
                "mcp.tool.result", "Mcp", "Internal", "Failed", BrokerAuditIdentity.OrganizationId(session),
                "McpTool", ParentEventId: toolAuditId, ExternalRequestId: request.RequestId,
                Actor: BrokerAuditIdentity.Actor(session), ErrorCode: "no_result"), cancellationToken);
            return Results.Json(Error(id, -32603, "The capability returned no result."), statusCode: StatusCodes.Status502BadGateway);
        }

        await audit.AppendAsync(new AuditEventWriteRequest(
            "mcp.tool.result", "Mcp", "Internal", terminal.Succeeded ? "Completed" : "Failed",
            BrokerAuditIdentity.OrganizationId(session), "McpTool", ParentEventId: toolAuditId,
            ExternalRequestId: request.RequestId, Actor: BrokerAuditIdentity.Actor(session),
            ContentType: terminal.ContentType, Payload: terminal.Payload.ToByteArray(), ErrorMessage: terminal.Error), cancellationToken);

        var text = terminal.Payload.Length > 0 ? terminal.Payload.ToStringUtf8() : terminal.Error;
        JsonNode? structured = null;
        if (!string.IsNullOrWhiteSpace(text))
        {
            try { structured = JsonNode.Parse(text); }
            catch (JsonException) { }
        }

        return Results.Json(Success(id, new
        {
            content = new[] { new { type = "text", text } },
            structuredContent = structured,
            isError = !terminal.Succeeded
        }));
    }

    private static async Task<IReadOnlySet<string>?> ReadCurrentGrantsAsync(
        AgentSession session,
        CSweetDbContext db,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(session.InstallationId, out var installationId)) return null;
        var installation = await db.AgentInstallations.AsNoTracking()
            .Include(x => x.Grant)
            .SingleOrDefaultAsync(x => x.Id == installationId && x.IsEnabled, cancellationToken);
        if (installation?.Grant is null || !string.Equals(installation.BusinessId, session.BusinessId, StringComparison.OrdinalIgnoreCase))
            return null;
        try
        {
            var persisted = JsonSerializer.Deserialize<IReadOnlyList<string>>(
                installation.Grant.RequestedCapabilitiesJson, JsonOptions) ?? [];
            var sessionGrants = session.Grant.RequestedCapabilities ?? new HashSet<string>(StringComparer.Ordinal);
            return persisted.Where(sessionGrants.Contains).ToHashSet(StringComparer.Ordinal);
        }
        catch (JsonException) { return null; }
    }

    private static string? ReadBearerToken(string? authorization)
    {
        const string prefix = "Bearer ";
        return authorization?.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) == true
            ? authorization[prefix.Length..].Trim()
            : null;
    }

    private static async Task<(byte[] Body, bool TooLarge)> ReadLimitedBodyAsync(
        Stream stream,
        int maximumBytes,
        CancellationToken cancellationToken)
    {
        await using var buffer = new MemoryStream(capacity: maximumBytes);
        var chunk = new byte[81_920];
        while (true)
        {
            var bytesRead = await stream.ReadAsync(chunk, cancellationToken);
            if (bytesRead == 0)
                return (buffer.ToArray(), false);

            if (buffer.Length + bytesRead > maximumBytes)
                return ([], true);

            await buffer.WriteAsync(chunk.AsMemory(0, bytesRead), cancellationToken);
        }
    }

    private static string? ValidateArguments(JsonElement arguments, JsonElement schema)
    {
        if (schema.TryGetProperty("required", out var required) && required.ValueKind == JsonValueKind.Array)
            foreach (var property in required.EnumerateArray().Select(x => x.GetString()).Where(x => x is not null))
                if (!arguments.TryGetProperty(property!, out _)) return $"Required argument '{property}' is missing.";
        if (schema.TryGetProperty("additionalProperties", out var additional) && additional.ValueKind == JsonValueKind.False &&
            schema.TryGetProperty("properties", out var properties))
        {
            var allowed = properties.EnumerateObject().Select(x => x.Name).ToHashSet(StringComparer.Ordinal);
            var unknown = arguments.EnumerateObject().FirstOrDefault(x => !allowed.Contains(x.Name));
            if (unknown.Name is not null) return $"Argument '{unknown.Name}' is not allowed.";
        }
        return null;
    }

    private static object Success(JsonElement? id, object result) => new { jsonrpc = "2.0", id, result };
    private static object Error(JsonElement? id, int code, string message) => new { jsonrpc = "2.0", id, error = new { code, message } };
    private static Task<Guid> AuditMcpResultAsync(IAuditEventWriter audit, AgentSession session, Guid parentEventId,
        string? method, string outcome, CancellationToken cancellationToken) => audit.AppendAsync(new AuditEventWriteRequest(
            "mcp.response", "Mcp", "Outbound", outcome, BrokerAuditIdentity.OrganizationId(session), "McpResponse",
            Summary: $"MCP method {method} finished with {outcome}.", ParentEventId: parentEventId,
            Actor: new AuditActor("Platform", DisplayName: "C-Sweet MCP gateway"),
            Target: BrokerAuditIdentity.Target(session)), cancellationToken);
    private static string Humanize(string name) => string.Join(' ', name.Split('_').Select(word => char.ToUpperInvariant(word[0]) + word[1..]));
}
