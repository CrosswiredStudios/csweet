var builder = DistributedApplication.CreateBuilder(args);

var postgresUserName = builder.AddParameterFromConfiguration(
    "postgres-username",
    "CSweet:Postgres:UserName");

var postgresPassword = builder.AddParameterFromConfiguration(
    "postgres-password",
    "CSweet:Postgres:Password",
    secret: true);

var postgresDatabaseName = builder.Configuration["CSweet:Postgres:Database"]
    ?? throw new InvalidOperationException("CSweet:Postgres:Database must be configured for AppHost.");

var postgresServer = builder.AddPostgres("postgres", userName: postgresUserName, password: postgresPassword)
    .WithDataVolume("csweet-aspire-postgres");
var postgres = postgresServer.AddDatabase("csweet", postgresDatabaseName);
var relayDatabase = postgresServer.AddDatabase("relay-postgres", "csweet_discord_relay");

var repositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

var migrator = builder.AddExecutable(
        "migrator",
        "dotnet",
        repositoryRoot,
        "run",
        "--project",
        "src/CSweet.Migrator/CSweet.Migrator.csproj")
    .WithReference(postgres)
    .WaitFor(postgres);

var agentHost = builder.AddProject<Projects.CSweet_AgentHost>("agenthost")
    .WithReference(postgres)
    .WaitFor(postgres)
    .WaitForCompletion(migrator);

var api = builder.AddProject<Projects.CSweet_Api>("api")
    .WithReference(postgres)
    .WithReference(agentHost)
    .WaitFor(postgres)
    .WaitFor(agentHost)
    .WaitForCompletion(migrator);

var discordRelay = builder.AddProject<Projects.CSweet_DiscordRelay>("discord-relay")
    .WithReference(relayDatabase);

builder.AddProject<Projects.CSweet_App>("app", launchProfileName: "http")
    .WithReference(api)
    .WaitFor(api);

builder.AddProject<Projects.CSweet_WorkerHost>("workerhost")
    .WithReference(api)
    .WithReference(agentHost)
    .WithReference(postgres)
    .WithReference(discordRelay)
    .WaitFor(postgres)
    .WaitForCompletion(migrator)
    .WaitFor(api);

builder.Build().Run();
