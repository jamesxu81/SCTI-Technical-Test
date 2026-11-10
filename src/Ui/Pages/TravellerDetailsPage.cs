using System.Text.RegularExpressions;
using Microsoft.Playwright;
using TechnicalTest.Ui.Helpers;
using TechnicalTest.Ui.Models;
using static Microsoft.Playwright.Assertions;

namespace TechnicalTest.Ui.Pages;

public class TravellerDetailsPage : BasePage
{
    internal static readonly Regex Url = new("step=traveller-details");

    private const string ResidencyQuestion = "CitizenOrLivingInNZ";
    private const string JourneyStartedQuestion = "JourneyStarted NZ";
    private const string FraudQuestion = "Fraud NZ";

    public TravellerDetailsPage(IPage page) : base(page)
    {
    }

    public ILocator TravellerForms => Page.Locator("[id$='].firstName']");

    public ILocator TotalPremium =>
        Page.GetByText("Total premium", new() { Exact = true }).Locator("..").GetByText(Amount);

    private ILocator PolicyholderHeading => Page.GetByText(new Regex(@"^Main policyholder - Adult 1, aged \d+$"));

    private ILocator ConfirmDeclaration =>
        Page.GetByRole(AriaRole.Checkbox, new() { Name = "I confirm that I have read, understood, and agree" });

    private ILocator AuthoriseDeclaration =>
        Page.GetByRole(AriaRole.Checkbox, new() { Name = "I authorise SCTI to collect, use, and share personal information" });

    private ILocator AddTravellerButton => Page.GetByRole(AriaRole.Button, new() { Name = "Add traveller" });

    private ILocator NextToHighValueItemsButton =>
        Page.GetByRole(AriaRole.Button, new() { Name = "Next: High-value items" });

    public IReadOnlyList<ILocator> EligibilityQuestions =>
        new[] { ResidencyQuestion, JourneyStartedQuestion, FraudQuestion }.Select(RadioGroup).ToList();

    public ILocator TravellerHeading(string heading) => Page.GetByText(heading, new() { Exact = true });

    public ILocator TravellerField(int travellerIndex, TravellerFieldName field) => Field(travellerIndex, field switch
    {
        TravellerFieldName.Title => "title",
        TravellerFieldName.FirstName => "firstName",
        TravellerFieldName.LastName => "lastName",
        TravellerFieldName.DateOfBirth => "dob",
        TravellerFieldName.MobilePhone => "mobilePhone",
        TravellerFieldName.EmailAddress => "emailAddress",
        TravellerFieldName.StreetAddress => "streetAddress",
        TravellerFieldName.Suburb => "suburb",
        TravellerFieldName.City => "city",
        TravellerFieldName.Postcode => "postcode",
        _ => throw new ArgumentOutOfRangeException(nameof(field), field, null),
    });

    public ILocator DateOfBirthPromptButton(string name) =>
        Page.GetByRole(AriaRole.Button, new() { Name = name, Exact = true });

    public Task ExpectOpenAsync() => Expect(Page).ToHaveURLAsync(Url);

    public async Task CompleteTravellerEligibilityAsync()
    {
        await CheckRadioAsync(ResidencyQuestion, "Yes");
        await CheckRadioAsync(JourneyStartedQuestion, "No");
        await CheckRadioAsync(FraudQuestion, "No");
        await ConfirmDeclaration.CheckAsync();
        await AuthoriseDeclaration.CheckAsync();
        await PolicyholderHeading.WaitForAsync();
    }

    public Task AnswerResidencyQuestionAsync(string answer) => CheckRadioAsync(ResidencyQuestion, answer);

    public async Task TryContinueToHighValueItemsAsync()
    {
        // A complete traveller form would be sent to the live site, so only continue before it appears.
        if (await PolicyholderHeading.IsVisibleAsync())
        {
            throw new InvalidOperationException("Refusing to continue: the traveller form is showing and may be complete.");
        }

        await NextToHighValueItemsButton.ClickAsync();
    }

