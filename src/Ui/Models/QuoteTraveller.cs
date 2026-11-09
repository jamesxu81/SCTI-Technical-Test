namespace TechnicalTest.Ui.Models;

// Same order as the rows and Increment buttons in the site's travellers dialog.
public enum TravellerType
{
    Adult,
    DependentChild,
    NonDependentChild
}

public sealed record QuoteTraveller(TravellerType Type, int Age);
