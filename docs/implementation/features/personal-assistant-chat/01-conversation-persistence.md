# Phase 1 — Conversation persistence

## Goal

Add the database foundation for chat: a `Conversation` (a thread between the "Self" user and
one agent employee) and its `ConversationMessage` rows (each user or assistant turn). Expose
minimal API endpoints to create a conversation, fetch it, and list its messages.

This phase has **no agent and no streaming** — it is pure CRUD. You can build and test it in
isolation, which is exactly why we do it first.

## Why this phase matters

Everything else references a conversation by `Guid`. The gateway persists the user's message
before contacting the agent, and persists the assistant's message once the stream finishes.
If persistence is solid and tested here, later phases only have to *call* it.

## Prerequisites

- You can run the solution locally (see [docs/implementation/README.md](../../README.md)).
- You understand the existing persistence pattern. Read these first — you will copy their shape:
  - Entity: [src/CSweet.Domain/Core/OrganizationUser.cs](../../../../src/CSweet.Domain/Core/OrganizationUser.cs)
  - EF config: [src/CSweet.Infrastructure/Persistence/CoreConfigurations.cs](../../../../src/CSweet.Infrastructure/Persistence/CoreConfigurations.cs)
  - Service: [src/CSweet.Infrastructure/Core/OrganizationUserService.cs](../../../../src/CSweet.Infrastructure/Core/OrganizationUserService.cs)
  - Endpoints: [src/CSweet.Api/Core/OrganizationUserEndpoints.cs](../../../../src/CSweet.Api/Core/OrganizationUserEndpoints.cs)
  - Mapper: [src/CSweet.Infrastructure/Core/CoreMappers.cs](../../../../src/CSweet.Infrastructure/Core/CoreMappers.cs)

## Deliverables

- Two domain entities and one enum in `CSweet.Domain`.
- EF Core configuration + `DbSet`s + a migration.
- An `IConversationService` (interface in `CSweet.Application`, implementation in `CSweet.Infrastructure`).
- Request/response DTOs in `CSweet.Contracts`.
- `ToResponse()` mappers.
- `MapConversationEndpoints()` registered in the API.
- DI registration.

## Data model

```
Conversation (1) ────< (many) ConversationMessage

Conversation
  Id                          Guid (PK)
  OrganizationId              Guid  -> the business
  AgentOrganizationUserId     Guid  -> the agent employee being chatted with (OrganizationUser, EmployeeType=Agent)
  InitiatedByOrganizationUserId Guid -> the "Self" human who started it
  Title                       string?  (optional, e.g. first user message truncated)
  CreatedAt                   DateTimeOffset
  UpdatedAt                   DateTimeOffset

ConversationMessage
  Id                Guid (PK)
  ConversationId    Guid (FK -> Conversation)
  Role              ConversationRole enum (User | Assistant)
  Content           string (the full text of the turn)
  CreatedAt         DateTimeOffset
```

> We keep this intentionally small. No token counts, no status flags, no provider metadata.
> Add those later only if a phase actually needs them.

## Step-by-step

### 1. Domain entities and enum

Create `src/CSweet.Domain/Core/ConversationRole.cs`:

```csharp
namespace CSweet.Domain.Core;

public enum ConversationRole
{
    User = 0,
    Assistant = 1
}
```

Create `src/CSweet.Domain/Core/Conversation.cs`:

```csharp
namespace CSweet.Domain.Core;

public sealed class Conversation
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid AgentOrganizationUserId { get; set; }
    public Guid InitiatedByOrganizationUserId { get; set; }
    public string? Title { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    // Navigation
    public Organization? Organization { get; set; }
    public OrganizationUser? AgentOrganizationUser { get; set; }
    public ICollection<ConversationMessage> Messages { get; set; } = new List<ConversationMessage>();
}
```

Create `src/CSweet.Domain/Core/ConversationMessage.cs`:

```csharp
namespace CSweet.Domain.Core;

public sealed class ConversationMessage
{
    public Guid Id { get; set; }
    public Guid ConversationId { get; set; }
    public ConversationRole Role { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }

    // Navigation
    public Conversation? Conversation { get; set; }
}
```

