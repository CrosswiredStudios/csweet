# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS restore
WORKDIR /src
COPY ["Directory.Build.props", "./"]
COPY ["Directory.Packages.props", "./"]
COPY ["src/CSweet.AgentHost/CSweet.AgentHost.csproj", "src/CSweet.AgentHost/"]
COPY ["src/CSweet.Agent.Contracts/CSweet.Agent.Contracts.csproj", "src/CSweet.Agent.Contracts/"]
COPY ["src/CSweet.Application/CSweet.Application.csproj", "src/CSweet.Application/"]
COPY ["src/CSweet.Domain/CSweet.Domain.csproj", "src/CSweet.Domain/"]
COPY ["src/CSweet.Contracts/CSweet.Contracts.csproj", "src/CSweet.Contracts/"]
COPY ["src/CSweet.Infrastructure/CSweet.Infrastructure.csproj", "src/CSweet.Infrastructure/"]
COPY ["src/CSweet.AI/CSweet.AI.csproj", "src/CSweet.AI/"]
COPY ["src/CSweet.ServiceDefaults/CSweet.ServiceDefaults.csproj", "src/CSweet.ServiceDefaults/"]
RUN dotnet restore "src/CSweet.AgentHost/CSweet.AgentHost.csproj"

FROM restore AS publish
COPY . .
RUN dotnet publish "src/CSweet.AgentHost/CSweet.AgentHost.csproj" -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
USER $APP_UID
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "CSweet.AgentHost.dll"]
