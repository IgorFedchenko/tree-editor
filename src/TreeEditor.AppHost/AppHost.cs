var builder = DistributedApplication.CreateBuilder(args);

var db = builder.AddPostgres("pg")
    .WithImageTag("18.3")
    .AddDatabase("treedb");

var api = builder.AddProject<Projects.TreeEditor_Api>("api")
    .WithReference(db)
    .WaitFor(db)
    .WithHttpHealthCheck("/health");

builder.AddViteApp("web", "../TreeEditor.Web")
    .WithEndpoint("http", e => e.Port = 5173)
    .WithReference(api)
    .WaitFor(api);

builder.Build().Run();
