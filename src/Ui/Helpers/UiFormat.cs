using System.Globalization;

namespace TechnicalTest.Ui.Helpers;

public static class UiFormat
{
    public static string Date(DateOnly date) => date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    public static string Money(decimal amount) =>
        (amount < 0 ? "-$" : "$") + Math.Abs(amount).ToString("#,##0.00", CultureInfo.InvariantCulture);

    public static decimal ParseMoney(string text) =>
        decimal.Parse(
            PlainAmount(text).Trim(),
            NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture);

    public static string PlainAmount(string amount) =>
        amount.Replace("$", string.Empty).Replace(",", string.Empty);
}
