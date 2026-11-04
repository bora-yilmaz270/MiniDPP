namespace MiniDPP.Web.Models;

public sealed record ProductCsvRow(
    string Gtin,
    string ModelName,
    string BatchNo,
    decimal CottonPercent,
    decimal ElastanePercent,
    string ProductionPlace,
    decimal CarbonKgCo2e);
