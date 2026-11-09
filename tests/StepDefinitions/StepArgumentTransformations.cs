using Reqnroll;
using TechnicalTest.Tests.Support;
using TechnicalTest.Ui.Models;

namespace TechnicalTest.Tests.StepDefinitions;

[Binding]
public class StepArgumentTransformations
{
    [StepArgumentTransformation(@"^today\+(\d+)$")]
    public DateOnly DaysFromToday(int days) => TestData.TodayInNewZealand.AddDays(days);

    [StepArgumentTransformation(@"^age:(\d+)$")]
    public DateOnly DateOfBirthForAge(int age) => TestData.DateOfBirthForAge(age);

    [StepArgumentTransformation]
    public IReadOnlyList<QuoteTraveller> Travellers(string travellers) => TestData.ParseTravellers(travellers);
}
