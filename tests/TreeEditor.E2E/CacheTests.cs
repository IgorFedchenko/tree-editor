using System.Text.RegularExpressions;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace TreeEditor.E2E;

[Collection("browser")]
public class CacheTests(BrowserFixture fixture)
{
    [Fact]
    public async Task Nodes_loaded_bottom_up_find_their_places()
    {
        await using var app = await TreePage.Open(fixture);
        await app.Expand(1);
        await app.Expand(2);

        await app.Load(3, "Node 1.1.1");
        Assert.Null(await app.ParentOf(app.CacheNode(3)));
        await Expect(app.Row(app.CacheNode(3))).ToHaveTextAsync(new Regex(@"^\s*…\s*Node 1\.1\.1\s*$"));
        await Expect(app.EmptyCache).ToBeHiddenAsync();

        await app.Load(1, "Node 1");
        await Expect(app.CacheNode(1).GetByTestId("cache-node-3")).ToBeVisibleAsync();
        Assert.Equal("cache-node-1", await app.ParentOf(app.CacheNode(3)));
        await Expect(app.Marker(3)).ToHaveCountAsync(1);
        Assert.Null(await app.ParentOf(app.CacheNode(1)));
        await Expect(app.Marker(1)).ToHaveCountAsync(0);

        await app.Load(9, "Node 1.1.2");
        Assert.Equal("cache-node-1", await app.ParentOf(app.CacheNode(9)));
        await Expect(app.Marker(9)).ToHaveCountAsync(1);

        await app.Load(2, "Node 1.1");
        await Expect(app.CacheNode(2).GetByTestId("cache-node-3")).ToBeVisibleAsync();
        Assert.Equal("cache-node-1", await app.ParentOf(app.CacheNode(2)));
        Assert.Equal("cache-node-2", await app.ParentOf(app.CacheNode(3)));
        Assert.Equal("cache-node-2", await app.ParentOf(app.CacheNode(9)));
        foreach (var id in new[] { 2, 3, 9 })
        {
            await Expect(app.Marker(id)).ToHaveCountAsync(0);
        }

        await app.Load(13, "Node 2");
        Assert.Null(await app.ParentOf(app.CacheNode(13)));
        await Expect(app.VisibleCacheNodes).ToHaveCountAsync(5);
        await Expect(app.Badges).ToHaveCountAsync(0);
        await Expect(app.ApplyButton).ToBeDisabledAsync();
    }

    [Fact]
    public async Task Nodes_loaded_with_skipped_levels_move_when_the_gap_is_filled()
    {
        await using var app = await TreePage.Open(fixture);
        await app.Expand(1);
        await app.Expand(10);
        await app.Expand(13);
        await app.Expand(14);
        await app.Expand(15);

        await app.Load(10, "Node 1.2");
        Assert.Null(await app.ParentOf(app.CacheNode(10)));
        await Expect(app.Marker(10)).ToHaveCountAsync(1);

        await app.Load(12, "Node 1.2.2");
        Assert.Equal("cache-node-10", await app.ParentOf(app.CacheNode(12)));
        await Expect(app.Marker(12)).ToHaveCountAsync(0);

        await app.Load(16, "Node 2.1.1.1");
        Assert.Null(await app.ParentOf(app.CacheNode(16)));
        await Expect(app.Marker(16)).ToHaveCountAsync(1);

        await app.Load(13, "Node 2");
        Assert.Equal("cache-node-13", await app.ParentOf(app.CacheNode(16)));
        await Expect(app.Marker(16)).ToHaveCountAsync(1);
        await Expect(app.Marker(13)).ToHaveCountAsync(0);

        await app.Load(15, "Node 2.1.1");
        Assert.Equal("cache-node-13", await app.ParentOf(app.CacheNode(15)));
        await Expect(app.Marker(15)).ToHaveCountAsync(1);
        Assert.Equal("cache-node-15", await app.ParentOf(app.CacheNode(16)));
        await Expect(app.Marker(16)).ToHaveCountAsync(0);

        await app.Load(1, "Node 1");
        Assert.Equal("cache-node-1", await app.ParentOf(app.CacheNode(10)));
        await Expect(app.Marker(10)).ToHaveCountAsync(0);
        Assert.Equal("cache-node-10", await app.ParentOf(app.CacheNode(12)));
        Assert.Null(await app.ParentOf(app.CacheNode(1)));
        Assert.Null(await app.ParentOf(app.CacheNode(13)));
        await Expect(app.VisibleCacheNodes).ToHaveCountAsync(6);
    }

