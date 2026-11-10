using FluentAssertions;
using Reqnroll;
using TechnicalTest.Tests.Support;
using TechnicalTest.Ui.Helpers;
using TechnicalTest.Ui.Models;
using TechnicalTest.Ui.Pages;
using static Microsoft.Playwright.Assertions;

namespace TechnicalTest.Tests.StepDefinitions;

[Binding]
public class InsuranceBookingValidationSteps
{
    private readonly QuoteFormPage _quoteForm;
    private readonly PlanOptionsPage _planOptions;
    private readonly TravellerDetailsPage _travellerDetails;
    private readonly NetworkRecorder _network;
    private readonly QuoteState _quote;

    public InsuranceBookingValidationSteps(
        QuoteFormPage quoteForm,
        PlanOptionsPage planOptions,
        TravellerDetailsPage travellerDetails,
        NetworkRecorder network,
        QuoteState quote)
    {
        _quoteForm = quoteForm;
        _planOptions = planOptions;
        _travellerDetails = travellerDetails;
        _network = network;
        _quote = quote;
    }

    [Given(@"I have a quote for ""(.*)"" from ""(.*)"" to ""(.*)"" for ""(.*)""")]
    public async Task GivenIHaveAQuote(
        string destinations, DateOnly departure, DateOnly returnDate, IReadOnlyList<QuoteTraveller> travellers)
    {
        _quote.Departure = departure;
        _quote.Return = returnDate;
        _quote.Travellers = travellers;

        await _quoteForm.SelectJourneyAsync("One return journey");
        await _quoteForm.FillDestinationsAsync(TestData.SplitList(destinations));
        await _quoteForm.FillTravelDatesAsync(departure, returnDate);
        await _quoteForm.FillTravellersAsync(travellers);
        await _quoteForm.ClickGetPlanOptionsAsync();
        await _planOptions.WaitForLoadAsync();
    }

    [Then(@"I should see the quote form error ""(.*)""")]
    public async Task ThenIShouldSeeTheQuoteFormError(string message)
    {
        await Expect(_quoteForm.Message(message)).ToBeVisibleAsync();
    }

    [Then(@"no quote should be requested")]
    public async Task ThenNoQuoteShouldBeRequested()
    {
        await _quoteForm.ExpectOpenAsync();
        _network.ApiCalls("POST", QuoteApi.Create).Should().BeEmpty();
    }

    [When(@"I try to add ""(.*)"" travellers")]
    public async Task WhenITryToAddTravellers(IReadOnlyList<QuoteTraveller> travellers)
    {
        await _quoteForm.EnterTravellersAsync(travellers);
    }

    [Then(@"I should see the traveller error ""(.*)""")]
    public async Task ThenIShouldSeeTheTravellerError(string message)
    {
        await Expect(_quoteForm.TravellersError(message)).ToBeVisibleAsync();
    }

    [Then(@"the travellers selector should stay open")]
    public async Task ThenTheTravellersSelectorShouldStayOpen()
    {
        await Expect(_quoteForm.TravellersDialog).ToBeVisibleAsync();
    }

    [When(@"I change the return date to ""(.*)""")]
    public async Task WhenIChangeTheReturnDateTo(DateOnly returnDate)
    {
        await _quoteForm.ChangeReturnDateAsync(returnDate);
    }

    [Then(@"I should see the travel dates error ""(.*)""")]
    public async Task ThenIShouldSeeTheTravelDatesError(string message)
    {
        await Expect(_quoteForm.TravelDatesError(message)).ToBeVisibleAsync();
    }

    [Then(@"the travel dates selector should stay open")]
    public async Task ThenTheTravelDatesSelectorShouldStayOpen()
    {
        await Expect(_quoteForm.TravelDatesDialog).ToBeVisibleAsync();
    }

    [Then(@"the travel dates should be ""(.*)"" to ""(.*)""")]
    public async Task ThenTheTravelDatesShouldBe(DateOnly departure, DateOnly returnDate)
    {
        await Expect(_quoteForm.TravelDatesInput)
            .ToHaveValueAsync($"{UiFormat.Date(departure)} - {UiFormat.Date(returnDate)}");
    }

    [When(@"I continue without answering the eligibility questions or declarations")]
    public async Task WhenIContinueWithoutAnsweringTheEligibilityQuestionsOrDeclarations()
    {
        await _travellerDetails.TryContinueToHighValueItemsAsync();
    }

    [Then(@"I should see (\d+) ""(.*)"" messages")]
    public async Task ThenIShouldSeeMessages(int count, string message)
    {
        await Expect(_travellerDetails.Message(message)).ToHaveCountAsync(count);
    }

