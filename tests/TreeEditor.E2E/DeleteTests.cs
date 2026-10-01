using static Microsoft.Playwright.Assertions;

namespace TreeEditor.E2E;

[Collection("browser")]
public class DeleteTests(BrowserFixture fixture)
{
    [Fact]
    public async Task Delete_reaches_loaded_descendants_behind_unloaded_levels()
    {
        await using var app = await TreePage.Open(fixture);
        foreach (var id in new[] { 1, 2, 3, 4 })
        {
            await app.Expand(id);
        }
        await app.Load(2, "Node 1.1");
        await app.Load(5, "Node 1.1.1.1.1");
        Assert.Equal("cache-node-2", await app.ParentOf(app.CacheNode(5)));
        await Expect(app.Marker(5)).ToHaveCountAsync(1);

        await app.SelectInCache(2, "Node 1.1");
        await app.DeleteButton.ClickAsync();
        await Expect(app.Text(app.CacheNode(2), "Node 1.1")).ToContainClassAsync(TreePage.StrikeClass);
        await Expect(app.Text(app.CacheNode(5), "Node 1.1.1.1.1")).ToContainClassAsync(TreePage.StrikeClass);
        await Expect(app.Text(app.CacheNode(2), "deleted")).ToBeVisibleAsync();
        await Expect(app.Text(app.CacheNode(5), "deleted")).ToBeVisibleAsync();
        await Expect(app.ApplyButton).ToBeEnabledAsync();

        await app.Load(4, "Node 1.1.1.1");
        await Expect(app.Text(app.CacheNode(4), "Node 1.1.1.1")).ToContainClassAsync(TreePage.StrikeClass);
        await Expect(app.Text(app.CacheNode(4), "deleted")).ToBeVisibleAsync();
        Assert.Equal("cache-node-2", await app.ParentOf(app.CacheNode(4)));
        await Expect(app.Marker(4)).ToHaveCountAsync(1);
        Assert.Equal("cache-node-4", await app.ParentOf(app.CacheNode(5)));
        await Expect(app.Marker(5)).ToHaveCountAsync(0);

        await app.SelectInCache(4, "Node 1.1.1.1");
        await Expect(app.EditButton).ToBeDisabledAsync();
    }

    [Fact]
    public async Task Apply_deletes_descendants_that_were_never_loaded()
    {
        await using var app = await TreePage.Open(fixture);
        await app.Expand(1);
        await app.Load(2, "Node 1.1");
        await app.SelectInCache(2, "Node 1.1");
        await app.DeleteButton.ClickAsync();

        var response = await app.ClickApply();
        Assert.Equal(200, response.Status);
        TreePage.AssertJson("""{"created":[],"updated":[],"deleted":[2]}""", response.Request.PostData!);
        TreePage.AssertJson("""{"newIds":[]}""", await response.TextAsync());

        await Expect(app.Text(app.CacheNode(2), "Node 1.1")).ToContainClassAsync(TreePage.StrikeClass);
        await Expect(app.RowBadges(2)).ToHaveCountAsync(0);
        await Expect(app.ApplyButton).ToBeDisabledAsync();

        foreach (var id in Enumerable.Range(1, 17))
        {
            var deleted = id is >= 2 and <= 9;
            Assert.Equal(deleted, await app.IsDeletedInDb(id));
        }

        await Expect(app.Toggle(1)).ToHaveTextAsync("−");
        await Expect(app.Text(app.DbNode(2), "Node 1.1")).ToContainClassAsync(TreePage.StrikeClass);
        await app.Expand(2);
        await Expect(app.Text(app.DbNode(3), "Node 1.1.1")).ToContainClassAsync(TreePage.StrikeClass);
        await Expect(app.Text(app.DbNode(9), "Node 1.1.2")).ToContainClassAsync(TreePage.StrikeClass);
        await Expect(app.Text(app.DbNode(10), "Node 1.2")).Not.ToContainClassAsync(TreePage.StrikeClass);
        await Expect(app.Text(app.DbNode(1), "Node 1")).Not.ToContainClassAsync(TreePage.StrikeClass);
    }

    [Fact]
    public async Task Edits_are_applied_to_nodes_deleted_after_them()
    {
        await using var app = await TreePage.Open(fixture);
        await app.Expand(1);
        await app.Expand(2);
        await app.Load(2, "Node 1.1");
        await app.Load(3, "Node 1.1.1");
        await app.Rename(3, "Node 1.1.1", "Edited");
        await app.Rename(2, "Node 1.1", "Renamed");
        await app.DeleteButton.ClickAsync();

        var response = await app.ClickApply();
        Assert.Equal(200, response.Status);
        var expectedRequest = """
            {"created":[],"updated":[{"id":2,"value":"Renamed"},{"id":3,"value":"Edited"}],"deleted":[2,3]}
            """;
        TreePage.AssertJson(expectedRequest, response.Request.PostData!);

        await Expect(app.Text(app.CacheNode(2), "Renamed")).ToContainClassAsync(TreePage.StrikeClass);
        await Expect(app.Text(app.CacheNode(3), "Edited")).ToContainClassAsync(TreePage.StrikeClass);
        Assert.Equal("Renamed", await app.NodeValue(2));
        Assert.Equal("Edited", await app.NodeValue(3));
    }

    [Fact]
    public async Task Deleted_node_cannot_be_edited()
    {
        await using var app = await TreePage.Open(fixture, """{"deleted":[2]}""");
        await app.Expand(1);
        await app.Expand(2);
        await Expect(app.Text(app.DbNode(2), "Node 1.1")).ToContainClassAsync(TreePage.StrikeClass);
        await Expect(app.Text(app.DbNode(9), "Node 1.1.2")).ToContainClassAsync(TreePage.StrikeClass);

        await app.Load(2, "Node 1.1");
        await app.SelectInCache(2, "Node 1.1");
        await Expect(app.Text(app.CacheNode(2), "Node 1.1")).ToContainClassAsync(TreePage.StrikeClass);
        await Expect(app.RowBadges(2)).ToHaveCountAsync(0);
        await Expect(app.EditButton).ToBeDisabledAsync();
        await Expect(app.AddButton).ToBeDisabledAsync();
        await Expect(app.DeleteButton).ToBeDisabledAsync();

        await app.Load(9, "Node 1.1.2");
        await app.SelectInCache(9, "Node 1.1.2");
        await Expect(app.Text(app.CacheNode(9), "Node 1.1.2")).ToContainClassAsync(TreePage.StrikeClass);
        await Expect(app.RowBadges(9)).ToHaveCountAsync(0);
        Assert.Equal("cache-node-2", await app.ParentOf(app.CacheNode(9)));
        await Expect(app.Marker(9)).ToHaveCountAsync(0);
        await Expect(app.EditButton).ToBeDisabledAsync();

        await app.Load(10, "Node 1.2");
        await app.SelectInCache(10, "Node 1.2");
        await app.DeleteButton.ClickAsync();
        await app.Text(app.CacheNode(10), "Node 1.2").ClickAsync();
        await Expect(app.Text(app.CacheNode(10), "Node 1.2")).ToContainClassAsync(TreePage.StrikeClass);
        await Expect(app.Text(app.CacheNode(10), "deleted")).ToBeVisibleAsync();
        await Expect(app.CacheNode(10)).ToContainClassAsync("active");
        await Expect(app.EditButton).ToBeDisabledAsync();
        await Expect(app.ApplyButton).ToBeEnabledAsync();
    }
}
