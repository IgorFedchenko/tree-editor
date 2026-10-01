using TreeEditor.Api;

namespace TreeEditor.Tests;

public class ResetTests(ApiFixture fixture) : ApiTest(fixture)
{
    [Fact]
    public async Task Reset_brings_back_the_sample_data()
    {
        await Apply(new
        {
            created = new[] { new { tempId = -1, parentId = 3, value = "A" } },
            updated = new[] { new { id = 10, value = "Renamed" } },
            deleted = new[] { 2 },
        });

        var response = await Client.PostAsync("/api/reset", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var nodes = await ReadNodesFromDb();
        Assert.Equal(17, nodes.Count);
        Assert.DoesNotContain(nodes, n => n.IsDeleted);
        Assert.Equal("Node 1.2", nodes.Single(n => n.Id == 10).Value);
        Assert.Equal([4, 8], (await Get<List<NodeDto>>("/api/nodes/3/children")).Select(c => c.Id));
    }

    [Fact]
    public async Task New_node_after_reset_gets_id_18()
    {
        await Apply(new { created = new[] { new { tempId = -1, parentId = 3, value = "A" } } });
        await Client.PostAsync("/api/reset", null);

        var response = await Apply(new { created = new[] { new { tempId = -1, parentId = 3, value = "B" } } });

        Assert.Equal("""{"newIds":[{"tempId":-1,"id":18}]}""", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Health_check_is_ok()
    {
        var response = await Client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
