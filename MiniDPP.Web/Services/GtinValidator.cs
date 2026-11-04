namespace MiniDPP.Web.Services;

public static class GtinValidator
{
    public static bool IsValid(string? gtin)
    {
        if (gtin is null || gtin.Length != 14 || gtin.Any(c => c is < '0' or > '9'))
            return false;

        // GS1 GTIN-14: multiply rightmost DATA digit by 3, then alternate 1,3...
        var sum = 0;
        for (var i = 12; i >= 0; i--)
            sum += (gtin[i] - '0') * (i % 2 == 0 ? 3 : 1);

        var checkDigit = (10 - sum % 10) % 10;
        return checkDigit == gtin[13] - '0';
    }
}
