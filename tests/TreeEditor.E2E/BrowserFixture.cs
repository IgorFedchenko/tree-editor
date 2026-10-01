using Microsoft.Playwright;

namespace TreeEditor.E2E;

public class BrowserFixture : IAsyncLifetime
{
    private IPlaywright _playwright = null!;

    public IBrowser Browser { get; private set; } = null!;

    public string BaseUrl { get; } = Environment.GetEnvironmentVariable("E2E_BASE_URL") ?? "http://localhost:5173";

    public async Task InitializeAsync()
    {
        _playwright = await Playwright.CreateAsync();
        Browser = await _playwright.Chromium.LaunchAsync();
    }

    public async Task DisposeAsync()
    {
        await Browser.DisposeAsync();
        _playwright.Dispose();
    }
}

[CollectionDefinition("browser")]
public class BrowserCollection : ICollectionFixture<BrowserFixture>;
