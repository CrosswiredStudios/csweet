# C-Sweet Chief of Staff

This standalone third-party C-Sweet agent exercises GitHub import, approval, isolated build,
launch, broker authorization, and streamed conversation behavior.

## Requirements

- .NET 10 SDK
- `CSweet.Agent.SDK` 0.1.0 from NuGet.org
- A C-Sweet broker endpoint and approved agent installation

## Build

```powershell
dotnet build CSweetAgentChiefOfStaff.slnx
dotnet test CSweetAgentChiefOfStaff.slnx
```

For pre-publication SDK testing, add the directory containing `CSweet.Agent.SDK.0.1.0.nupkg` as an
additional restore source rather than changing this repository's package reference.

## Import

Push this repository publicly and paste its GitHub URL into C-Sweet's agent importer. Review and
approve only the requested permissions, events, and capabilities required by the installation.

The agent requests model responses through `platform.llm.chat-stream.v1`; provider credentials
remain inside C-Sweet and are never supplied to this process.
