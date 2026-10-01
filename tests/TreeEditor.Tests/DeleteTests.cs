using TreeEditor.Api;

namespace TreeEditor.Tests;

public class DeleteTests(ApiFixture fixture) : ApiTest(fixture)
{
    [Fact]
    public async Task Delete_marks_the_whole_subtree_with_one_time()
    {
        var response = await Apply(new { deleted = new[] { 2 } });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var nodes = await ReadNodesFromDb();
        var deleted = nodes.Where(n => n.IsDeleted).ToList();
        Assert.Equal([2, 3, 4, 5, 6, 7, 8, 9], deleted.Select(n => n.Id));
        Assert.Single(deleted.Select(n => n.DeletedAt).Distinct());
        Assert.NotNull(deleted[0].DeletedAt);
        Assert.All(nodes.Except(deleted), n => Assert.Null(n.DeletedAt));
        Assert.Equal(9, nodes.Count - deleted.Count);
    }

    [Fact]
    public async Task Delete_reaches_nodes_that_were_never_loaded()
    {
        var deepestId = (await CreateChain(6, 100))[^1].Id;

        await Apply(new { deleted = new[] { 2 } });

        var nodes = await ReadNodesFromDb();
        Assert.Equal(108, nodes.Count(n => n.IsDeleted));
        Assert.True((await GetNode(deepestId)).IsDeleted);
        Assert.False((await GetNode(10)).IsDeleted);
    }

    [Fact]
    public async Task Deleted_node_is_still_returned_with_a_flag()
    {
        await Apply(new { deleted = new[] { 2 } });

        Assert.True((await GetNode(2)).IsDeleted);
        var children = await Get<List<NodeDto>>("/api/nodes/1/children");
        Assert.True(children.Single(c => c.Id == 2).IsDeleted);
        Assert.False(children.Single(c => c.Id == 10).IsDeleted);
    }

    [Fact]
    public async Task Deleted_node_cannot_be_edited_or_get_a_child()
    {
        await Apply(new { deleted = new[] { 2 } });

        var update = await Apply(new { updated = new[] { new { id = 2, value = "X" } } });
        var child = await Apply(new { created = new[] { new { tempId = -1, parentId = 2, value = "A" } } });
        var deepUpdate = await Apply(new { updated = new[] { new { id = 5, value = "X" } } });

        Assert.Equal(HttpStatusCode.Conflict, update.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, child.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, deepUpdate.StatusCode);
        Assert.Equal("application/problem+json", update.Content.Headers.ContentType?.MediaType);
        Assert.Contains("deleted", await update.Content.ReadAsStringAsync());
        Assert.Equal("Node 1.1", (await GetNode(2)).Value);
    }

    [Fact]
    public async Task Second_delete_does_not_change_the_deleted_time()
    {
        await Apply(new { deleted = new[] { 2 } });
        var first = (await ReadNodesFromDb()).Single(n => n.Id == 2).DeletedAt;

        var response = await Apply(new { deleted = new[] { 2 } });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(first, (await ReadNodesFromDb()).Single(n => n.Id == 2).DeletedAt);
    }

    [Fact]
    public async Task Delete_of_missing_node_is_not_found()
    {
        var response = await Apply(new { deleted = new[] { 999 } });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Node_created_and_deleted_in_one_apply_ends_up_deleted()
    {
        var response = await Apply(new
        {
            created = new[] { new { tempId = -1, parentId = 10, value = "New" } },
            deleted = new[] { 10 },
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True((await GetNode(18)).IsDeleted);
    }
}
