using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using Reqnroll;
using TechnicalTest.Tests.Support;
using TechnicalTest.Ui.Helpers;
using TechnicalTest.Ui.Models;
using TechnicalTest.Ui.Pages;
using static Microsoft.Playwright.Assertions;

namespace TechnicalTest.Tests.StepDefinitions;

[Binding]
public class InsuranceBookingSteps
{
    private readonly QuoteFormPage _quoteForm;
    private readonly PlanOptionsPage _planOptions;
    private readonly TravellerDetailsPage _travellerDetails;
    private readonly NetworkRecorder _network;
    private readonly QuoteState _quote;

    public InsuranceBookingSteps(
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

    [Given(@"I navigate to the insurance portal")]
    public async Task GivenINavigateToTheInsurancePortal()
    {
        await _quoteForm.NavigateAsync();
    }

    [When(@"I select ""(.*)"" journey")]
    public async Task WhenISelectJourney(string journeyType)
    {
        await _quoteForm.SelectJourneyAsync(journeyType);
        await Expect(_quoteForm.JourneyTab(journeyType)).ToHaveAttributeAsync("aria-selected", "true");
    }

    [When(@"I add destination ""(.*)""")]
    public async Task WhenIAddDestination(string destinations)
    {
        var names = TestData.SplitList(destinations);
        await _quoteForm.FillDestinationsAsync(names);
        await Expect(_quoteForm.DestinationInput).ToHaveValueAsync(string.Join(", ", names));
    }

    [When(@"I add travel dates ""(.*)"" and ""(.*)""")]
    public async Task WhenIAddTravelDates(DateOnly departure, DateOnly returnDate)
    {
        _quote.Departure = departure;
        _quote.Return = returnDate;
        await _quoteForm.FillTravelDatesAsync(departure, returnDate);
        await Expect(_quoteForm.TravelDatesInput)
            .ToHaveValueAsync($"{UiFormat.Date(departure)} - {UiFormat.Date(returnDate)}");
    }

    [When(@"I add ""(.*)"" travellers")]
    public async Task WhenIAddTravellers(IReadOnlyList<QuoteTraveller> travellers)
    {
        _quote.Travellers = travellers;
        await _quoteForm.FillTravellersAsync(travellers);
        await Expect(_quoteForm.TravellersInput).ToHaveValueAsync(new Regex($"^{travellers.Count} Travellers?$"));
    }

    [When(@"I click Get plan options")]
    public async Task WhenIClickGetPlanOptions()
    {
        await _quoteForm.ClickGetPlanOptionsAsync();
    }

    [Then(@"I should see plan options available")]
    public async Task ThenIShouldSeePlanOptionsAvailable()
    {
        await _planOptions.WaitForLoadAsync();
        await _planOptions.ExpectOpenAsync();
        await Expect(_planOptions.PlanName(PlanOptionsPage.OneReturnJourneyPlan)).ToBeVisibleAsync();
        await Expect(_planOptions.PlanName(PlanOptionsPage.MedicalOnlyPlan)).ToBeVisibleAsync();
        await Expect(_planOptions.SelectPlanButton).ToBeVisibleAsync();

        _quote.BasePrice = await _planOptions.GetPlanPriceAsync();
        _quote.BasePrice.Should().BePositive("the one return journey plan should show a price");
    }

    [Then(@"the quote request should contain destinations ""(.*)"" and travellers ""(.*)""")]
    public void ThenTheQuoteRequestShouldContain(string destinationCodes, IReadOnlyList<QuoteTraveller> travellers)
    {
        var request = _network.ApiCalls("POST", QuoteApi.Create).Should().ContainSingle().Subject;
        request.Status.Should().Be(200);

        using var body = JsonDocument.Parse(request.RequestBody!);
        var quote = body.RootElement;
        quote.GetProperty("ProductCode").GetString().Should().Be("ST");
        quote.GetProperty("BaseCountry").GetString().Should().Be("NZL");
        Strings(quote, "Destinations").Should().Equal(TestData.SplitList(destinationCodes));
        DatePart(quote, "StartDateOfJourney").Should().Be(_quote.Departure);
        DatePart(quote, "EndDate").Should().Be(_quote.Return);
        DatePart(quote, "StartDateOfInsurance").Should().Be(TestData.TodayInNewZealand);
        Ages(quote, "AdultsAge").Should().Equal(AgesOf(travellers, TravellerType.Adult));
        Ages(quote, "DependentChildAges").Should().Equal(AgesOf(travellers, TravellerType.DependentChild));
        Ages(quote, "NonDependentChildAges").Should().Equal(AgesOf(travellers, TravellerType.NonDependentChild));
    }

    [When(@"I select a plan and customise it")]
    public async Task WhenISelectAPlanAndCustomiseIt()
    {
        await _planOptions.SelectPlanAsync();
    }

    [When(@"I choose discount ""(.*)"", excess ""(.*)"", cancellation cover ""(.*)"", and extra cover ""(.*)""")]
    public async Task WhenIChooseDiscountExcessCancellationCoverAndExtraCover(
        string discount, string excess, string cancellationCover, string extraCover)
    {
        await _planOptions.CustomisePlanAsync(discount, excess, cancellationCover, TestData.SplitList(extraCover));
    }

    [Then(@"the plan price should equal the base price plus the premium changes")]
    public async Task ThenThePlanPriceShouldEqualTheBasePricePlusThePremiumChanges()
    {
        var price = await _planOptions.GetPlanPriceAsync();
        var discount = await _planOptions.GetDiscountAsync();
        var premiumChanges = await _planOptions.GetPremiumChangesAsync();

        price.Should().Be(
            _quote.BasePrice + discount + premiumChanges.Sum(),
            "base {0}, discount {1} and premium changes {2} should add up to the plan price",
            _quote.BasePrice, discount, string.Join(", ", premiumChanges));
        _quote.CustomisedPrice = price;
    }

    [Then(@"the latest plan update request should contain discount ""(.*)"", excess ""(.*)"", cancellation cover ""(.*)"" and extra cover ""(.*)""")]
    public void ThenTheLatestPlanUpdateRequestShouldContain(
        string discount, string excess, string cancellationCover, string extraCover)
    {
        var updates = _network.ApiCalls("PUT", QuoteApi.Extras);
        updates.Should().NotBeEmpty().And.OnlyContain(call => call.Status == 200);

        using var body = JsonDocument.Parse(updates[^1].RequestBody!);
        var extras = body.RootElement;
        var covers = TestData.SplitList(extraCover);
        extras.GetProperty("ProductCode").GetString().Should().Be("ST");
        extras.GetProperty("SchsDiscount").GetBoolean().Should().Be(discount == "Yes");
        extras.GetProperty("Excess").GetString().Should().Be(UiFormat.PlainAmount(excess));
        extras.GetProperty("CancellationCover").GetString().Should().Be(UiFormat.PlainAmount(cancellationCover));
        extras.GetProperty("MopedCover").GetBoolean().Should().Be(covers.Contains("Moped or motorbike"));
        extras.GetProperty("SkiCover").GetBoolean().Should().Be(covers.Contains("Skiing or snowboarding"));
        extras.GetProperty("CruiseCover").GetBoolean().Should().Be(covers.Contains("Cruise"));
    }

    [When(@"I click Next to Traveller details")]
    public async Task WhenIClickNextToTravellerDetails()
    {
        await _planOptions.ClickNextToTravellerDetailsAsync();
        _quote.TotalPremium = await _travellerDetails.TotalPremium.InnerTextAsync();
    }

    [Then(@"I should see traveller eligibility section")]
    public async Task ThenIShouldSeeTravellerEligibilitySection()
    {
        await _travellerDetails.ExpectOpenAsync();
        foreach (var question in _travellerDetails.EligibilityQuestions)
        {
            await Expect(question).ToBeVisibleAsync();
        }
    }

    [When(@"I complete traveller eligibility and privacy declaration")]
    public async Task WhenICompleteTravellerEligibilityAndPrivacyDeclaration()
    {
        await _travellerDetails.CompleteTravellerEligibilityAsync();
    }

    [When(@"I add adult traveller with firstname ""(.*)"", lastname ""(.*)"", and DOB ""(.*)""")]
    public Task WhenIAddAdultTravellerWithFirstnameLastnameAndDOB(string firstName, string lastName, DateOnly dob) =>
        AddTravellerAsync(TravellerType.Adult, firstName, lastName, dob);

    [When(@"I add dependent traveller with firstname ""(.*)"", lastname ""(.*)"", and DOB ""(.*)""")]
    public Task WhenIAddDependentTravellerWithFirstnameLastnameAndDOB(string firstName, string lastName, DateOnly dob) =>
        AddTravellerAsync(TravellerType.DependentChild, firstName, lastName, dob);

    [When(@"I add non-dependent traveller with firstname ""(.*)"", lastname ""(.*)"", and DOB ""(.*)""")]
    public Task WhenIAddNonDependentTravellerWithFirstnameLastnameAndDOB(string firstName, string lastName, DateOnly dob) =>
        AddTravellerAsync(TravellerType.NonDependentChild, firstName, lastName, dob);

    [When(@"I enter contact details for the main policyholder with email ""(.*)"", mobile ""(.*)"" and address ""(.*)""")]
    public async Task WhenIEnterContactDetailsForTheMainPolicyholder(string email, string mobile, string address)
    {
        _quote.PolicyholderEmail = email;
        _quote.PolicyholderMobile = mobile;
        await _travellerDetails.FillPolicyholderContactAsync(email, mobile, address);
    }

    [Then(@"I should see all travellers added successfully")]
    public async Task ThenIShouldSeeAllTravellersAddedSuccessfully()
    {
        await _travellerDetails.ExpectOpenAsync();
        await Expect(_travellerDetails.TravellerForms).ToHaveCountAsync(_quote.Travellers.Count);
        foreach (var heading in ExpectedTravellerHeadings(_quote.Travellers))
        {
            await Expect(_travellerDetails.TravellerHeading(heading)).ToBeVisibleAsync();
        }

        foreach (var traveller in _quote.FilledTravellers)
        {
            await Expect(_travellerDetails.TravellerField(traveller.Index, TravellerFieldName.FirstName))
                .ToHaveValueAsync(traveller.FirstName);
            await Expect(_travellerDetails.TravellerField(traveller.Index, TravellerFieldName.LastName))
                .ToHaveValueAsync(traveller.LastName);
            await Expect(_travellerDetails.TravellerField(traveller.Index, TravellerFieldName.DateOfBirth))
                .ToHaveValueAsync(UiFormat.Date(traveller.DateOfBirth));
        }

        await Expect(_travellerDetails.TravellerField(0, TravellerFieldName.EmailAddress)).ToHaveValueAsync(_quote.PolicyholderEmail);
        await Expect(_travellerDetails.TravellerField(0, TravellerFieldName.MobilePhone)).ToHaveValueAsync(_quote.PolicyholderMobile);
        await Expect(_travellerDetails.TravellerField(0, TravellerFieldName.City)).Not.ToBeEmptyAsync();
        await Expect(_travellerDetails.TravellerField(0, TravellerFieldName.Postcode)).Not.ToBeEmptyAsync();
        await Expect(_travellerDetails.Message("This field is required")).ToHaveCountAsync(0);
        await Expect(_travellerDetails.Message("Entered age and date of birth do not match.")).ToHaveCountAsync(0);
        await Expect(_travellerDetails.TotalPremium).ToHaveTextAsync(UiFormat.Money(_quote.CustomisedPrice));

        _network.ApiCalls("PUT", QuoteApi.Select).Should().ContainSingle().Which.Status.Should().Be(200);
        _network.FirstPartyApiCallsAfter("PUT", QuoteApi.Select).Should().BeEmpty(
            "traveller details should stay in the browser until Next: High-value items is clicked");
    }

    private async Task AddTravellerAsync(TravellerType type, string firstName, string lastName, DateOnly dateOfBirth)
    {
        var travellerIndex = await _travellerDetails.AddTravellerAsync(type, firstName, lastName, dateOfBirth);
        _quote.FilledTravellers.Add(new FilledTraveller(travellerIndex, firstName, lastName, dateOfBirth));
    }

    // e.g. "Main policyholder - Adult 1, aged 40", "Dependent child 1, aged 10", "Non-dependent child 1, aged 12".
    private static IEnumerable<string> ExpectedTravellerHeadings(IEnumerable<QuoteTraveller> travellers) =>
        travellers.GroupBy(traveller => traveller.Type).SelectMany(group => group.Select((traveller, i) =>
            (group.Key, Number: i + 1) switch
            {
                (TravellerType.Adult, 1) => $"Main policyholder - Adult 1, aged {traveller.Age}",
                (TravellerType.Adult, var number) => $"Adult {number}, aged {traveller.Age}",
                (TravellerType.DependentChild, var number) => $"Dependent child {number}, aged {traveller.Age}",
                (_, var number) => $"Non-dependent child {number}, aged {traveller.Age}",
            }));

    private static IEnumerable<string> Strings(JsonElement json, string property) =>
        json.GetProperty(property).EnumerateArray().Select(item => item.GetString()!);

    private static IEnumerable<int> Ages(JsonElement json, string property) =>
        json.GetProperty(property).EnumerateArray().Select(item => item.GetInt32());

    // The API sends dates as e.g. "2026-12-09T00:00:00.000Z".
    private static DateOnly DatePart(JsonElement json, string property) =>
        DateOnly.ParseExact(json.GetProperty(property).GetString()![..10], "yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static IEnumerable<int> AgesOf(IEnumerable<QuoteTraveller> travellers, TravellerType type) =>
        travellers.Where(traveller => traveller.Type == type).Select(traveller => traveller.Age);
}
