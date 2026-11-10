namespace TechnicalTest.Ui.Helpers;

// The site's own quote API, used by the page objects to wait and by the tests to assert.
public static class QuoteApi
{
    public const string Create = "/api/Quotes/create";
    public const string Extras = "/api/Quotes/extras";
    public const string Select = "/api/Quotes/select";
}
