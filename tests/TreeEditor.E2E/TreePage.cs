using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace TreeEditor.E2E;

public class TreePage : IAsyncDisposable
{
    public const string StrikeClass = "text-decoration-line-through";

    private static readonly string? ArtifactsDir = Environment.GetEnvironmentVariable("E2E_ARTIFACTS_DIR");

    private readonly IBrowserContext _context;
    private readonly string _baseUrl;
    private readonly string _testName;
    private readonly ConcurrentQueue<string> _requests = new();
    private readonly ConcurrentQueue<string> _dialogs = new();

    private TreePage(IBrowserContext context, IPage page, string baseUrl, string testName)
    {
        _context = context;
        _baseUrl = baseUrl;
        _testName = testName;
        Page = page;
        page.Dialog += async (_, dialog) =>
        {
            _dialogs.Enqueue(dialog.Message);
            await dialog.DismissAsync();
        };
        page.Request += (_, request) =>
        {
            var path = new Uri(request.Url).AbsolutePath;
            if (path.StartsWith("/api/"))
            {
                _requests.Enqueue($"{request.Method} {path}");
            }
        };
    }

    public IPage Page { get; }

    public ILocator Db => Page.GetByTestId("db-view");
    public ILocator Cache => Page.GetByTestId("cache-view");
    public ILocator LoadButton => Db.GetByRole(AriaRole.Button, new() { Name = "Load to cache" });
    public ILocator AddButton => Cache.GetByRole(AriaRole.Button, new() { Name = "Add child", Exact = true });
    public ILocator EditButton => Cache.GetByRole(AriaRole.Button, new() { Name = "Edit", Exact = true });
    public ILocator DeleteButton => Cache.GetByRole(AriaRole.Button, new() { Name = "Delete", Exact = true });
    public ILocator ApplyButton => Cache.GetByRole(AriaRole.Button, new() { Name = "Apply", Exact = true });
    public ILocator ResetButton => Page.GetByRole(AriaRole.Button, new() { Name = "Reset", Exact = true });
    public ILocator Editor => Cache.GetByRole(AriaRole.Textbox);
    public ILocator SaveButton => Cache.GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true });
    public ILocator CancelButton => Cache.GetByRole(AriaRole.Button, new() { Name = "Cancel", Exact = true });
    public ILocator Alert => Page.GetByRole(AriaRole.Alert);
    public ILocator EmptyCache => Cache.GetByText("Cache is empty");
    public ILocator VisibleDbNodes => Db.Locator("[data-testid^='db-node-']:visible");
    public ILocator VisibleCacheNodes => Cache.Locator("[data-testid^='cache-node-']:visible");
    public ILocator Badges => Cache.GetByText(AnyBadge);

    public static Regex AnyBadge { get; } = new("^(new|edited|deleted)$");

    public IReadOnlyList<string> Requests => _requests.ToArray();

    public static async Task<TreePage> Open(
        BrowserFixture fixture,
        string? changesBeforeOpen = null,
        [CallerMemberName] string testName = "")
    {
        var context = await fixture.Browser.NewContextAsync();
        if (ArtifactsDir != null)
        {
            await context.Tracing.StartAsync(new() { Screenshots = true, Snapshots = true });
        }

        var reset = await context.APIRequest.PostAsync($"{fixture.BaseUrl}/api/reset");
        Assert.Equal(204, reset.Status);

        var app = new TreePage(context, await context.NewPageAsync(), fixture.BaseUrl, testName);
        if (changesBeforeOpen != null)
        {
            var apply = await app.PostApply(changesBeforeOpen);
            Assert.Equal(200, apply.Status);
        }

        await app.Page.GotoAsync(fixture.BaseUrl);
        await Expect(app.DbNode(1)).ToBeVisibleAsync();
        await Expect(app.DbNode(13)).ToBeVisibleAsync();
        return app;
    }

    public async ValueTask DisposeAsync()
    {
        if (ArtifactsDir != null)
        {
            Directory.CreateDirectory(ArtifactsDir);
            await Page.ScreenshotAsync(new() { Path = Path.Combine(ArtifactsDir, $"{_testName}.png") });
            await _context.Tracing.StopAsync(new() { Path = Path.Combine(ArtifactsDir, $"{_testName}.zip") });
        }

        await _context.CloseAsync();
        Assert.Empty(_dialogs);
    }

    public ILocator DbNode(int id) => Page.GetByTestId($"db-node-{id}");

    public ILocator CacheNode(int id) => Page.GetByTestId($"cache-node-{id}");

    public ILocator Row(ILocator node) => node.Locator(":scope > .tree-row");

    public ILocator Text(ILocator node, string text) => Row(node).GetByText(text, new() { Exact = true });

    public ILocator Toggle(int id) => Row(DbNode(id)).GetByRole(AriaRole.Button);

    public ILocator Marker(int id) => Row(CacheNode(id)).GetByTitle("Parent is not loaded");

    public ILocator RowBadges(int id) => Row(CacheNode(id)).GetByText(AnyBadge);

    public Task<string?> ParentOf(ILocator node) =>
        node.EvaluateAsync<string?>(
            "e => e.parentElement.closest('[data-testid*=\"-node-\"]')?.getAttribute('data-testid') ?? null");

    public async Task Expand(int id)
    {
        await Expect(Toggle(id)).ToHaveTextAsync("+");
        await Toggle(id).ClickAsync();
        await Expect(Toggle(id)).ToHaveTextAsync("−");
    }

    public async Task SelectInDb(int id, string text)
    {
        await Text(DbNode(id), text).ClickAsync();
        await Expect(DbNode(id)).ToContainClassAsync("active");
    }

    public async Task Load(int id, string text)
    {
        await SelectInDb(id, text);
        await LoadButton.ClickAsync();
        await Expect(CacheNode(id)).ToBeVisibleAsync();
    }

    public async Task SelectInCache(int id, string text)
    {
        await Text(CacheNode(id), text).ClickAsync();
        await Expect(CacheNode(id)).ToContainClassAsync("active");
    }

    public async Task Rename(int id, string oldText, string newText)
    {
        await SelectInCache(id, oldText);
        await EditButton.ClickAsync();
        await Editor.FillAsync(newText);
        await SaveButton.ClickAsync();
        await Expect(Text(CacheNode(id), newText.Trim())).ToBeVisibleAsync();
    }

    public Task<IResponse> ClickApply() =>
        Page.RunAndWaitForResponseAsync(
            () => ApplyButton.ClickAsync(),
            response => response.Url.EndsWith("/api/apply"));

    public int Mark() => _requests.Count;

    public IReadOnlyList<string> Since(int mark) => _requests.Skip(mark).ToList();

    // gives a request that should not happen a chance to show up in the log
    public Task Pause() => Task.Delay(500);

    private async Task<JsonElement> Node(int id)
    {
        var res = await _context.APIRequest.GetAsync($"{_baseUrl}/api/nodes/{id}");
        Assert.Equal(200, res.Status);
        return (await res.JsonAsync())!.Value;
    }

    public async Task<string?> NodeValue(int id) => (await Node(id)).GetProperty("value").GetString();

    public async Task<bool> IsDeletedInDb(int id) => (await Node(id)).GetProperty("isDeleted").GetBoolean();

    public async Task<int[]> ChildIds(int id)
    {
        var res = await _context.APIRequest.GetAsync($"{_baseUrl}/api/nodes/{id}/children");
        Assert.Equal(200, res.Status);
        return (await res.JsonAsync())!.Value.EnumerateArray().Select(x => x.GetProperty("id").GetInt32()).ToArray();
    }

    public Task<IAPIResponse> PostApply(string json) =>
        _context.APIRequest.PostAsync($"{_baseUrl}/api/apply", new()
        {
            DataString = json,
            Headers = new Dictionary<string, string> { ["Content-Type"] = "application/json" },
        });

    public static void AssertJson(string expected, string actual) =>
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(expected), JsonNode.Parse(actual)), actual);
}
