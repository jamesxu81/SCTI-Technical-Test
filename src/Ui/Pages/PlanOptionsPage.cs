using System.Text.RegularExpressions;
using Microsoft.Playwright;
using TechnicalTest.Ui.Helpers;
using static Microsoft.Playwright.Assertions;

namespace TechnicalTest.Ui.Pages;

public class PlanOptionsPage : BasePage
{
    public const string OneReturnJourneyPlan = "International Comprehensive Single Trip";

    public const string MedicalOnlyPlan = "International Medical Only";

    internal static readonly Regex Url = new("step=plan-options");

    public PlanOptionsPage(IPage page) : base(page)
    {
    }

    // The id is the product code the API uses: ST is the single trip (one return journey) plan.
    public ILocator SelectPlanButton => Page.Locator("button#ST");

    // After customising, a sticky summary repeats the plan name without a price, so only a name next to a price matches.
    private ILocator PlanPrice => Page.GetByText(OneReturnJourneyPlan, new() { Exact = true })
        .Locator("..").GetByText(Amount).Locator("visible=true");

    private ILocator CustomiseSection => Page.GetByText("Changes below only apply to the selected plan");

    private ILocator DiscountAmount =>
        Page.GetByText("Discount", new() { Exact = true }).Locator("..").GetByText(Amount);

    // One row each for excess, cancellation cover and extra cover, in that order.
    private ILocator PremiumChanges =>
        Page.GetByText("Premium change", new() { Exact = true }).Locator("..").GetByText(Amount);

    private ILocator NextToTravellerDetailsButton =>
        Page.GetByRole(AriaRole.Button, new() { Name = "Next: Traveller details" });

    // Plan names are repeated in hidden markup, so only the visible card counts.
    public ILocator PlanName(string name) =>
        Page.GetByText(name, new() { Exact = true }).Locator("visible=true").First;

    public async Task WaitForLoadAsync()
    {
        await Page.WaitForURLAsync(Url);
        await SelectPlanButton.WaitForAsync();
    }

    public Task ExpectOpenAsync() => Expect(Page).ToHaveURLAsync(Url);

    public async Task SelectPlanAsync()
    {
        await SelectPlanButton.ClickAsync();
        await CustomiseSection.WaitForAsync();
    }

    public async Task CustomisePlanAsync(
        string discount, string excess, string cancellationCover, IEnumerable<string> extraCovers)
    {
        await ChooseQuoteOptionAsync("schsDiscount", discount, DiscountAmount, changesPremium: discount == "Yes");
        await ChooseQuoteOptionAsync("excess", excess, PremiumChanges.Nth(0), changesPremium: true);
        await ChooseQuoteOptionAsync("cancellationCover", cancellationCover, PremiumChanges.Nth(1), changesPremium: true);
        foreach (var cover in extraCovers)
        {
            var before = await PremiumChanges.Nth(2).InnerTextAsync();
            await Page.GetByRole(AriaRole.Checkbox, new() { Name = cover, Exact = true }).CheckAsync();
            await WaitForAmountToChangeAsync(PremiumChanges.Nth(2), before);
        }
    }

    public async Task<decimal> GetPlanPriceAsync() => UiFormat.ParseMoney(await PlanPrice.InnerTextAsync());

    // No discount amount is shown unless the discount applies.
    public async Task<decimal> GetDiscountAsync() =>
        await DiscountAmount.CountAsync() == 0 ? 0m : UiFormat.ParseMoney(await DiscountAmount.InnerTextAsync());

    public async Task<IReadOnlyList<decimal>> GetPremiumChangesAsync() =>
        (await PremiumChanges.AllInnerTextsAsync()).Select(UiFormat.ParseMoney).ToList();

    public async Task ClickNextToTravellerDetailsAsync()
    {
        await RunAndWaitForApiResponseAsync(() => NextToTravellerDetailsButton.ClickAsync(), "PUT", QuoteApi.Select);
        await Page.WaitForURLAsync(TravellerDetailsPage.Url);
    }

    private async Task ChooseQuoteOptionAsync(string radioGroupName, string label, ILocator amount, bool changesPremium)
    {
        if (await RadioInput(radioGroupName, label).IsCheckedAsync())
        {
            return;
        }

        var before = await amount.CountAsync() == 0 ? null : await amount.InnerTextAsync();
        await CheckRadioAsync(radioGroupName, label);
        if (changesPremium)
        {
            await WaitForAmountToChangeAsync(amount, before);
        }
    }

    // Each change is re-priced by the server; waiting for the new amount keeps the updates in order.
    private static Task WaitForAmountToChangeAsync(ILocator amount, string? before) =>
        before is null
            ? Expect(amount).ToBeVisibleAsync()
            : Expect(amount).Not.ToHaveTextAsync(before);
}
