# Phase 1 - Repository and Solution Bootstrap

## Goal

Create the initial C-Sweet .NET solution structure, local development orchestration, and baseline build/test workflow.

This phase does not implement business logic. It creates the skeleton that every later phase depends on.

## Why this phase comes first

Junior developers need a consistent project layout before adding entities, APIs, UI, and AI integration. The solution should clearly separate domain logic, application use cases, infrastructure, AI integration, the Blazor app, the API, and the worker host.

## Deliverables

- A solution file named `CSweet.sln`.
- Initial projects under `/src`.
- Initial test projects under `/tests`.
- .NET Aspire AppHost and ServiceDefaults projects.
- Basic API health endpoint.
- Basic Blazor WASM shell.
- Centralized package/version management if desired.
- A working `dotnet build`.
- A working `dotnet test`.

## Proposed folder structure

```text
/src
  /CSweet.App
  /CSweet.Api
  /CSweet.Domain
  /CSweet.Application
  /CSweet.Infrastructure
  /CSweet.AI
  /CSweet.WorkerHost
  /CSweet.Contracts
  /CSweet.AppHost
  /CSweet.ServiceDefaults
/tests
  /CSweet.UnitTests
  /CSweet.IntegrationTests
/docs
  /implementation
```

## Project creation commands

Use these commands from the repository root. Confirm the target .NET SDK version with the tech lead before running them.

```bash
dotnet new sln -n CSweet

mkdir -p src tests docs

dotnet new webapi -n CSweet.Api -o src/CSweet.Api
dotnet new blazorwasm -n CSweet.App -o src/CSweet.App
dotnet new classlib -n CSweet.Domain -o src/CSweet.Domain
dotnet new classlib -n CSweet.Application -o src/CSweet.Application
dotnet new classlib -n CSweet.Infrastructure -o src/CSweet.Infrastructure
dotnet new classlib -n CSweet.AI -o src/CSweet.AI
dotnet new classlib -n CSweet.Contracts -o src/CSweet.Contracts
dotnet new worker -n CSweet.WorkerHost -o src/CSweet.WorkerHost

dotnet new aspire-apphost -n CSweet.AppHost -o src/CSweet.AppHost
dotnet new aspire-servicedefaults -n CSweet.ServiceDefaults -o src/CSweet.ServiceDefaults

dotnet new xunit -n CSweet.UnitTests -o tests/CSweet.UnitTests
dotnet new xunit -n CSweet.IntegrationTests -o tests/CSweet.IntegrationTests
```

Add projects to the solution:

```bash
dotnet sln add src/CSweet.Api/CSweet.Api.csproj
dotnet sln add src/CSweet.App/CSweet.App.csproj
dotnet sln add src/CSweet.Domain/CSweet.Domain.csproj
dotnet sln add src/CSweet.Application/CSweet.Application.csproj
dotnet sln add src/CSweet.Infrastructure/CSweet.Infrastructure.csproj
dotnet sln add src/CSweet.AI/CSweet.AI.csproj
dotnet sln add src/CSweet.Contracts/CSweet.Contracts.csproj
dotnet sln add src/CSweet.WorkerHost/CSweet.WorkerHost.csproj
dotnet sln add src/CSweet.AppHost/CSweet.AppHost.csproj
dotnet sln add src/CSweet.ServiceDefaults/CSweet.ServiceDefaults.csproj
dotnet sln add tests/CSweet.UnitTests/CSweet.UnitTests.csproj
dotnet sln add tests/CSweet.IntegrationTests/CSweet.IntegrationTests.csproj
```

## Project references

Recommended references:

```text
CSweet.Api
  → CSweet.Application
  → CSweet.Contracts
  → CSweet.Infrastructure
  → CSweet.AI
  → CSweet.ServiceDefaults

CSweet.App
  → CSweet.Contracts

CSweet.Application
  → CSweet.Domain
  → CSweet.Contracts

CSweet.Infrastructure
  → CSweet.Domain
  → CSweet.Application

CSweet.AI
  → CSweet.Application
  → CSweet.Contracts

CSweet.WorkerHost
  → CSweet.Application
  → CSweet.Infrastructure
  → CSweet.AI
  → CSweet.Contracts
  → CSweet.ServiceDefaults

CSweet.AppHost
  → CSweet.Api
  → CSweet.App
  → CSweet.WorkerHost

CSweet.UnitTests
  → CSweet.Domain
  → CSweet.Application
  → CSweet.AI

CSweet.IntegrationTests
  → CSweet.Api
  → CSweet.Infrastructure
```

Do not reference infrastructure from domain.

## ServiceDefaults setup

Add service defaults to API and WorkerHost so they share health checks, logging, OpenTelemetry, and resiliency.

Expected API startup pattern:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.MapDefaultEndpoints();
app.MapControllers();

app.Run();
```

## AppHost baseline

The AppHost should run:

- API
- Blazor app
- WorkerHost
- Postgres placeholder, added in Phase 2

Example shape:

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var api = builder.AddProject<Projects.CSweet_Api>("api");

builder.AddProject<Projects.CSweet_App>("app")
    .WithReference(api);

builder.AddProject<Projects.CSweet_WorkerHost>("workerhost");

builder.Build().Run();
```

Adjust the exact syntax to match the current Aspire package version.

## Baseline API endpoint

Add:

```http
GET /api/health
```

Expected response:

```json
{
  "status": "ok",
  "service": "CSweet.Api"
}
```

## Baseline UI

The Blazor app should initially show:

- Product name: C-Sweet.
- Environment label.
- API connectivity status.
- Link placeholder to `/setup`.

## Configuration files

Add or verify:

```text
.editorconfig
.gitignore
Directory.Build.props
Directory.Packages.props optional
README.md
```

## Testing requirements

### Unit tests

Add one placeholder test that confirms the unit test project runs.

### Integration tests

Add one test for `GET /api/health`.

Expected result:

- Status code is 200.
- Response includes `status = ok`.

## Manual QA

From repo root:

```bash
dotnet restore
dotnet build
dotnet test
```

Run AppHost:

```bash
dotnet run --project src/CSweet.AppHost
```

Verify:

- Aspire dashboard opens.
- API is listed.
- App is listed.
- WorkerHost is listed.
- API health endpoint responds.

## Acceptance criteria

- [ ] Solution builds.
- [ ] Tests run.
- [ ] API health endpoint works.
- [ ] Blazor app loads.
- [ ] Aspire AppHost starts all initial services.
- [ ] Project references follow the dependency rules.
- [ ] No business logic has been added to the UI or infrastructure projects.

## Common mistakes

- Do not put EF Core entities in `CSweet.Api`.
- Do not let the Blazor app call the database.
- Do not call LM Studio directly from the UI.
- Do not add Agent Framework directly to every project.
- Do not create marketplace-specific projects yet.
