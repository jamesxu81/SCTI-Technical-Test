using System.Globalization;
using TechnicalTest.Ui.Models;

namespace TechnicalTest.Tests.Support;

public static class TestData
{
    private static readonly TimeZoneInfo NewZealand = TimeZoneInfo.FindSystemTimeZoneById("Pacific/Auckland");

    public static DateOnly TodayInNewZealand =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, NewZealand));

    // Birthday one month ago, so the traveller is exactly this age on the quote date.
    public static DateOnly DateOfBirthForAge(int age) => TodayInNewZealand.AddYears(-age).AddMonths(-1);

    // "Adult:40, DependentChild:10" -> one traveller per entry.
    public static IReadOnlyList<QuoteTraveller> ParseTravellers(string travellers) =>
        travellers.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(entry => entry.Split(':', StringSplitOptions.TrimEntries))
            .Select(parts => new QuoteTraveller(
                Enum.Parse<TravellerType>(parts[0]),
                int.Parse(parts[1], CultureInfo.InvariantCulture)))
            .ToList();

    public static IReadOnlyList<string> SplitList(string value) =>
        value.Equals("None", StringComparison.OrdinalIgnoreCase)
            ? Array.Empty<string>()
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
