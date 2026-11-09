using Microsoft.Playwright;

namespace TechnicalTest.Ui.Pages;

public abstract class BasePage
{
    protected readonly IPage Page;

    protected BasePage(IPage page)
    {
        Page = page;
    }

    protected async Task WaitForNetworkIdle()
    {
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    protected async Task<string> GetTextContent(string selector)
    {
        return await Page.Locator(selector).TextContentAsync() ?? "";
    }

    protected async Task<string?> GetAttribute(string selector, string attribute)
    {
        return await Page.Locator(selector).GetAttributeAsync(attribute);
    }
}
