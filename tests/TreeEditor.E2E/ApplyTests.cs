using System.Text.Json.Nodes;
using static Microsoft.Playwright.Assertions;

namespace TreeEditor.E2E;

[Collection("browser")]
public class ApplyTests(BrowserFixture fixture)
{
    [Fact]
    public async Task Apply_sends_all_changes_in_one_request_and_refreshes_both_trees()
    {
        await using var app = await TreePage.Open(fixture);
        await app.Expand(1);
        await app.Expand(2);
        await app.Expand(3);
        await app.Load(2, "Node 1.1");
        await app.Load(3, "Node 1.1.1");
        await app.Load(10, "Node 1.2");

        await app.Rename(2, "Node 1.1", "Renamed");
        await app.SelectInCache(10, "Node 1.2");
        await app.DeleteButton.ClickAsync();
        await app.SelectInCache(3, "Node 1.1.1");
        await app.AddButton.ClickAsync();
        await app.AddButton.ClickAsync();
        await Expect(app.CacheNode(-2)).ToContainClassAsync("active");

        var mark = app.Mark();
        var response = await app.ClickApply();

        var expectedRequest = """
            {"created":[{"tempId":-1,"parentId":3,"value":"New node"},{"tempId":-2,"parentId":-1,"value":"New node"}],
             "updated":[{"id":2,"value":"Renamed"}],
             "deleted":[10]}
            """;
        TreePage.AssertJson(expectedRequest, response.Request.PostData!);
        Assert.Equal(200, response.Status);
        TreePage.AssertJson("""{"newIds":[{"tempId":-1,"id":18},{"tempId":-2,"id":19}]}""", await response.TextAsync());

        await Expect(app.CacheNode(18)).ToBeVisibleAsync();
        await Expect(app.CacheNode(19)).ToBeVisibleAsync();
        await Expect(app.DbNode(18)).ToBeVisibleAsync();
        await Expect(app.Badges).ToHaveCountAsync(0);
        await Expect(app.ApplyButton).ToBeDisabledAsync();
        await app.Pause();
        var requests = app.Since(mark);
        Assert.Equal("POST /api/apply", requests[0]);
        Assert.Single(requests, x => x.StartsWith("POST "));

        await Expect(app.Text(app.CacheNode(18), "New node")).ToBeVisibleAsync();
        await Expect(app.Text(app.CacheNode(19), "New node")).ToBeVisibleAsync();
        Assert.Equal("cache-node-3", await app.ParentOf(app.CacheNode(18)));
        Assert.Equal("cache-node-18", await app.ParentOf(app.CacheNode(19)));
        await Expect(app.CacheNode(-1)).ToHaveCountAsync(0);
        await Expect(app.CacheNode(-2)).ToHaveCountAsync(0);
        await Expect(app.CacheNode(19)).ToContainClassAsync("active");
        await Expect(app.Text(app.CacheNode(2), "Renamed")).ToBeVisibleAsync();
        await Expect(app.Text(app.CacheNode(10), "Node 1.2")).ToContainClassAsync(TreePage.StrikeClass);

        await Expect(app.Text(app.DbNode(2), "Renamed")).ToBeVisibleAsync();
        Assert.Equal("db-node-1", await app.ParentOf(app.DbNode(2)));
        foreach (var id in new[] { 1, 2, 3 })
        {
            await Expect(app.Toggle(id)).ToHaveTextAsync("−");
        }
        await Expect(app.DbNode(3).GetByTestId("db-node-4")).ToBeVisibleAsync();
        await Expect(app.DbNode(3).GetByTestId("db-node-8")).ToBeVisibleAsync();
        await Expect(app.DbNode(3).GetByTestId("db-node-18")).ToBeVisibleAsync();
        Assert.Equal("db-node-3", await app.ParentOf(app.DbNode(18)));
        await Expect(app.Text(app.DbNode(18), "New node")).ToBeVisibleAsync();
        await Expect(app.Toggle(18)).ToHaveTextAsync("+");
        await Expect(app.DbNode(19)).ToBeHiddenAsync();
        await Expect(app.Text(app.DbNode(10), "Node 1.2")).ToContainClassAsync(TreePage.StrikeClass);

        await app.SelectInDb(18, "New node");
        await Expect(app.LoadButton).ToBeDisabledAsync();

        Assert.Equal("Renamed", await app.NodeValue(2));
        Assert.Equal(new[] { 4, 8, 18 }, await app.ChildIds(3));
        Assert.True(await app.IsDeletedInDb(11));
    }

