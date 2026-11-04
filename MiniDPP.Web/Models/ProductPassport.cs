namespace MiniDPP.Web.Models;

public sealed record ProductPassport(
    string Gtin,
    string ModelName,
    string BatchNo,
    decimal CottonPercent,
    decimal ElastanePercent,
    string ProductionPlace,
    decimal CarbonKgCo2e,
    int VersionNumber);

public sealed record ProductSummary(
    string Gtin, string ModelName, int BatchCount, int VersionNumber);

public sealed class HomeIndexViewModel
{
    public List<ProductSummary> Products { get; set; } = [];
}
