using TreeEditor.Api;

namespace TreeEditor.Tests;

public class ReadTests(ApiFixture fixture) : ApiTest(fixture)
{
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
        await AssertSampleData();
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
    public async Task Health_check_is_ok()
    {
        var response = await Client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_route_is_not_found_with_problem_details()
    {
        var response = await Client.GetAsync("/api/nodes/abc");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Children_of_missing_node_are_not_found()
    {
        var response = await Client.GetAsync("/api/nodes/999/children");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}