> Follow the existing style: plain sealed classes, `Guid Id`, `DateTimeOffset` timestamps,
> no data annotations, no EF references here.

### 2. Register DbSets

In [src/CSweet.Infrastructure/Persistence/CSweetDbContext.cs](../../../../src/CSweet.Infrastructure/Persistence/CSweetDbContext.cs),
add DbSets next to the other core entities:

```csharp
public DbSet<Conversation> Conversations => Set<Conversation>();
public DbSet<ConversationMessage> ConversationMessages => Set<ConversationMessage>();
```

### 3. EF Core configuration

Add two configuration methods to
[src/CSweet.Infrastructure/Persistence/CoreConfigurations.cs](../../../../src/CSweet.Infrastructure/Persistence/CoreConfigurations.cs).
First register them at the top of `Apply(...)`:

```csharp
modelBuilder.Entity<Conversation>(ConfigureConversation);
modelBuilder.Entity<ConversationMessage>(ConfigureConversationMessage);
```

Then add the methods (match the enum-as-string and delete-behavior conventions used by
`ConfigureOrganizationUser`):

```csharp
static void ConfigureConversation(EntityTypeBuilder<Conversation> entity)
{
    entity.HasKey(x => x.Id);
    entity.Property(x => x.Title).HasMaxLength(256);

    entity.HasOne(x => x.Organization)
        .WithMany()
        .HasForeignKey(x => x.OrganizationId)
        .OnDelete(DeleteBehavior.Cascade);

    entity.HasOne(x => x.AgentOrganizationUser)
        .WithMany()
        .HasForeignKey(x => x.AgentOrganizationUserId)
        .OnDelete(DeleteBehavior.Restrict);

    entity.HasIndex(x => new { x.OrganizationId, x.AgentOrganizationUserId });
}

static void ConfigureConversationMessage(EntityTypeBuilder<ConversationMessage> entity)
{
    entity.HasKey(x => x.Id);
    entity.Property(x => x.Role).HasConversion<string>().HasMaxLength(16).IsRequired();
    entity.Property(x => x.Content).HasMaxLength(32768).IsRequired();

    entity.HasOne(x => x.Conversation)
        .WithMany(x => x.Messages)
        .HasForeignKey(x => x.ConversationId)
        .OnDelete(DeleteBehavior.Cascade);

    entity.HasIndex(x => new { x.ConversationId, x.CreatedAt });
}
```

> We do **not** add a FK from `InitiatedByOrganizationUserId` to `OrganizationUser` to avoid
> multiple cascade paths on the same table in PostgreSQL. It stays a plain `Guid` column.

### 4. DTOs (contracts)

Create `src/CSweet.Contracts/Core/ConversationResponse.cs`:

```csharp
namespace CSweet.Contracts.Core;

public sealed record ConversationResponse(
    Guid Id,
    Guid OrganizationId,
    Guid AgentOrganizationUserId,
    Guid InitiatedByOrganizationUserId,
    string? Title,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
```

Create `src/CSweet.Contracts/Core/ConversationMessageResponse.cs`:

```csharp
namespace CSweet.Contracts.Core;

public sealed record ConversationMessageResponse(
    Guid Id,
    Guid ConversationId,
    int Role,          // 0 = User, 1 = Assistant (matches ConversationRole)
    string Content,
    DateTimeOffset CreatedAt);
```

Create `src/CSweet.Contracts/Core/StartConversationRequest.cs`:

```csharp
using System.ComponentModel.DataAnnotations;

namespace CSweet.Contracts.Core;

public sealed record StartConversationRequest(
    [property: Required] Guid AgentOrganizationUserId);
```

### 5. Mappers

Add to [src/CSweet.Infrastructure/Core/CoreMappers.cs](../../../../src/CSweet.Infrastructure/Core/CoreMappers.cs):

```csharp
public static ConversationResponse ToResponse(this Conversation conversation)
{
    return new ConversationResponse(
        conversation.Id,
        conversation.OrganizationId,
        conversation.AgentOrganizationUserId,
        conversation.InitiatedByOrganizationUserId,
        conversation.Title,
        conversation.CreatedAt,
        conversation.UpdatedAt);
}

public static ConversationMessageResponse ToResponse(this ConversationMessage message)
{
    return new ConversationMessageResponse(
        message.Id,
        message.ConversationId,
        (int)message.Role,
        message.Content,
        message.CreatedAt);
}
```

