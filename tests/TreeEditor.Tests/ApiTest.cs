using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TreeEditor.Api;

namespace TreeEditor.Tests;

[Collection("api")]
public abstract class ApiTest(ApiFixture fixture) : IAsyncLifetime
{
    protected HttpClient Client { get; } = fixture.Factory.CreateClient();

    public async Task InitializeAsync()
    {
        var response = await Client.PostAsync("/api/reset", null);
        response.EnsureSuccessStatusCode();
    }

    public Task DisposeAsync()
    {
        Client.Dispose();
        return Task.CompletedTask;
    }

    protected Task<HttpResponseMessage> Apply(object request) => Client.PostAsJsonAsync("/api/apply", request);

    // a chain of new nodes: the first one under parentId, each next one under the previous
    protected async Task<List<NewId>> CreateChain(int parentId, int length)
    {
        var chain = Enumerable.Range(1, length)
            .Select(i => new { tempId = -i, parentId = i == 1 ? parentId : -(i - 1), value = $"Deep {i}" })
            .ToArray();
        var response = await Apply(new { created = chain });
        return (await response.Content.ReadFromJsonAsync<ApplyResponse>())!.NewIds;
    }

    protected async Task<T> Get<T>(string url) => (await Client.GetFromJsonAsync<T>(url))!;

    protected async Task<NodeDetailsDto> GetNode(int id) => await Get<NodeDetailsDto>($"/api/nodes/{id}");

    protected async Task<List<Node>> ReadNodesFromDb()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TreeDbContext>();
        return await db.Nodes.AsNoTracking().OrderBy(n => n.Id).ToListAsync();
    }
}
