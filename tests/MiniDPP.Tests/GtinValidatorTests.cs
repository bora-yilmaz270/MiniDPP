using MiniDPP.Web.Services;

namespace MiniDPP.Tests;

public sealed class GtinValidatorTests
{
    [Theory]
    [InlineData("08690000000012", true)]
    [InlineData("08690000000029", true)]
    [InlineData("08690000000050", true)]
    [InlineData("08690000000013", false)]
    [InlineData("0869000000001", false)]
    [InlineData("0869000000001A", false)]
    [InlineData("", false)]
    public void ValidatesGtin14AndGs1CheckDigit(string gtin, bool expected)
    {
        Assert.Equal(expected, GtinValidator.IsValid(gtin));
    }
}
