using MiniDPP.Web.Services;

namespace MiniDPP.Tests;

public sealed class CsvRowValidatorTests
{
    private static readonly string[] ValidRow =
        ["08690000000012", "Slim Denim 501", "B2027-01", "98", "2", "Denizli", "12.4"];

    [Fact]
    public void ValidRow_PreservesLeadingZeroAndDecimalCarbon()
    {
        var (row, errors) = CsvRowValidator.Validate(ValidRow);
        Assert.Empty(errors);
        Assert.NotNull(row);
        Assert.Equal("08690000000012", row.Gtin);
        Assert.Equal(12.4m, row.CarbonKgCo2e);
    }

    [Fact]
    public void RejectsIncorrectFiberTotal()
    {
        var fields = (string[])ValidRow.Clone();
        fields[3] = "95";
        fields[4] = "3";

        var (row, errors) = CsvRowValidator.Validate(fields);
        Assert.Null(row);
        Assert.Contains(errors, x => x.Contains("%100"));
    }

    [Fact]
    public void RejectsMissingFieldsAndNegativeCarbon()
    {
        var fields = (string[])ValidRow.Clone();
        fields[0] = "";
        fields[1] = "";
        fields[2] = "";
        fields[5] = "";
        fields[6] = "-1";

        var (row, errors) = CsvRowValidator.Validate(fields);
        Assert.Null(row);
        Assert.Equal(5, errors.Count);
    }

    [Fact]
    public void RejectsNonNumericAndOutOfRangeFiberValues()
    {
        var fields = (string[])ValidRow.Clone();
        fields[3] = "abc";
        fields[4] = "101";

        var (row, errors) = CsvRowValidator.Validate(fields);
        Assert.Null(row);
        Assert.Contains(errors, x => x.Contains("sayısal"));
        Assert.Contains(errors, x => x.Contains("0–100"));
    }

    [Fact]
    public void RejectsInvalidColumnCount()
    {
        var (row, errors) = CsvRowValidator.Validate(["too", "few"]);
        Assert.Null(row);
        Assert.Single(errors);
    }
}
