using System.Text;
using TreeEditor.Api;

namespace TreeEditor.Tests;

public class ApplyTests(ApiFixture fixture) : ApiTest(fixture)
{
    [Fact]
    public async Task Created_nodes_get_real_ids_and_updated_value_is_saved()
    {
        var response = await Apply(new
        {
            created = new object[]
            {
                new { tempId = -1, parentId = 3, value = "Child A" },
                new { tempId = -2, parentId = -1, value = "Child B" },
            },
            updated = new[] { new { id = 2, value = "Renamed" } },
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("""{"newIds":[{"tempId":-1,"id":18},{"tempId":-2,"id":19}]}""", await response.Content.ReadAsStringAsync());

        var childB = await GetNode(19);
        Assert.Equal(18, childB.ParentId);
        Assert.Equal([1, 2, 3, 18], childB.AncestorIds);
        Assert.Equal("Renamed", (await GetNode(2)).Value);
    }

    [Fact]
    public async Task Nothing_is_saved_when_one_change_fails()
    {
        var response = await Apply(new
        {
            created = new[] { new { tempId = -1, parentId = 3, value = "A" } },
            updated = new[] { new { id = 999, value = "X" } },
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal([4, 8], (await Get<List<NodeDto>>("/api/nodes/3/children")).Select(c => c.Id));
    }

    [Fact]
    public async Task Nothing_is_saved_when_delete_fails_after_create()
    {
        var response = await Apply(new
        {
            created = new[] { new { tempId = -1, parentId = 3, value = "A" } },
            deleted = new[] { 999 },
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(17, (await ReadNodesFromDb()).Count);
    }

    [Fact]
    public async Task Update_written_before_a_failed_delete_is_rolled_back()
    {
        var response = await Apply(new
        {
            updated = new[] { new { id = 3, value = "Changed" } },
            deleted = new[] { 999 },
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Node 1.1.1", (await GetNode(3)).Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("a\0b")]
    public async Task Wrong_value_is_a_bad_request(string? value)
    {
        var created = await Apply(new { created = new[] { new { tempId = -1, parentId = 3, value } } });
        var updated = await Apply(new { updated = new[] { new { id = 3, value } } });

        Assert.Equal(HttpStatusCode.BadRequest, created.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, updated.StatusCode);
        Assert.Equal(17, (await ReadNodesFromDb()).Count);
        Assert.Equal("Node 1.1.1", (await GetNode(3)).Value);
    }

    [Fact]
    public async Task Value_longer_than_100_characters_is_a_bad_request()
    {
        var tooLongUpdate = await Apply(new { updated = new[] { new { id = 3, value = new string('a', 101) } } });
        var tooLongCreate = await Apply(new { created = new[] { new { tempId = -1, parentId = 3, value = new string('a', 101) } } });
        var maxLength = await Apply(new { updated = new[] { new { id = 3, value = new string('a', 100) } } });

        Assert.Equal(HttpStatusCode.BadRequest, tooLongUpdate.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, tooLongCreate.StatusCode);
        Assert.Equal(HttpStatusCode.OK, maxLength.StatusCode);
        Assert.Equal(100, (await GetNode(3)).Value.Length);
        Assert.Equal([4, 8], (await Get<List<NodeDto>>("/api/nodes/3/children")).Select(c => c.Id));
    }

    [Theory]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("""{"created":[""")]
    [InlineData("""{"updated":[{"id":"x","value":"a"}]}""")]
    [InlineData("""{"created":[null]}""")]
    [InlineData("""{"updated":[null]}""")]
    public async Task Malformed_request_is_a_bad_request(string body)
    {
        var response = await Client.PostAsync("/api/apply", new StringContent(body, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Value_is_trimmed_on_save()
    {
        await Apply(new { updated = new[] { new { id = 3, value = "  X  " } } });

        Assert.Equal("X", (await GetNode(3)).Value);
    }

    [Fact]
    public async Task Parent_must_exist_or_be_created_before_its_child()
    {
        var notCreated = await Apply(new { created = new[] { new { tempId = -1, parentId = -5, value = "A" } } });
        var missing = await Apply(new { created = new[] { new { tempId = -1, parentId = 999, value = "A" } } });
        var noParent = await Apply(new { created = new[] { new { tempId = -1, value = "A" } } });
        var childFirst = await Apply(new
        {
            created = new[]
            {
                new { tempId = -2, parentId = -1, value = "child" },
                new { tempId = -1, parentId = 3, value = "parent" },
            },
        });

        Assert.Equal(HttpStatusCode.BadRequest, notCreated.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, noParent.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, childFirst.StatusCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    public async Task Temporary_id_must_be_negative(int tempId)
    {
        var response = await Apply(new { created = new[] { new { tempId, parentId = 3, value = "A" } } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Temporary_id_must_be_unique()
    {
        var response = await Apply(new
        {
            created = new[]
            {
                new { tempId = -1, parentId = 3, value = "A" },
                new { tempId = -1, parentId = 3, value = "B" },
            },
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Parent_cannot_be_changed()
    {
        var response = await Apply(new { updated = new[] { new { id = 3, value = "X", parentId = 10 } } });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var node = await GetNode(3);
        Assert.Equal(2, node.ParentId);
        Assert.Equal("X", node.Value);
    }

    [Fact]
    public async Task Tree_can_be_very_deep()
    {
        var newIds = await CreateChain(6, 100);

        Assert.Equal(100, newIds.Count);
        var deepest = await GetNode(newIds[^1].Id);
        Assert.Equal(105, deepest.AncestorIds.Count);
        Assert.Equal(newIds[^2].Id, deepest.ParentId);
    }
}
