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

    public static bool CanSeeCarbon(string? role) =>
        string.Equals(role, "yetkili", StringComparison.OrdinalIgnoreCase);
}
