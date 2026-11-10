using System.Globalization;
using Microsoft.Playwright;
using TechnicalTest.Ui.Helpers;
using TechnicalTest.Ui.Models;
using static Microsoft.Playwright.Assertions;

namespace TechnicalTest.Ui.Pages;

public class QuoteFormPage : BasePage
{
    public QuoteFormPage(IPage page) : base(page)
    {
    }

    public ILocator DestinationInput => Page.GetByRole(AriaRole.Textbox, new() { Name = "Enter destination(s)" });

    public ILocator TravelDatesInput => Page.GetByRole(AriaRole.Textbox, new() { Name = "Select travel dates" });

    public ILocator TravellersInput => Page.GetByRole(AriaRole.Textbox, new() { Name = "Add travellers" });

    public ILocator TravellersDialog => Page.GetByRole(AriaRole.Dialog, new() { Name = "Add number of travellers" });

    public ILocator TravelDatesDialog => Page.GetByRole(AriaRole.Dialog, new() { Name = "Date picker" });

    private ILocator GetAQuoteButton => Page.GetByRole(AriaRole.Button, new() { Name = "Get a quote" });

    private ILocator DestinationDialog => Page.GetByRole(AriaRole.Dialog, new() { Name = "Select destinations" });

    public ILocator JourneyTab(string journeyType) => Page.GetByRole(AriaRole.Tab, new() { Name = journeyType });

    public ILocator TravellersError(string text) => TravellersDialog.GetByText(text, new() { Exact = true });

    public ILocator TravelDatesError(string text) => TravelDatesDialog.GetByText(text, new() { Exact = true });

    public async Task NavigateAsync()
    {
        await GotoBaseUrlAsync();
        await GetAQuoteButton.WaitForAsync();
    }

    public Task ExpectOpenAsync() => Expect(Page).ToHaveURLAsync(UiSettings.BaseUrl);

    public Task SelectJourneyAsync(string journeyType) => JourneyTab(journeyType).ClickAsync();

    public async Task FillDestinationsAsync(IEnumerable<string> destinations)
    {
        await OpenFieldAsync(DestinationInput);
        var search = DestinationDialog.GetByRole(AriaRole.Textbox, new() { Name = "Destination(s) *" });
        foreach (var destination in destinations)
        {
            await search.ClearAsync();
            await search.PressSequentiallyAsync(destination);
            await DestinationDialog.GetByRole(AriaRole.Button, new() { Name = destination, Exact = true }).ClickAsync();
        }

        await CloseDialogAsync(DestinationDialog);
    }

    public async Task FillTravelDatesAsync(DateOnly departure, DateOnly returnDate)
    {
        await OpenFieldAsync(TravelDatesInput);
        await DateField("Departure date *").FillAsync(UiFormat.Date(departure));
        await DateField("Return date *").FillAsync(UiFormat.Date(returnDate));
        await CloseDialogAsync(TravelDatesDialog);
    }

    // Clicks Done without waiting for the dialog to close, because an invalid date keeps it open.
    public async Task ChangeReturnDateAsync(DateOnly returnDate)
    {
        await OpenFieldAsync(TravelDatesInput);
        var field = DateField("Return date *");
        await field.ClearAsync();
        // Typed like a user (the mask adds the slashes); in Firefox a pasted invalid date is silently reverted.
        await field.PressSequentiallyAsync(UiFormat.Date(returnDate).Replace("/", string.Empty));
        // Leave the field first so every browser validates the new date before Done is handled.
        await field.PressAsync("Tab");
        await DoneButton(TravelDatesDialog).ClickAsync();
    }

    public async Task FillTravellersAsync(IReadOnlyList<QuoteTraveller> travellers)
    {
        await EnterTravellersAsync(travellers);
        await TravellersDialog.WaitForAsync(new() { State = WaitForSelectorState.Hidden });
    }

    public async Task EnterTravellersAsync(IReadOnlyList<QuoteTraveller> travellers)
    {
        await OpenFieldAsync(TravellersInput);
        foreach (var group in travellers.GroupBy(traveller => traveller.Type))
        {
            // All three Increment buttons share one name, so the row index is the only handle.
            var increment = TravellersDialog.GetByRole(AriaRole.Button, new() { Name = "Increment" }).Nth((int)group.Key);
            var ordinal = 0;
            foreach (var traveller in group)
            {
                await increment.ClickAsync();
                await TravellersDialog.Locator($"#{group.Key}-age-{ordinal++}")
                    .FillAsync(traveller.Age.ToString(CultureInfo.InvariantCulture));
            }
        }

        await DoneButton(TravellersDialog).ClickAsync();
    }

    public Task ClickGetPlanOptionsAsync() => GetAQuoteButton.ClickAsync();

    private ILocator DateField(string name) => TravelDatesDialog.GetByRole(AriaRole.Textbox, new() { Name = name });

    // In WebKit the field icon covers all but the input's left edge, so click there rather than the centre.
    private static Task OpenFieldAsync(ILocator field) =>
        field.ClickAsync(new() { Position = new() { X = 5, Y = 10 } });

    private static ILocator DoneButton(ILocator dialog) =>
        dialog.GetByRole(AriaRole.Button, new() { Name = "Done", Exact = true });

    private static async Task CloseDialogAsync(ILocator dialog)
    {
        await DoneButton(dialog).ClickAsync();
        await dialog.WaitForAsync(new() { State = WaitForSelectorState.Hidden });
    }
}
