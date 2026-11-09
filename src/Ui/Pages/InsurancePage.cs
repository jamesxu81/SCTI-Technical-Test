using Microsoft.Playwright;

namespace TechnicalTest.Ui.Pages;

public class InsurancePage : BasePage
{
    private const string Url = "";


    public InsurancePage(IPage page) : base(page)
    {
    }

    public Task NavigateAsync() =>
        throw new NotImplementedException();

    public Task SelectOneReturnJourney() =>
        throw new NotImplementedException();

    public Task FillDestination(string destination) =>
        throw new NotImplementedException();

    public Task FillTravelDates(string departureDate, string returnDate) =>
        throw new NotImplementedException();

    public Task FillTravellers(string numberOfTravellers) =>
        throw new NotImplementedException();

    public Task ClickGetPlanOptions() =>
        throw new NotImplementedException();

    public Task<bool> IsElementVisible(string selector) =>
        throw new NotImplementedException();

    public Task SelectPlan() =>
        throw new NotImplementedException();

    public Task CustomisePlan(string discount, string excess, bool includeCancellation, bool includeExtra) =>
        throw new NotImplementedException();

    public Task CompleteTravellerEligibility() =>
        throw new NotImplementedException();

    public Task AddTraveller(string travellerType, string firstName, string lastName, string dateOfBirth) =>
        throw new NotImplementedException();
}
