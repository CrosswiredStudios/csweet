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

var repositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
var workspaceRoot = Directory.GetParent(repositoryRoot)?.FullName
    ?? throw new InvalidOperationException("The C-Sweet repository must have a workspace parent directory.");
var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
var localStateDirectory = string.IsNullOrWhiteSpace(localAppData)
    ? Path.Combine(repositoryRoot, ".csweet")
    : Path.Combine(localAppData, "CSweet");
Directory.CreateDirectory(localStateDirectory);

var migrator = builder.AddExecutable(
        "migrator",
        "dotnet",
        repositoryRoot,
        "run",
        "--project",
        "src/CSweet.Migrator/CSweet.Migrator.csproj")
    .WithReference(postgres)
    .WaitFor(postgres);

// Agent runtimes execute on private Docker networks. Keeping the broker in a
// real container lets the runtime manager attach only this gateway to each
// network instead of exposing the runtime to the host or Aspire network.
var agentHost = builder.AddDockerfile(
        "agenthost",
        workspaceRoot,
        Path.Combine("csweet", "docker", "agenthost.Dockerfile"))
    .WithContainerName("agenthost")
    .WithHttpEndpoint(targetPort: 8080, name: "http")
    .WithBindMount(localStateDirectory, "/state")
    .WithEnvironment("CSweet__Secrets__FilePath", "/state/provider-secrets.json")
    .WithReference(postgres)
    .WaitFor(postgres)
    .WaitForCompletion(migrator);
var agentHostEndpoint = agentHost.GetEndpoint("http");

var api = builder.AddProject<Projects.CSweet_Api>("api")
    .WithReference(postgres)
    .WithReference(agentHostEndpoint)
    .WithEnvironment("CSweet__ApiGateway__BrokerEndpoint", agentHostEndpoint)
    .WithEnvironment("CSweet__CommunicationPlugins__BrokerEndpoint", agentHostEndpoint)
    .WaitFor(postgres)
    .WaitFor(agentHost)
    .WaitForCompletion(migrator);

builder.AddProject<Projects.CSweet_App>("app", launchProfileName: "http")
    .WithReference(api)
    .WaitFor(api);

builder.AddProject<Projects.CSweet_WorkerHost>("workerhost")
    .WithReference(api)
    .WithReference(agentHostEndpoint)
    .WithEnvironment("CSweet__CommunicationPlugins__BrokerEndpoint", agentHostEndpoint)
    .WithReference(postgres)
    .WaitFor(postgres)
    .WaitForCompletion(migrator)
    .WaitFor(api);

builder.Build().Run();
