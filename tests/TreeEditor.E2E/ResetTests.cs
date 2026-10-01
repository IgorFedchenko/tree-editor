using static Microsoft.Playwright.Assertions;

namespace TreeEditor.E2E;

[Collection("browser")]
public class ResetTests(BrowserFixture fixture)
{
    [Fact]
    public async Task Reset_clears_the_cache_collapses_the_tree_and_restores_the_database()
    {
        var changes = """{"updated":[{"id":2,"value":"Applied"}],"deleted":[10]}""";
        await using var app = await TreePage.Open(fixture, changes);
        await app.Expand(1);
        await app.Expand(2);
        await Expect(app.Text(app.DbNode(2), "Applied")).ToBeVisibleAsync();
        await Expect(app.Text(app.DbNode(10), "Node 1.2")).ToContainClassAsync(TreePage.StrikeClass);

        await app.Load(2, "Applied");
        await app.Load(3, "Node 1.1.1");
        await app.Rename(2, "Applied", "Unsaved");
        await app.SelectInCache(3, "Node 1.1.1");
        await app.AddButton.ClickAsync();
        await Expect(app.Text(app.CacheNode(2), "edited")).ToBeVisibleAsync();
        await Expect(app.Text(app.CacheNode(3).GetByTestId("cache-node--1"), "new")).ToBeVisibleAsync();
        await Expect(app.ApplyButton).ToBeEnabledAsync();

        await app.ResetButton.ClickAsync();
        await Expect(app.EmptyCache).ToBeVisibleAsync();
        await Expect(app.ApplyButton).ToBeDisabledAsync();
        await Expect(app.VisibleDbNodes).ToHaveCountAsync(2);
        await Expect(app.Text(app.DbNode(1), "Node 1")).ToBeVisibleAsync();
        await Expect(app.Text(app.DbNode(13), "Node 2")).ToBeVisibleAsync();
        await Expect(app.DbNode(2)).ToBeHiddenAsync();
        await Expect(app.Toggle(1)).ToHaveTextAsync("+");

        Assert.Equal("Node 1.1", await app.NodeValue(2));
        Assert.False(await app.IsDeletedInDb(2));

        await app.Expand(1);
        await Expect(app.Text(app.DbNode(2), "Node 1.1")).Not.ToContainClassAsync(TreePage.StrikeClass);
        await Expect(app.Text(app.DbNode(10), "Node 1.2")).Not.ToContainClassAsync(TreePage.StrikeClass);

        await app.Expand(2);
        await app.Load(3, "Node 1.1.1");
        await app.SelectInCache(3, "Node 1.1.1");
        await app.AddButton.ClickAsync();
        var response = await app.ClickApply();
        Assert.Equal(200, response.Status);
        await Expect(app.CacheNode(18)).ToBeVisibleAsync();
    }
}
