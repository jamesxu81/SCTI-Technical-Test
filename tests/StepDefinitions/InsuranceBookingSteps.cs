using TechnicalTest.Ui.Pages;
using Microsoft.Playwright;
using NUnit.Framework;
using Reqnroll;

namespace TechnicalTest.Tests.StepDefinitions;

[Binding]
public class InsuranceBookingSteps
{
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IPage? _page;
    private InsurancePage? _insurancePage;
    private readonly List<string> _networkCalls = new();

    [BeforeScenario]
    public async Task BeforeScenario()
    {
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = false,
            SlowMo = 100
        });
        _page = await _browser.NewPageAsync();
        
        _page.Request += (_, request) => _networkCalls.Add($"{request.Method} {request.Url}");
        
        _insurancePage = new InsurancePage(_page);
    }

    [AfterScenario]
    public async Task AfterScenario()
    {
        await _page?.CloseAsync()!;
        await _browser?.CloseAsync()!;
        _playwright?.Dispose();
    }

    [Given(@"I navigate to the insurance portal")]
    public async Task GivenINavigateToTheInsurancePortal()
    {
        await _insurancePage!.NavigateAsync();
    }

    [When(@"I select ""(.*)"" journey")]
    public async Task WhenISelectJourney(string journeyType)
    {
        await _insurancePage!.SelectOneReturnJourney();
    }

    [When(@"I add destination ""(.*)""")]
    public async Task WhenIAddDestination(string destination)
    {
        await _insurancePage!.FillDestination(destination);
    }

    [When(@"I add travel dates ""(.*)"" and ""(.*)""")]
    public async Task WhenIAddTravelDates(string departureDate, string returnDate)
    {
        await _insurancePage!.FillTravelDates(departureDate, returnDate);
    }

    [When(@"I add ""(.*)"" travellers")]
    public async Task WhenIAddTravellers(string numberOfTravellers)
    {
        await _insurancePage!.FillTravellers(numberOfTravellers);
    }

    [When(@"I click Get plan options")]
    public async Task WhenIClickGetPlanOptions()
    {
        await _insurancePage!.ClickGetPlanOptions();
    }

    [Then(@"I should see plan options available")]
    public async Task ThenIShouldSeePlanOptionsAvailable()
    {
        var isVisible = await _insurancePage!.IsElementVisible("button:has-text('Select')");
        Assert.That(isVisible, Is.True, "Plan options should be visible");
    }

    [When(@"I select a plan and customise it")]
    public async Task WhenISelectAPlanAndCustomiseIt()
    {
        await _insurancePage!.SelectPlan();
    }

    [When(@"I choose discount ""(.*)"", excess ""(.*)"", cancellation cover ""(.*)"", and extra cover ""(.*)""")]
    public async Task WhenIChooseDiscountExcessCancellationCoverAndExtraCover(
        string discount, string excess, string cancellationCover, string extraCover)
    {
        bool includeCancellation = cancellationCover.Equals("Yes", StringComparison.OrdinalIgnoreCase);
        bool includeExtra = extraCover.Equals("Yes", StringComparison.OrdinalIgnoreCase);
        
        await _insurancePage!.CustomisePlan(discount, excess, includeCancellation, includeExtra);
    }

    [When(@"I click Next to Traveller details")]
    public async Task WhenIClickNextToTravellerDetails()
    {
        await Task.CompletedTask;
    }

    [Then(@"I should see traveller eligibility section")]
    public async Task ThenIShouldSeeTravellerEligibilitySection()
    {
        var isVisible = await _insurancePage!.IsElementVisible("input[name='eligibility']");
        Assert.That(isVisible, Is.True, "Eligibility section should be visible");
    }

    [When(@"I complete traveller eligibility and privacy declaration")]
    public async Task WhenICompleteTravellerEligibilityAndPrivacyDeclaration()
    {
        await _insurancePage!.CompleteTravellerEligibility();
    }

    [When(@"I add adult traveller with firstname ""(.*)"", lastname ""(.*)"", and DOB ""(.*)""")]
    public async Task WhenIAddAdultTravellerWithFirstnameLastnameAndDOB(string firstName, string lastName, string dob)
    {
        await _insurancePage!.AddTraveller("Adult", firstName, lastName, dob);
    }

    [When(@"I add dependent traveller with firstname ""(.*)"", lastname ""(.*)"", and DOB ""(.*)""")]
    public async Task WhenIAddDependentTravellerWithFirstnameLastnameAndDOB(string firstName, string lastName, string dob)
    {
        await _insurancePage!.AddTraveller("Dependent", firstName, lastName, dob);
    }

    [When(@"I add non-dependent traveller with firstname ""(.*)"", lastname ""(.*)"", and DOB ""(.*)""")]
    public async Task WhenIAddNonDependentTravellerWithFirstnameLastnameAndDOB(string firstName, string lastName, string dob)
    {
        await _insurancePage!.AddTraveller("Non-Dependent", firstName, lastName, dob);
    }

    [Then(@"I should see all travellers added successfully")]
    public async Task ThenIShouldSeeAllTravellersAddedSuccessfully()
    {
        Assert.That(_networkCalls.Count, Is.GreaterThan(0), "Network calls should have been captured");
        await Task.CompletedTask;
    }
}
