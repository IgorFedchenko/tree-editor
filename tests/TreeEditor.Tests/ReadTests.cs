using TreeEditor.Api;

namespace TreeEditor.Tests;

public class ReadTests(ApiFixture fixture) : ApiTest(fixture)
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

    [Fact]
    public async Task Roots_are_returned_in_id_order()
    {
        var json = await Client.GetStringAsync("/api/nodes/roots");

        Assert.Equal(
            """[{"id":1,"parentId":null,"value":"Node 1","isDeleted":false,"hasChildren":true},{"id":13,"parentId":null,"value":"Node 2","isDeleted":false,"hasChildren":true}]""",
            json);
    }

    [Fact]
    public async Task Node_comes_with_ancestors_from_root_to_parent()
    {
        var json = await Client.GetStringAsync("/api/nodes/5");

        Assert.Equal("""{"id":5,"parentId":4,"value":"Node 1.1.1.1.1","isDeleted":false,"ancestorIds":[1,2,3,4]}""", json);
        Assert.Equal([1, 2, 3, 4, 5], (await GetNode(6)).AncestorIds);
        Assert.Empty((await GetNode(13)).AncestorIds);
    }

    [Fact]
    public async Task Sample_data_has_17_nodes_with_6_levels()
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

    [Fact]
    public async Task Children_are_only_one_level_down()
    {
        var children = await Get<List<NodeDto>>("/api/nodes/1/children");

        Assert.Equal([2, 10], children.Select(c => c.Id));
        Assert.All(children, c => Assert.True(c.HasChildren));
        Assert.Empty(await Get<List<NodeDto>>("/api/nodes/9/children"));
    }

    [Fact]
    public async Task Children_of_missing_node_are_not_found()
    {
        var response = await Client.GetAsync("/api/nodes/999/children");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}
