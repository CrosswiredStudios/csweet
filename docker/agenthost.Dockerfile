# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0.203 AS restore
WORKDIR /src
COPY . .
RUN dotnet restore "src/CSweet.AgentHost/CSweet.AgentHost.csproj"

FROM restore AS publish
RUN dotnet publish "src/CSweet.AgentHost/CSweet.AgentHost.csproj" -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
RUN mkdir -p /state && chown -R $APP_UID:$APP_UID /state
USER $APP_UID
ENV ASPNETCORE_URLS=http://+:8080
ENV CSweet__Secrets__FilePath=/state/provider-secrets.json
EXPOSE 8080
EXPOSE 8081
ENTRYPOINT ["dotnet", "CSweet.AgentHost.dll"]