### 6. Application interface

Create `src/CSweet.Application/Core/IConversationService.cs`:

```csharp
using CSweet.Contracts.Core;

namespace CSweet.Application.Core;

public interface IConversationService
{
    Task<ConversationResponse?> GetAsync(Guid conversationId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ConversationMessageResponse>> ListMessagesAsync(
        Guid conversationId,
        CancellationToken cancellationToken = default);

    /// <summary>Creates a conversation with the given agent employee, or returns a failure.</summary>
    Task<ConversationActionResponse> StartAsync(
        Guid organizationId,
        StartConversationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Appends a message turn and bumps the conversation's UpdatedAt.</summary>
    Task<ConversationMessageResponse> AppendMessageAsync(
        Guid conversationId,
        ConversationRole role,
        string content,
        CancellationToken cancellationToken = default);
}
```

You will reference `ConversationRole` from `CSweet.Domain.Core`, so add
`using CSweet.Domain.Core;` (the Application layer may reference Domain).

Create a small result record `src/CSweet.Contracts/Core/ConversationActionResponse.cs`
(mirrors the existing `CoreActionResponse` shape):

```csharp
namespace CSweet.Contracts.Core;

public sealed record ConversationActionResponse(
    bool Succeeded,
    string? ErrorCode,
    string? Message,
    ConversationResponse? Conversation = null);
```

### 7. Service implementation

Create `src/CSweet.Infrastructure/Core/ConversationService.cs`. Copy the shape of
`OrganizationUserService` (inject `CSweetDbContext` + `IAuditEventWriter`, validate, write audit):

```csharp
using CSweet.Application.Core;
using CSweet.Application.Setup;
using CSweet.Contracts.Core;
using CSweet.Domain.Core;
using CSweet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSweet.Infrastructure.Core;

public sealed class ConversationService : IConversationService
{
    private readonly CSweetDbContext _dbContext;
    private readonly IAuditEventWriter _auditEventWriter;

    public ConversationService(CSweetDbContext dbContext, IAuditEventWriter auditEventWriter)
    {
        _dbContext = dbContext;
        _auditEventWriter = auditEventWriter;
    }

    public async Task<ConversationResponse?> GetAsync(Guid conversationId, CancellationToken cancellationToken = default)
    {
        var conversation = await _dbContext.Conversations
            .SingleOrDefaultAsync(x => x.Id == conversationId, cancellationToken);

        return conversation?.ToResponse();
    }

    public async Task<IReadOnlyList<ConversationMessageResponse>> ListMessagesAsync(
        Guid conversationId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.ConversationMessages
            .Where(x => x.ConversationId == conversationId)
            .OrderBy(x => x.CreatedAt)
            .Select(x => x.ToResponse())
            .ToListAsync(cancellationToken);
    }

    public async Task<ConversationActionResponse> StartAsync(
        Guid organizationId,
        StartConversationRequest request,
        CancellationToken cancellationToken = default)
    {
        var agent = await _dbContext.CoreOrganizationUsers
            .SingleOrDefaultAsync(
                x => x.Id == request.AgentOrganizationUserId && x.OrganizationId == organizationId,
                cancellationToken);

        if (agent is null)
        {
            return new ConversationActionResponse(false, "agent_not_found",
                "The agent employee was not found in this organization.");
        }

        if (agent.EmployeeType != EmployeeType.Agent)
        {
            return new ConversationActionResponse(false, "not_an_agent",
                "Conversations can only be started with agent employees.");
        }

        // The initiator is the org's "Self" human owner. Auth is out of scope for now.
        var self = await _dbContext.CoreOrganizationUsers
            .Where(x => x.OrganizationId == organizationId && x.EmployeeType == EmployeeType.Human)
            .OrderBy(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (self is null)
        {
            return new ConversationActionResponse(false, "no_owner",
                "This organization has no human owner to initiate the conversation.");
        }

        var now = DateTimeOffset.UtcNow;
        var conversation = new Conversation
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            AgentOrganizationUserId = agent.Id,
            InitiatedByOrganizationUserId = self.Id,
            Title = null,
            CreatedAt = now,
            UpdatedAt = now
        };

        _dbContext.Conversations.Add(conversation);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditEventWriter.WriteAsync(
            "conversation.started",
            "Conversation",
            conversation.Id,
            $"Conversation started with agent '{agent.DisplayName}'.",
            cancellationToken: cancellationToken);

        return new ConversationActionResponse(true, null, "Conversation started.",
            conversation.ToResponse());
    }

    public async Task<ConversationMessageResponse> AppendMessageAsync(
        Guid conversationId,
        ConversationRole role,
        string content,
        CancellationToken cancellationToken = default)
    {
        var conversation = await _dbContext.Conversations
            .SingleOrDefaultAsync(x => x.Id == conversationId, cancellationToken)
            ?? throw new InvalidOperationException($"Conversation {conversationId} was not found.");

        var now = DateTimeOffset.UtcNow;
        var message = new ConversationMessage
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            Role = role,
            Content = content,
            CreatedAt = now
        };

        conversation.UpdatedAt = now;

        // Use the first user message as the conversation title (nice-to-have).
        if (conversation.Title is null && role == ConversationRole.User)
        {
            conversation.Title = content.Length <= 80 ? content : content[..80];
        }

        _dbContext.ConversationMessages.Add(message);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return message.ToResponse();
    }
}
```

