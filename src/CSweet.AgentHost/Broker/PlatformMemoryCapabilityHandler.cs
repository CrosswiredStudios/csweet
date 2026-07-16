using System.Text.Json;
using CSweet.Agent.Contracts.Grpc;
using CSweet.Memory;
using Google.Protobuf;

namespace CSweet.AgentHost.Broker;

public sealed class PlatformMemoryCapabilityHandler
{
    private const int MaximumRequestBytes = 1_048_576;
    private const int MaximumResponseBytes = 4_194_304;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IMemoryStore _store;
    private readonly IKnowledgeTransferStore _transfers;
    private readonly ILogger<PlatformMemoryCapabilityHandler> _logger;

    public PlatformMemoryCapabilityHandler(IMemoryStore store, ILogger<PlatformMemoryCapabilityHandler> logger)
    {
        _store = store;
        _transfers = store as IKnowledgeTransferStore
            ?? throw new InvalidOperationException("The platform memory store must support knowledge transfer.");
        _logger = logger;
    }

    public static bool IsPlatformMemoryCapability(string capability) => capability is
        CSweetMemoryCapabilities.Query or CSweetMemoryCapabilities.Write or
        CSweetMemoryCapabilities.Manage or CSweetMemoryCapabilities.Export;

    public async Task<CapabilityResult> HandleAsync(
        AgentSession session,
        RequestCapability request,
        CancellationToken cancellationToken)
    {
        if (request.Payload.Length > MaximumRequestBytes)
            return Failure(request.RequestId, "The memory request exceeds the 1 MB limit.");
        if (!string.Equals(request.ContentType, "application/json", StringComparison.OrdinalIgnoreCase))
            return Failure(request.RequestId, "Platform memory requests must use application/json.");
        if (!session.Grant.Permissions.Contains("capability.request"))
            return Failure(request.RequestId, "The installation is not granted capability.request.");

        try
        {
            var command = JsonSerializer.Deserialize<CSweetMemoryCommand>(request.Payload.Span, JsonOptions)
                ?? throw new JsonException("The memory command is empty.");
            await _store.InitializeAsync(cancellationToken);
            var result = request.Capability switch
            {
                CSweetMemoryCapabilities.Query => await HandleQueryAsync(session, command, cancellationToken),
                CSweetMemoryCapabilities.Write => await HandleWriteAsync(session, command, cancellationToken),
                CSweetMemoryCapabilities.Manage => await HandleManageAsync(session, command, cancellationToken),
                CSweetMemoryCapabilities.Export => await HandleExportAsync(session, command, cancellationToken),
                _ => throw new InvalidOperationException("Unsupported platform memory capability.")
            };
            var payload = JsonSerializer.SerializeToUtf8Bytes(result, result?.GetType() ?? typeof(object), JsonOptions);
            return payload.Length > MaximumResponseBytes
                ? Failure(request.RequestId, "The memory response exceeds the 4 MB limit; narrow the requested scope.")
                : Success(request.RequestId, payload);
        }
        catch (JsonException exception)
        {
            return Failure(request.RequestId, $"The memory request is invalid: {exception.Message}");
        }
        catch (UnauthorizedAccessException exception)
        {
            _logger.LogWarning("Denied memory operation {RequestId} from agent {AgentId}: {Reason}", request.RequestId, session.AgentId, exception.Message);
            return Failure(request.RequestId, exception.Message);
        }
        catch (KeyNotFoundException)
        {
            return Failure(request.RequestId, "The requested memory record was not found.");
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
        {
            _logger.LogWarning(exception, "Memory operation {RequestId} failed for agent {AgentId}.", request.RequestId, session.AgentId);
            return Failure(request.RequestId, "The platform could not complete the memory operation.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Platform memory is unavailable for request {RequestId} from agent {AgentId}.", request.RequestId, session.AgentId);
            return Failure(request.RequestId, "Long-term memory is temporarily unavailable; the agent can continue without recalled memory.");
        }
    }

    private async Task<object?> HandleQueryAsync(AgentSession session, CSweetMemoryCommand command, CancellationToken cancellationToken) => command.Operation switch
    {
        "find-entity-by-application-key" => await FindEntityByApplicationKeyAsync(session, Read<FindEntityByApplicationKeyInput>(command), cancellationToken),
        "find-entity" => await FindEntityAsync(session, Read<FindEntityInput>(command), cancellationToken),
        "search" => await SearchAsync(session, Read<MemorySearchRequest>(command), cancellationToken),
        "get-claim" => await GetClaimAsync(session, Read<GetClaimInput>(command).ClaimId, cancellationToken),
        "list-claims" => await ListClaimsAsync(session, Read<MemoryPartition>(command), cancellationToken),
        "get-knowledge-transfer" => await GetTransferAsync(session, Read<GetTransferInput>(command).PackageId, cancellationToken),
        _ => throw new InvalidOperationException("Unsupported memory query operation.")
    };

    private async Task<object> HandleWriteAsync(AgentSession session, CSweetMemoryCommand command, CancellationToken cancellationToken)
    {
        switch (command.Operation)
        {
            case "append-episode":
                var episode = Read<MemoryEpisode>(command); Authorize(session, episode.Partition, MemoryAction.Propose);
                return await _store.AppendEpisodeAsync(episode, cancellationToken);
            case "upsert-entity":
                var entity = Read<MemoryEntity>(command); Authorize(session, entity.Partition, MemoryAction.Propose);
                return await _store.UpsertEntityAsync(entity, cancellationToken);
            case "write-claim":
                var claim = Read<MemoryClaim>(command); Authorize(session, claim.Partition, MemoryAction.Propose);
                return await _store.WriteClaimAsync(claim, cancellationToken);
            case "write-edge":
                var edge = Read<MemoryEdge>(command); Authorize(session, edge.Partition, MemoryAction.Propose);
                return await _store.WriteEdgeAsync(edge, cancellationToken);
            case "write-block":
                var block = Read<MemoryBlock>(command); Authorize(session, block.Partition, MemoryAction.Propose);
                return await _store.WriteBlockAsync(block, cancellationToken);
            case "write-procedure":
                var procedure = Read<ProceduralMemory>(command); Authorize(session, procedure.Partition, MemoryAction.Propose);
                return await _store.WriteProcedureAsync(procedure, cancellationToken);
            case "write-embedding":
                var embedding = Read<MemoryEmbedding>(command); Authorize(session, embedding.Partition, MemoryAction.Propose);
                return await _store.WriteEmbeddingAsync(embedding, cancellationToken);
            case "record-use":
                var use = Read<MemoryUse>(command); Authorize(session, use.Partition, MemoryAction.Propose);
                await _store.RecordUseAsync(use, cancellationToken);
                return new MemoryWriteResult(use.Id, true);
            default:
                throw new InvalidOperationException("Unsupported memory write operation.");
        }
    }

    private async Task<object> HandleManageAsync(AgentSession session, CSweetMemoryCommand command, CancellationToken cancellationToken)
    {
        switch (command.Operation)
        {
            case "supersede-claim":
                var supersede = Read<SupersedeClaimInput>(command);
                var existing = await RequiredClaimAsync(supersede.ClaimId, cancellationToken);
                Authorize(session, existing.Partition, MemoryAction.Manage);
                await _store.SupersedeClaimAsync(supersede.ClaimId, supersede.SupersededByClaimId, supersede.ValidTo, cancellationToken);
                return new MemoryWriteResult(supersede.ClaimId, false);
            case "set-confirmation":
                var confirmation = Read<SetConfirmationInput>(command);
                var claim = await RequiredClaimAsync(confirmation.ClaimId, cancellationToken);
                Authorize(session, claim.Partition, MemoryAction.Manage);
                await _store.SetClaimConfirmationAsync(confirmation.ClaimId, confirmation.Confirmation, cancellationToken);
                return new MemoryWriteResult(confirmation.ClaimId, false);
            case "write-knowledge-transfer":
                var package = Read<KnowledgeTransferPackage>(command);
                foreach (var source in package.SourceNamespaces) Authorize(session, source.Partition, MemoryAction.Read);
                Authorize(session, package.TargetNamespace.Partition,
                    package.Status == KnowledgeTransferStatus.PendingApproval ? MemoryAction.Propose : MemoryAction.Manage);
                await _transfers.WriteKnowledgeTransferAsync(package, cancellationToken);
                return new MemoryWriteResult(package.Id, true);
            case "delete-scope":
                var partition = Read<MemoryPartition>(command);
                Authorize(session, partition, MemoryAction.Manage);
                await _store.DeleteScopeAsync(partition, cancellationToken);
                return new MemoryWriteResult(Guid.Empty, false);
            default:
                throw new InvalidOperationException("Unsupported memory management operation.");
        }
    }

    private async Task<object> HandleExportAsync(AgentSession session, CSweetMemoryCommand command, CancellationToken cancellationToken)
    {
        if (command.Operation != "export") throw new InvalidOperationException("Unsupported memory export operation.");
        var partition = Read<MemoryPartition>(command);
        Authorize(session, partition, MemoryAction.Read);
        return await _store.ExportAsync(partition, cancellationToken);
    }

    private async Task<object?> FindEntityByApplicationKeyAsync(AgentSession session, FindEntityByApplicationKeyInput input, CancellationToken cancellationToken)
    { Authorize(session, input.Partition, MemoryAction.Read); return await _store.FindEntityByApplicationKeyAsync(input.Partition, input.ApplicationKey, cancellationToken); }

    private async Task<object?> FindEntityAsync(AgentSession session, FindEntityInput input, CancellationToken cancellationToken)
    { Authorize(session, input.Partition, MemoryAction.Read); return await _store.FindEntityAsync(input.Partition, input.CanonicalName, cancellationToken); }

    private async Task<object> SearchAsync(AgentSession session, MemorySearchRequest request, CancellationToken cancellationToken)
    { Authorize(session, request.Partition, MemoryAction.Read); return await _store.SearchAsync(request, cancellationToken); }

    private async Task<object?> GetClaimAsync(AgentSession session, Guid claimId, CancellationToken cancellationToken)
    { var claim = await _store.GetClaimAsync(claimId, cancellationToken); if (claim is null) return null; Authorize(session, claim.Partition, MemoryAction.Read); return claim; }

    private async Task<object> ListClaimsAsync(AgentSession session, MemoryPartition partition, CancellationToken cancellationToken)
    { Authorize(session, partition, MemoryAction.Read); return await _store.ListClaimsAsync(partition, cancellationToken); }

    private async Task<object?> GetTransferAsync(AgentSession session, Guid packageId, CancellationToken cancellationToken)
    { var package = await _transfers.GetKnowledgeTransferAsync(packageId, cancellationToken); if (package is null) return null; Authorize(session, package.TargetNamespace.Partition, MemoryAction.Read); return package; }

    private async Task<MemoryClaim> RequiredClaimAsync(Guid claimId, CancellationToken cancellationToken) =>
        await _store.GetClaimAsync(claimId, cancellationToken) ?? throw new KeyNotFoundException();

    private static T Read<T>(CSweetMemoryCommand command) => command.Payload.Deserialize<T>(JsonOptions)
        ?? throw new JsonException($"Operation '{command.Operation}' has an empty payload.");

    private static void Authorize(AgentSession session, MemoryPartition partition, MemoryAction action)
    {
        if (!string.Equals(partition.TenantId, session.BusinessId, StringComparison.Ordinal))
            throw new UnauthorizedAccessException("Cross-business memory access is forbidden.");
        var area = partition.UserId is null ? "business" : "user";
        var verb = action switch { MemoryAction.Read => "read", MemoryAction.Propose => "propose", _ => "manage" };
        var permission = $"memory.{area}.{verb}";
        if (!session.Grant.Permissions.Contains(permission))
            throw new UnauthorizedAccessException($"The installation is not granted {permission}.");
    }

    private static CapabilityResult Success(string requestId, byte[] payload) => new()
    {
        RequestId = requestId, Succeeded = true, ContentType = "application/json",
        Payload = ByteString.CopyFrom(payload), HasMore = false
    };

    private static CapabilityResult Failure(string requestId, string error) => new()
    {
        RequestId = requestId, Succeeded = false, ContentType = "application/json", Error = error, HasMore = false
    };

    private enum MemoryAction { Read, Propose, Manage }
    private sealed record FindEntityByApplicationKeyInput(MemoryPartition Partition, string ApplicationKey);
    private sealed record FindEntityInput(MemoryPartition Partition, string CanonicalName);
    private sealed record GetClaimInput(Guid ClaimId);
    private sealed record GetTransferInput(Guid PackageId);
    private sealed record SupersedeClaimInput(Guid ClaimId, Guid SupersededByClaimId, DateTimeOffset ValidTo);
    private sealed record SetConfirmationInput(Guid ClaimId, MemoryConfirmationState Confirmation);
}
