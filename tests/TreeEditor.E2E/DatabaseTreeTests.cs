using static Microsoft.Playwright.Assertions;

namespace TreeEditor.E2E;

[Collection("browser")]
public class DatabaseTreeTests(BrowserFixture fixture)
{
    [Fact]
    public async Task Page_shows_two_roots_and_an_empty_cache()
    {
        await using var app = await TreePage.Open(fixture);
        await app.Pause();

        await Expect(app.VisibleDbNodes).ToHaveCountAsync(2);
        await Expect(app.Text(app.DbNode(1), "Node 1")).ToBeVisibleAsync();
        await Expect(app.Text(app.DbNode(13), "Node 2")).ToBeVisibleAsync();
        Assert.Null(await app.ParentOf(app.DbNode(1)));
        Assert.Null(await app.ParentOf(app.DbNode(13)));

        // StrictMode may read the roots twice, nothing else is requested
        Assert.InRange(app.Requests.Count, 1, 2);
        Assert.All(app.Requests, x => Assert.Equal("GET /api/nodes/roots", x));

        await Expect(app.EmptyCache).ToBeVisibleAsync();
        await Expect(app.Cache.Locator("[data-testid^='cache-node-']")).ToHaveCountAsync(0);

        await Expect(app.LoadButton).ToBeDisabledAsync();
        await Expect(app.ApplyButton).ToBeDisabledAsync();
    }

    [Fact]
    public async Task Children_are_requested_every_time_a_node_is_expanded()
    {
        await using var app = await TreePage.Open(fixture);

        var mark = app.Mark();
        await app.Expand(1);
        await Expect(app.Text(app.DbNode(2), "Node 1.1")).ToBeVisibleAsync();
        await Expect(app.Text(app.DbNode(10), "Node 1.2")).ToBeVisibleAsync();
        Assert.Equal("db-node-1", await app.ParentOf(app.DbNode(2)));
        Assert.Equal("db-node-1", await app.ParentOf(app.DbNode(10)));
        await Expect(app.DbNode(1).Locator("[data-testid^='db-node-']:visible")).ToHaveCountAsync(2);
        await app.Pause();
        Assert.Equal(["GET /api/nodes/1/children"], app.Since(mark));

        mark = app.Mark();
        await app.Toggle(1).ClickAsync();
        await Expect(app.DbNode(2)).ToBeHiddenAsync();
        await Expect(app.DbNode(10)).ToBeHiddenAsync();
        await Expect(app.VisibleDbNodes).ToHaveCountAsync(2);
        await app.Pause();
        Assert.Empty(app.Since(mark));

        mark = app.Mark();
        await app.Expand(1);
        await Expect(app.DbNode(2)).ToBeVisibleAsync();
        await app.Pause();
        Assert.Equal(["GET /api/nodes/1/children"], app.Since(mark));

        await app.Expand(2);
        await Expect(app.DbNode(2).GetByTestId("db-node-3")).ToBeVisibleAsync();
        await Expect(app.DbNode(2).GetByTestId("db-node-9")).ToBeVisibleAsync();
        await Expect(app.Text(app.DbNode(3), "Node 1.1.1")).ToBeVisibleAsync();
        await Expect(app.Text(app.DbNode(9), "Node 1.1.2")).ToBeVisibleAsync();
        Assert.Contains("GET /api/nodes/2/children", app.Since(mark));

        await Expect(app.Toggle(9)).ToHaveCountAsync(0);

        await app.Expand(10);
        await Expect(app.DbNode(11)).ToBeVisibleAsync();
        await Expect(app.DbNode(12)).ToBeVisibleAsync();
        await Expect(app.Toggle(11)).ToHaveCountAsync(0);
        await Expect(app.Toggle(12)).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task Selected_node_is_loaded_into_the_cache_once()
    {
        await using var app = await TreePage.Open(fixture);
        await Expect(app.LoadButton).ToBeDisabledAsync();
        await Expect(app.Db.Locator("[data-testid^='db-node-'].active")).ToHaveCountAsync(0);

        await app.SelectInDb(1, "Node 1");
        await Expect(app.DbNode(13)).Not.ToContainClassAsync("active");
        await Expect(app.LoadButton).ToBeEnabledAsync();

        var mark = app.Mark();
        await app.LoadButton.ClickAsync();
        await Expect(app.CacheNode(1)).ToBeVisibleAsync();
        await Expect(app.Text(app.CacheNode(1), "Node 1")).ToBeVisibleAsync();
        await Expect(app.VisibleCacheNodes).ToHaveCountAsync(1);
        await Expect(app.EmptyCache).ToBeHiddenAsync();
        await app.Pause();
        Assert.Equal(["GET /api/nodes/1"], app.Since(mark));

        await Expect(app.LoadButton).ToBeDisabledAsync();
        await Expect(app.CacheNode(1)).ToHaveCountAsync(1);

        await app.SelectInDb(13, "Node 2");
        await Expect(app.LoadButton).ToBeEnabledAsync();
        await app.SelectInDb(1, "Node 1");
        await Expect(app.LoadButton).ToBeDisabledAsync();
        await Expect(app.CacheNode(1)).ToHaveCountAsync(1);
    }
}
