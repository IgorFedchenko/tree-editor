using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;
using TreeEditor.Api;

namespace TreeEditor.Tests;

public class ApiFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:18.3").Build();

    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await postgres.StartAsync();
        Factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(host => host.UseSetting("ConnectionStrings:treedb", postgres.GetConnectionString()));
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        await postgres.DisposeAsync();
    }
}

[CollectionDefinition("api")]
public class ApiCollection : ICollectionFixture<ApiFixture>;
