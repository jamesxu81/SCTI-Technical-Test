using System.Text.RegularExpressions;
using Microsoft.Playwright;
using TechnicalTest.Ui.Helpers;
using static Microsoft.Playwright.Assertions;

namespace TechnicalTest.Ui.Pages;

public abstract class BasePage
{
    // Money as the site shows it, e.g. "$1,234.56" or "-$2.58".
    protected static readonly Regex Amount = new(@"^-?\$[\d,]+\.\d{2}$");

    protected readonly IPage Page;

    protected BasePage(IPage page)
    {
        Page = page;
    }

    // Validation and eligibility messages anywhere on the page.
    public ILocator Message(string text) => Page.GetByText(text, new() { Exact = true });

    protected Task GotoBaseUrlAsync() => Page.GotoAsync(UiSettings.BaseUrl);

    protected Task<IResponse> RunAndWaitForApiResponseAsync(Func<Task> action, string method, string path) =>
        Page.RunAndWaitForResponseAsync(
            action,
            response => response.Request.Method == method
                && new Uri(response.Url).AbsolutePath.Equals(path, StringComparison.OrdinalIgnoreCase));

    protected ILocator RadioGroup(string name) =>
        Page.GetByRole(AriaRole.Radiogroup, new() { Name = name, Exact = true });

    // Radio ids end with the lower-case answer or the bare amount, e.g. excess-100 or CitizenOrLivingInNZ-yes.
    protected ILocator RadioInput(string radioGroupName, string label) =>
        Page.Locator($"[id='{radioGroupName}-{(label is "Yes" or "No" ? label.ToLowerInvariant() : UiFormat.PlainAmount(label))}']");

    // The radio inputs are hidden, so click the visible label and confirm the input took the change.
    protected async Task CheckRadioAsync(string radioGroupName, string label)
    {
        await RadioGroup(radioGroupName).GetByText(label, new() { Exact = true }).ClickAsync();
        await Expect(RadioInput(radioGroupName, label)).ToBeCheckedAsync();
    }
}