    [Fact]
    public async Task Apply_of_a_stale_cache_shows_the_error_and_keeps_the_changes()
    {
        await using var app = await TreePage.Open(fixture);
        await app.Expand(1);
        await app.Load(10, "Node 1.2");
        await Expect(app.Text(app.CacheNode(10), "Node 1.2")).Not.ToContainClassAsync(TreePage.StrikeClass);

        var deleteInDb = await app.PostApply("""{"deleted":[10]}""");
        Assert.Equal(200, deleteInDb.Status);

        await app.Rename(10, "Node 1.2", "X");
        await Expect(app.Text(app.CacheNode(10), "edited")).ToBeVisibleAsync();
        await Expect(app.ApplyButton).ToBeEnabledAsync();

        var response = await app.ClickApply();
        Assert.Equal(409, response.Status);
        Assert.StartsWith("application/problem+json", response.Headers["content-type"]);
        var detail = JsonNode.Parse(await response.TextAsync())!["detail"]!.GetValue<string>();
        Assert.Contains("deleted", detail);

        await Expect(app.Alert).ToBeVisibleAsync();
        await Expect(app.Alert).ToContainTextAsync(detail);
        await Expect(app.Text(app.CacheNode(10), "X")).ToBeVisibleAsync();
        await Expect(app.Text(app.CacheNode(10), "edited")).ToBeVisibleAsync();
        await Expect(app.ApplyButton).ToBeEnabledAsync();

        Assert.Equal("Node 1.2", await app.NodeValue(10));
        Assert.True(await app.IsDeletedInDb(10));

        await app.ResetButton.ClickAsync();
        await Expect(app.Alert).ToHaveCountAsync(0);
        await Expect(app.EmptyCache).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Buttons_are_locked_while_a_request_is_running()
    {
        await using var app = await TreePage.Open(fixture);
        await app.Expand(1);
        await app.Expand(2);
        await app.Load(3, "Node 1.1.1");
        await app.SelectInCache(3, "Node 1.1.1");
        await app.AddButton.ClickAsync();
        await Expect(app.CacheNode(-1)).ToContainClassAsync("active");

        await app.Page.RouteAsync("**/api/apply", async route =>
        {
            await Task.Delay(2000);
            await route.ContinueAsync();
        });
        await app.ApplyButton.ClickAsync();
        var buttons = new[] { app.AddButton, app.EditButton, app.DeleteButton, app.ApplyButton, app.LoadButton, app.ResetButton };
        foreach (var button in buttons)
        {
            await Expect(button).ToBeDisabledAsync();
        }

        await Expect(app.CacheNode(18)).ToBeVisibleAsync();
        await Expect(app.CacheNode(18)).ToContainClassAsync("active");
        await app.Page.UnrouteAsync("**/api/apply");
        await Expect(app.ApplyButton).ToBeDisabledAsync();
        await Expect(app.AddButton).ToBeEnabledAsync();
        await Expect(app.EditButton).ToBeEnabledAsync();
        await Expect(app.DeleteButton).ToBeEnabledAsync();
        await Expect(app.ResetButton).ToBeEnabledAsync();

        await app.Page.RouteAsync("**/api/nodes/9", async route =>
        {
            await Task.Delay(2000);
            await route.ContinueAsync();
        });
        await app.SelectInDb(9, "Node 1.1.2");
        await app.LoadButton.ClickAsync();
        foreach (var button in buttons)
        {
            await Expect(button).ToBeDisabledAsync();
        }

        await Expect(app.CacheNode(9)).ToHaveCountAsync(1);
        await Expect(app.LoadButton).ToBeDisabledAsync();
    }
}
