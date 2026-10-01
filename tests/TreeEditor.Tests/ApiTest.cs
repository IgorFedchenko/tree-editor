using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TreeEditor.Api;

namespace TreeEditor.Tests;

[Collection("api")]
public abstract class ApiTest(ApiFixture fixture) : IAsyncLifetime
{
    private static readonly (int Id, int? ParentId, string Value)[] SampleNodes =
    [
        (1, null, "Node 1"),
        (2, 1, "Node 1.1"),
        (3, 2, "Node 1.1.1"),
        (4, 3, "Node 1.1.1.1"),
        (5, 4, "Node 1.1.1.1.1"),
        (6, 5, "Node 1.1.1.1.1.1"),
        (7, 4, "Node 1.1.1.1.2"),
        (8, 3, "Node 1.1.1.2"),
        (9, 2, "Node 1.1.2"),
        (10, 1, "Node 1.2"),
        (11, 10, "Node 1.2.1"),
        (12, 10, "Node 1.2.2"),
        (13, null, "Node 2"),
        (14, 13, "Node 2.1"),
        (15, 14, "Node 2.1.1"),
        (16, 15, "Node 2.1.1.1"),
        (17, 13, "Node 2.2"),
    ];

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

    protected async Task AssertSampleData()
    {
        foreach (var (id, parentId, value) in SampleNodes)
        {
            var node = await GetNode(id);
            Assert.Equal(parentId, node.ParentId);
            Assert.Equal(value, node.Value);
            Assert.False(node.IsDeleted);
        }

        var response = await Client.GetAsync("/api/nodes/18");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    protected async Task<List<Node>> ReadNodesFromDb()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TreeDbContext>();
        return await db.Nodes.AsNoTracking().OrderBy(n => n.Id).ToListAsync();
    }
}