    [Fact]
    public async Task Value_can_be_edited_in_place()
    {
        await using var app = await TreePage.Open(fixture);
        await app.Expand(1);
        await app.Load(2, "Node 1.1");
        await app.Load(10, "Node 1.2");

        await Expect(app.Cache.Locator("[data-testid^='cache-node-'].active")).ToHaveCountAsync(0);
        await Expect(app.AddButton).ToBeDisabledAsync();
        await Expect(app.EditButton).ToBeDisabledAsync();
        await Expect(app.DeleteButton).ToBeDisabledAsync();
        await Expect(app.ApplyButton).ToBeDisabledAsync();

        await app.SelectInCache(2, "Node 1.1");
        await app.EditButton.ClickAsync();
        await Expect(app.CacheNode(2).GetByRole(AriaRole.Textbox)).ToHaveValueAsync("Node 1.1");
        await Expect(app.Editor).ToHaveAttributeAsync("maxlength", "100");
        await Expect(app.SaveButton).ToBeEnabledAsync();

        await app.Editor.FillAsync("");
        await Expect(app.SaveButton).ToBeDisabledAsync();
        await app.Editor.FillAsync("   ");
        await Expect(app.SaveButton).ToBeDisabledAsync();

        await app.CancelButton.ClickAsync();
        await Expect(app.Editor).ToHaveCountAsync(0);
        await Expect(app.Text(app.CacheNode(2), "Node 1.1")).ToBeVisibleAsync();
        await Expect(app.RowBadges(2)).ToHaveCountAsync(0);
        await Expect(app.ApplyButton).ToBeDisabledAsync();

        await app.EditButton.ClickAsync();
        await app.Editor.FillAsync("Zzz");
        await app.SelectInCache(10, "Node 1.2");
        await Expect(app.Editor).ToHaveCountAsync(0);
        await Expect(app.Text(app.CacheNode(2), "Node 1.1")).ToBeVisibleAsync();
        await Expect(app.RowBadges(2)).ToHaveCountAsync(0);
        await Expect(app.ApplyButton).ToBeDisabledAsync();

        await app.Rename(2, "Node 1.1", "Renamed");
        await Expect(app.Editor).ToHaveCountAsync(0);
        await Expect(app.Text(app.CacheNode(2), "edited")).ToBeVisibleAsync();
        await Expect(app.ApplyButton).ToBeEnabledAsync();
        await Expect(app.Text(app.DbNode(2), "Node 1.1")).ToBeVisibleAsync();

        await app.EditButton.ClickAsync();
        await app.Editor.FillAsync("  Trimmed  ");
        await app.Editor.PressAsync("Enter");
        await Expect(app.Text(app.CacheNode(2), "Trimmed")).ToBeVisibleAsync();
        await Expect(app.Editor).ToHaveCountAsync(0);
        await Expect(app.Text(app.CacheNode(2), "edited")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Child_can_be_added_to_a_loaded_and_to_a_new_node()
    {
        await using var app = await TreePage.Open(fixture);
        await app.Expand(1);
        await app.Expand(2);
        await app.Load(3, "Node 1.1.1");
        await app.SelectInCache(3, "Node 1.1.1");
        await app.AddButton.ClickAsync();
        await Expect(app.CacheNode(3).GetByTestId("cache-node--1")).ToBeVisibleAsync();
        Assert.Equal("cache-node-3", await app.ParentOf(app.CacheNode(-1)));
        await Expect(app.Text(app.CacheNode(-1), "New node")).ToBeVisibleAsync();
        await Expect(app.Text(app.CacheNode(-1), "new")).ToBeVisibleAsync();
        await Expect(app.CacheNode(-1)).ToContainClassAsync("active");
        await Expect(app.CacheNode(3)).Not.ToContainClassAsync("active");
        await Expect(app.Marker(-1)).ToHaveCountAsync(0);
        await Expect(app.ApplyButton).ToBeEnabledAsync();

        await app.AddButton.ClickAsync();
        await Expect(app.CacheNode(-1).GetByTestId("cache-node--2")).ToBeVisibleAsync();
        Assert.Equal("cache-node--1", await app.ParentOf(app.CacheNode(-2)));
        await Expect(app.Text(app.CacheNode(-2), "New node")).ToBeVisibleAsync();
        await Expect(app.Text(app.CacheNode(-2), "new")).ToBeVisibleAsync();
        await Expect(app.CacheNode(-2)).ToContainClassAsync("active");

        await app.EditButton.ClickAsync();
        await app.Editor.FillAsync("Child B");
        await app.SaveButton.ClickAsync();
        await Expect(app.Text(app.CacheNode(-2), "Child B")).ToBeVisibleAsync();
        await Expect(app.Text(app.CacheNode(-2), "new")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Deleting_a_new_node_removes_it_from_the_cache()
    {
        await using var app = await TreePage.Open(fixture);
        await app.Expand(1);
        await app.Load(10, "Node 1.2");
        await app.SelectInCache(10, "Node 1.2");
        await app.AddButton.ClickAsync();
        await Expect(app.CacheNode(-1)).ToContainClassAsync("active");
        await Expect(app.ApplyButton).ToBeEnabledAsync();

        await app.DeleteButton.ClickAsync();
        await Expect(app.CacheNode(-1)).ToHaveCountAsync(0);
        await Expect(app.ApplyButton).ToBeDisabledAsync();
        await Expect(app.Text(app.CacheNode(10), "Node 1.2")).Not.ToContainClassAsync(TreePage.StrikeClass);
        await Expect(app.RowBadges(10)).ToHaveCountAsync(0);

        await app.SelectInCache(10, "Node 1.2");
        await app.AddButton.ClickAsync();
        await app.AddButton.ClickAsync();
        var newNodes = app.CacheNode(10).Locator("[data-testid^='cache-node--']");
        await Expect(newNodes).ToHaveCountAsync(2);

        await app.Text(app.CacheNode(-2), "New node").ClickAsync();
        await app.DeleteButton.ClickAsync();
        await Expect(newNodes).ToHaveCountAsync(0);
        await Expect(app.ApplyButton).ToBeDisabledAsync();
    }

    [Fact]
    public async Task Changes_stay_in_the_cache_until_apply()
    {
        await using var app = await TreePage.Open(fixture);
        await app.Expand(1);
        await app.Expand(2);
        await app.Load(2, "Node 1.1");
        await app.Load(3, "Node 1.1.1");
        await app.Load(10, "Node 1.2");
        var mark = app.Mark();

        await app.Rename(2, "Node 1.1", "Renamed");
        await app.SelectInCache(3, "Node 1.1.1");
        await app.AddButton.ClickAsync();
        await app.SelectInCache(10, "Node 1.2");
        await app.DeleteButton.ClickAsync();
        await app.Pause();
        Assert.Empty(app.Since(mark));

        await Expect(app.Text(app.DbNode(2), "Node 1.1")).ToBeVisibleAsync();
        Assert.Equal("Node 1.1", await app.NodeValue(2));
        Assert.Equal(new[] { 4, 8 }, await app.ChildIds(3));
        Assert.False(await app.IsDeletedInDb(10));
        await Expect(app.Text(app.DbNode(10), "Node 1.2")).Not.ToContainClassAsync(TreePage.StrikeClass);
    }
}
