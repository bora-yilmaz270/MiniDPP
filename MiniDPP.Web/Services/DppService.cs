using Microsoft.EntityFrameworkCore;
using MiniDPP.Web.Data;
using MiniDPP.Web.Models;

namespace MiniDPP.Web.Services;

public sealed class DppService(AppDbContext db)
{
    public async Task<ProductPassport?> GetAsync(
        string gtin, CancellationToken cancellationToken = default)
    {
        var model = await db.ProductModels.AsNoTracking()
            .Include(x => x.Batches)
            .SingleOrDefaultAsync(x => x.Gtin == gtin, cancellationToken);

        if (model is null) return null;

        // Since the assignment specifies /dpp/{gtin} (no lot in URL),
        // show the most recently imported batch for that model.
        var batch = model.Batches.OrderByDescending(x => x.UpdatedAtUtc)
            .ThenByDescending(x => x.Id).FirstOrDefault();
        if (batch is null) return null;

        return new ProductPassport(
            model.Gtin, model.Name, batch.BatchNo,
            model.CottonPercent, model.ElastanePercent,
            batch.ProductionPlace, batch.CarbonKgCo2e, model.CurrentVersion);
    }


    public async Task<ProductHistoryViewModel?> GetHistoryAsync(
        string gtin, CancellationToken cancellationToken = default)
    {
        var model = await db.ProductModels.AsNoTracking()
            .Where(x => x.Gtin == gtin)
            .Select(x => new { x.Gtin, x.Name, x.CurrentVersion })
            .SingleOrDefaultAsync(cancellationToken);

        if (model is null) return null;

        var previousVersions = await db.ProductVersions.AsNoTracking()
            .Where(x => x.ProductModel.Gtin == gtin)
            .OrderByDescending(x => x.VersionNumber)
            .Select(x => new ProductVersionEntry(
                x.VersionNumber, x.UpdatedAtUtc, false))
            .ToListAsync(cancellationToken);

        var versions = new List<ProductVersionEntry>
        {
            new(model.CurrentVersion, null, true)
        };
        versions.AddRange(previousVersions);

        return new ProductHistoryViewModel(
            model.Gtin, model.Name, model.CurrentVersion, versions);
    }

    public async Task<ProductVersionDetailViewModel?> GetPreviousVersionAsync(
        string gtin, int versionNumber, CancellationToken cancellationToken = default)
    {
        if (versionNumber < 1) return null;

        var stored = await db.ProductVersions.AsNoTracking()
            .Where(x => x.ProductModel.Gtin == gtin && x.VersionNumber == versionNumber)
            .Select(x => new { x.VersionNumber, x.UpdatedAtUtc, x.SnapshotJson })
            .SingleOrDefaultAsync(cancellationToken);

        if (stored is null) return null;

        var snapshot = System.Text.Json.JsonSerializer.Deserialize<ProductSnapshot>(
            stored.SnapshotJson);
        if (snapshot is null)
            throw new System.Text.Json.JsonException("Sürüm verisi okunamadı.");

        return new ProductVersionDetailViewModel(
            stored.VersionNumber, stored.UpdatedAtUtc, snapshot);
    }

    public static bool CanSeeCarbon(string? role) =>
        string.Equals(role, "yetkili", StringComparison.OrdinalIgnoreCase);
}
