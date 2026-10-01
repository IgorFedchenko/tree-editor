using System.Net.Http.Json;
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
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
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

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task Empty_value_is_a_bad_request(string? value)
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
        var tooLong = await Apply(new { updated = new[] { new { id = 3, value = new string('a', 101) } } });
        var maxLength = await Apply(new { updated = new[] { new { id = 3, value = new string('a', 100) } } });

        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
        Assert.Equal(HttpStatusCode.OK, maxLength.StatusCode);
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
        var chain = Enumerable.Range(1, 100)
            .Select(i => new { tempId = -i, parentId = i == 1 ? 6 : -(i - 1), value = $"Deep {i}" })
            .ToArray();

        var response = await Apply(new { created = chain });

        var result = (await response.Content.ReadFromJsonAsync<ApplyResponse>())!;
        Assert.Equal(100, result.NewIds.Count);
        var deepest = await GetNode(result.NewIds[^1].Id);
        Assert.Equal(105, deepest.AncestorIds.Count);
        Assert.Equal(result.NewIds[^2].Id, deepest.ParentId);
    }
}