    public async Task<int> AddTravellerAsync(TravellerType type, string firstName, string lastName, DateOnly dateOfBirth)
    {
        var travellerIndex = await FindEmptyTravellerFormAsync(type);
        await TravellerField(travellerIndex, TravellerFieldName.FirstName).FillAsync(firstName);
        await TravellerField(travellerIndex, TravellerFieldName.LastName).FillAsync(lastName);
        await EnterDateOfBirthAsync(travellerIndex, dateOfBirth);
        return travellerIndex;
    }

    public async Task EnterDateOfBirthAsync(int travellerIndex, DateOnly dateOfBirth)
    {
        var field = TravellerField(travellerIndex, TravellerFieldName.DateOfBirth);
        await field.FillAsync(UiFormat.Date(dateOfBirth));
        await field.PressAsync("Tab");
    }

    public async Task FillPolicyholderContactAsync(string email, string mobile, string address)
    {
        await TravellerField(0, TravellerFieldName.MobilePhone).FillAsync(mobile);
        await TravellerField(0, TravellerFieldName.EmailAddress).FillAsync(email);

        // Google Places loads after the travellers section appears; typing before then gets no suggestions.
        await Page.WaitForFunctionAsync("() => !!window.google?.maps?.places");
        await TravellerField(0, TravellerFieldName.StreetAddress).PressSequentiallyAsync(address.Split(',')[0]);
        await Page.GetByRole(AriaRole.Button, new() { Name = address, Exact = true }).ClickAsync();
        await Expect(TravellerField(0, TravellerFieldName.Postcode)).Not.ToBeEmptyAsync();
    }

    public Task ChooseDateOfBirthPromptOptionAsync(string option) => DateOfBirthPromptButton(option).ClickAsync();

    public async Task<int> AddAnotherTravellerAsync()
    {
        var travellerIndex = await TravellerForms.CountAsync();
        await AddTravellerButton.ClickAsync();
        await TravellerField(travellerIndex, TravellerFieldName.FirstName).WaitForAsync();
        return travellerIndex;
    }

    public async Task SetTravellerTypeAsync(int travellerIndex, TravellerType type)
    {
        var radio = TypeRadio(travellerIndex, type);
        await Page.Locator("label").Filter(new() { Has = radio }).ClickAsync();
        await Expect(radio).ToBeCheckedAsync();
    }

    public async Task RemoveTravellerAsync(int travellerIndex)
    {
        // The main policyholder (index 0) has no Remove link, so link n belongs to traveller n + 1.
        await Page.GetByText("Remove", new() { Exact = true }).Nth(travellerIndex - 1).ClickAsync();
        await TravellerField(travellerIndex, TravellerFieldName.FirstName)
            .WaitForAsync(new() { State = WaitForSelectorState.Detached });
    }

    private async Task<int> FindEmptyTravellerFormAsync(TravellerType type)
    {
        var count = await TravellerForms.CountAsync();
        for (var travellerIndex = 0; travellerIndex < count; travellerIndex++)
        {
            // The main policyholder form (index 0) is always an adult and has no traveller type choice.
            var isRequestedType = travellerIndex == 0
                ? type == TravellerType.Adult
                : await TypeRadio(travellerIndex, type).IsCheckedAsync();
            if (isRequestedType && await TravellerField(travellerIndex, TravellerFieldName.FirstName).InputValueAsync() == string.Empty)
            {
                return travellerIndex;
            }
        }

        throw new InvalidOperationException($"No empty {type} traveller form is showing.");
    }

    // Field ids follow the form model, e.g. travellers[0].firstName; labels repeat for every traveller.
    private ILocator Field(int travellerIndex, string id) => Page.Locator($"[id='travellers[{travellerIndex}].{id}']");

    private ILocator TypeRadio(int travellerIndex, TravellerType type) => Field(travellerIndex, $"type.{type}");
}
