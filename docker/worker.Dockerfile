# syntax=docker/dockerfile:1

# ---- Restore ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS restore
WORKDIR /src
COPY ["Directory.Build.props", "./"]
COPY ["Directory.Packages.props", "./"]
COPY ["src/CSweet.WorkerHost/CSweet.WorkerHost.csproj", "src/CSweet.WorkerHost/"]
COPY ["src/CSweet.Application/CSweet.Application.csproj", "src/CSweet.Application/"]
COPY ["src/CSweet.Agent.Contracts/CSweet.Agent.Contracts.csproj", "src/CSweet.Agent.Contracts/"]
COPY ["src/CSweet.Domain/CSweet.Domain.csproj", "src/CSweet.Domain/"]
COPY ["src/CSweet.Contracts/CSweet.Contracts.csproj", "src/CSweet.Contracts/"]
COPY ["src/CSweet.Infrastructure/CSweet.Infrastructure.csproj", "src/CSweet.Infrastructure/"]
COPY ["src/CSweet.AI/CSweet.AI.csproj", "src/CSweet.AI/"]
COPY ["src/CSweet.ServiceDefaults/CSweet.ServiceDefaults.csproj", "src/CSweet.ServiceDefaults/"]
RUN dotnet restore "src/CSweet.WorkerHost/CSweet.WorkerHost.csproj"

# ---- Publish ----
FROM restore AS publish
COPY . .
RUN dotnet publish "src/CSweet.WorkerHost/CSweet.WorkerHost.csproj" -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ---- Runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
USER root
RUN apt-get update \
    && apt-get install --yes --no-install-recommends docker.io git \
    && rm -rf /var/lib/apt/lists/* \
    && mkdir -p /var/lib/csweet/agents/sources /var/lib/csweet/agents/packages \
    && chown -R $APP_UID:0 /var/lib/csweet
WORKDIR /app
COPY --from=publish /app/publish .

USER $APP_UID

ENTRYPOINT ["dotnet", "CSweet.WorkerHost.dll"]
