using TechnicalTest.Ui.Models;

namespace TechnicalTest.Tests.Support;

// Shared between step classes for one scenario through Reqnroll context injection.
public sealed class QuoteState
{
    public IReadOnlyList<QuoteTraveller> Travellers { get; set; } = Array.Empty<QuoteTraveller>();

    public DateOnly Departure { get; set; }

    public DateOnly Return { get; set; }

    public decimal BasePrice { get; set; }

    public decimal CustomisedPrice { get; set; }

    public string TotalPremium { get; set; } = string.Empty;

    public string PolicyholderEmail { get; set; } = string.Empty;

    public string PolicyholderMobile { get; set; } = string.Empty;

    public List<FilledTraveller> FilledTravellers { get; } = new();

    public int AddedTravellerIndex { get; set; } = -1;
}

public sealed record FilledTraveller(int Index, string FirstName, string LastName, DateOnly DateOfBirth);
