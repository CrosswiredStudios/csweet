# Debug & Local Development Guide

## Quick Start

### Option 1: Aspire AppHost (Recommended)

Run `CSweet.AppHost` to start all services together with the Aspire dashboard.

**In VS Code:**
1. Set startup project: Right-click `src/CSweet.AppHost` → "Set as Startup Project" (or use `.vscode/launch.json`)
2. Press F5 or click Run → Start Debugging

**From terminal:**
```powershell
dotnet run --project src/CSweet.AppHost
```

This will:
- Build and start `CSweet.Api` on a random port
- Build and start `CSweet.App` (Blazor frontend) on a random port
- Build and start `CSweet.WorkerHost` as a background service
- Open the Aspire dashboard automatically (shows all services, health status, logs)

The Aspire dashboard URL appears in the console output (typically `https://localhost:15887`).

### Option 2: Individual Projects

Run any project independently for focused debugging.

```powershell
# API only (health endpoint on default port)
dotnet run --project src/CSweet.Api

# Blazor frontend only
dotnet run --project src/CSweet.App

# Worker background service only
dotnet run --project src/CSweet.WorkerHost
```

## External Services Required by Phase

| Phase | External Services Needed | Notes |
|-------|--------------------------|-------|
| **Phase 1** (current) | **None** | All projects are self-contained scaffolding with health checks only |
| **Phase 2** (config persistence + setup wizard) | PostgreSQL | EF Core context wired up, migration job needs database |
| **Phase 3+** (LLM providers, agent workflows) | PostgreSQL + LLM endpoint | LM Studio or OpenAI-compatible API for AI features |

## Verifying Your Setup

### Health Endpoints

After starting the API project, verify it's running:

```powershell
# Check custom health endpoint
curl http://localhost:<port>/api/health

# Expected response:
# {"status":"ok","service":"CSweet.Api"}

# Check built-in health check (from ServiceDefaults)
curl http://localhost:<port>/health
```

### Blazor App

After starting the App project, open the URL shown in the console (typically `https://localhost:<port>`) and verify:
- C-Sweet branding is visible
- Environment label shows "Development" or "Production"
- API connectivity badge shows Connected/Disconnected based on `/api/health` availability

## VS Code Launch Configuration

For a complete debug experience, create `.vscode/launch.json`:

```json
{
    "version": "0.2.0",
    "configurations": [
        {
            "name": ".NET Core Attach (AppHost)",
            "type": "coreclr",
            "request": "attach",
            "processId": "${command:pickRemoteProcess}"
        },
        {
            "name": ".NET Core Launch (CSweet.Api)",
            "type": "coreclr",
            "request": "launch",
            "preLaunchTask": "build",
            "program": "${workspaceFolder}/src/CSweet.Api/bin/Debug/net10.0/CSweet.Api.dll",
            "args": [],
            "cwd": "${workspaceFolder}/src/CSweet.Api",
            "console": "internalConsole"
        },
        {
            "name": ".NET Core Launch (CSweet.App)",
            "type": "coreclr",
            "request": "launch",
            "preLaunchTask": "build",
            "program": "${workspaceFolder}/src/CSweet.App/bin/Debug/net10.0/CSweet.App.dll",
            "args": [],
            "cwd": "${workspaceFolder}/src/CSweet.App",
            "console": "internalConsole"
        }
    ]
}
```

## Troubleshooting

### Port Already in Use
Aspire assigns random ports by default. If you need fixed ports, update the AppHost `Program.cs` with `.WithExternalHttpPorts()`.

### Aspire Dashboard Not Opening
The dashboard URL is printed to the console. Look for a line like:
```
Now listening on: https://localhost:15887
```

### "Unable to Connect" in Blazor App
When running `CSweet.App` standalone (without AppHost), the app tries to call `/api/health` relative to its own base URI. Since no API is running there, it shows "Disconnected". This is expected — run via AppHost for full connectivity.
