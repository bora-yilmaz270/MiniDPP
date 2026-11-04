namespace MiniDPP.Web.Models;

// Previous versions are persisted as immutable snapshots in ProductVersions.
// Current version remains in ProductModels + ProductBatches.
public sealed record ProductVersionEntry(
    int VersionNumber, DateTime? ArchivedAtUtc, bool IsCurrent);

public sealed record ProductHistoryViewModel(
    string Gtin, string ModelName, int CurrentVersion,
    IReadOnlyList<ProductVersionEntry> Versions);

// Matches the JSON saved by CsvImportService before an update.
public sealed record BatchSnapshot(
    string BatchNo, string ProductionPlace, decimal CarbonKgCo2e);

public sealed record ProductSnapshot(
    string Gtin, string Name, decimal CottonPercent,
    decimal ElastanePercent, List<BatchSnapshot> Batches);

public sealed record ProductVersionDetailViewModel(
    int VersionNumber, DateTime ArchivedAtUtc, ProductSnapshot Product);