> `AppendMessageAsync` does **not** write an audit event per message on purpose — chat is
> high-volume. The `conversation.started` audit is enough for now.

### 8. Register DI

In [src/CSweet.Infrastructure/DependencyInjection.cs](../../../../src/CSweet.Infrastructure/DependencyInjection.cs),
next to the other core services:

```csharp
builder.Services.AddScoped<IConversationService, ConversationService>();
```

### 9. API endpoints

Create `src/CSweet.Api/Core/ConversationEndpoints.cs`:

```csharp
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
```

Register it in [src/CSweet.Api/Program.cs](../../../../src/CSweet.Api/Program.cs) next to the
other core endpoints:

```csharp
app.MapConversationEndpoints();
```

### 10. Create the migration

From the repository root:

```powershell
dotnet ef migrations add ConversationPersistence `
  --project src/CSweet.Infrastructure `
  --startup-project src/CSweet.Api
```

This creates a new file under
`src/CSweet.Infrastructure/Persistence/Migrations/`. Open it and sanity-check that it creates
`Conversations` and `ConversationMessages` tables with the FKs and indexes above. In
development the API applies migrations on startup
(`CSweetDatabaseInitializer.EnsureDatabaseReadyAsync`), so you do not need to run `database
update` manually when running through Aspire.

## Testing

Add unit tests in `tests/CSweet.UnitTests` (use the in-memory provider pattern already used
by other tests — see existing `*ServiceTests.cs`).

- `StartAsync` returns success and persists a `Conversation` when the target is an agent.
- `StartAsync` returns `not_an_agent` when the target is a human (e.g. "Self").
- `StartAsync` returns `agent_not_found` for an unknown id.
- `AppendMessageAsync` persists a message, orders correctly by `CreatedAt`, and sets the
  conversation `Title` from the first user message.
- `ListMessagesAsync` returns messages in chronological order.

## Acceptance criteria

- [ ] `Conversation`, `ConversationMessage`, and `ConversationRole` exist in `CSweet.Domain`.
- [ ] `CSweetDbContext` exposes both new `DbSet`s and the EF config compiles.
- [ ] A migration exists that creates both tables and applies cleanly to a fresh database.
- [ ] `IConversationService` is implemented, registered in DI, and covered by unit tests.
- [ ] `POST`/`GET`/`GET messages` endpoints work when tested with the REST client
      ([src/CSweet.Api/CSweet.Api.http](../../../../src/CSweet.Api/CSweet.Api.http)) or curl.
- [ ] Starting a conversation against the "Self" user is rejected with `not_an_agent`.

## Definition of done

You can `POST` a conversation for an agent employee, `GET` it back, append messages through
the service, and list them in order — all without any agent or streaming involved.
