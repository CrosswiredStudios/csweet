// Aspire AppHost for local development orchestration.
using Aspire.Hosting.ApplicationModel;

var builder = DistributedApplication.CreateBuilder(args);

var postgresPassword = builder.AddParameter(
    "postgres-password",
    new GenerateParameterDefault
    {
        MinLength = 22,
        Special = false
    },
    secret: true,
    persist: true);

var postgres = builder.AddPostgres("postgres", password: postgresPassword)
    .WithDataVolume("csweet-aspire-postgres")
    .AddDatabase("csweet");

var repositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

var migrator = builder.AddExecutable(
        "migrator",
        "dotnet",
        repositoryRoot,
        "run",
        "--project",
        "src/CSweet.Migrator/CSweet.Migrator.csproj",
        "--no-build")
    .WithReference(postgres)
    .WaitFor(postgres);

var api = builder.AddProject<Projects.CSweet_Api>("api")
    .WithReference(postgres)
    .WaitFor(postgres)
    .WaitForCompletion(migrator);

builder.AddProject<Projects.CSweet_App>("app")
    .WithReference(api)
    .WaitFor(api);

builder.AddProject<Projects.CSweet_WorkerHost>("workerhost")
    .WithReference(api)
    .WithReference(postgres)
    .WaitFor(postgres)
    .WaitForCompletion(migrator)
    .WaitFor(api);

builder.Build().Run();
