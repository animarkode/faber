using Microsoft.Playwright;

namespace Faber.Modules.Documents.Application.Services;

public class PlaywrightService : IAsyncDisposable
{
    public IBrowser? Browser { get; private set; }

    public async ValueTask DisposeAsync()
    {
        if (Browser is not null)
        {
            await Browser.CloseAsync();
            await Browser.DisposeAsync();
        }

        GC.SuppressFinalize(this);
    }

    public async Task InitBrowserAsync()
    {
        var launchOptions = new BrowserTypeLaunchOptions
        {
            Headless = true,
            Args = ["--no-sandbox", "--disable-setuid-sandbox"]
        };

        var playwright = await Playwright.CreateAsync();
        Browser = await playwright.Chromium.LaunchAsync(launchOptions);
    }
}