    [Then(@"I should still be on the traveller details step")]
    public async Task ThenIShouldStillBeOnTheTravellerDetailsStep()
    {
        await _travellerDetails.ExpectOpenAsync();
    }

    [When(@"I answer ""(.*)"" to whether all travellers are New Zealand residents")]
    public async Task WhenIAnswerToWhetherAllTravellersAreNewZealandResidents(string answer)
    {
        await _travellerDetails.AnswerResidencyQuestionAsync(answer);
    }

    [Then(@"I should see the eligibility message ""(.*)""")]
    public async Task ThenIShouldSeeTheEligibilityMessage(string message)
    {
        await Expect(_travellerDetails.Message(message)).ToBeVisibleAsync();
    }

    [When(@"I enter date of birth ""(.*)"" for the main policyholder")]
    public async Task WhenIEnterDateOfBirthForTheMainPolicyholder(DateOnly dateOfBirth)
    {
        await _travellerDetails.EnterDateOfBirthAsync(0, dateOfBirth);
    }

    [Then(@"I should see the date of birth error ""(.*)""")]
    public async Task ThenIShouldSeeTheDateOfBirthError(string message)
    {
        await Expect(_travellerDetails.Message(message)).ToBeVisibleAsync();
    }

    [Then(@"I should be offered ""(.*)"" or ""(.*)""")]
    public async Task ThenIShouldBeOffered(string firstOption, string secondOption)
    {
        await Expect(_travellerDetails.DateOfBirthPromptButton(firstOption)).ToBeVisibleAsync();
        await Expect(_travellerDetails.DateOfBirthPromptButton(secondOption)).ToBeVisibleAsync();
    }

    [When(@"I choose ""(.*)"" in the date of birth prompt")]
    public async Task WhenIChooseInTheDateOfBirthPrompt(string option)
    {
        await _travellerDetails.ChooseDateOfBirthPromptOptionAsync(option);
    }

    [Then(@"the total premium should be unchanged")]
    public async Task ThenTheTotalPremiumShouldBeUnchanged()
    {
        await Expect(_travellerDetails.TotalPremium).ToHaveTextAsync(_quote.TotalPremium);
    }

    [When(@"I add another traveller")]
    public async Task WhenIAddAnotherTraveller()
    {
        _quote.AddedTravellerIndex = await _travellerDetails.AddAnotherTravellerAsync();
    }

    [Then(@"I should see a traveller form headed ""(.*)""")]
    public async Task ThenIShouldSeeATravellerFormHeaded(string heading)
    {
        await Expect(_travellerDetails.TravellerHeading(heading)).ToBeVisibleAsync();
    }

    [When(@"I change the new traveller's type to ""(.*)""")]
    public async Task WhenIChangeTheNewTravellersTypeTo(string typeLabel)
    {
        await _travellerDetails.SetTravellerTypeAsync(_quote.AddedTravellerIndex, TravellerTypeFromLabel(typeLabel));
    }

    [Then(@"the ""(.*)"" form should only ask for first name, last name and date of birth")]
    public async Task ThenTheFormShouldOnlyAskForFirstNameLastNameAndDateOfBirth(string heading)
    {
        await Expect(_travellerDetails.TravellerHeading(heading)).ToBeVisibleAsync();
        foreach (var field in new[] { TravellerFieldName.FirstName, TravellerFieldName.LastName, TravellerFieldName.DateOfBirth })
        {
            await Expect(_travellerDetails.TravellerField(_quote.AddedTravellerIndex, field)).ToBeVisibleAsync();
        }

        foreach (var field in new[] { TravellerFieldName.Title, TravellerFieldName.MobilePhone, TravellerFieldName.EmailAddress })
        {
            await Expect(_travellerDetails.TravellerField(_quote.AddedTravellerIndex, field)).ToHaveCountAsync(0);
        }
    }

    [When(@"I remove the new traveller")]
    public async Task WhenIRemoveTheNewTraveller()
    {
        await _travellerDetails.RemoveTravellerAsync(_quote.AddedTravellerIndex);
    }

    [Then(@"I should see (\d+) traveller forms")]
    public async Task ThenIShouldSeeTravellerForms(int count)
    {
        await Expect(_travellerDetails.TravellerForms).ToHaveCountAsync(count);
    }

    private static TravellerType TravellerTypeFromLabel(string label) =>
        label.StartsWith("Adult", StringComparison.OrdinalIgnoreCase) ? TravellerType.Adult
        : label.StartsWith("Dependent", StringComparison.OrdinalIgnoreCase) ? TravellerType.DependentChild
        : label.StartsWith("Non-dependent", StringComparison.OrdinalIgnoreCase) ? TravellerType.NonDependentChild
        : throw new ArgumentException($"Unknown traveller type '{label}'.", nameof(label));
}
