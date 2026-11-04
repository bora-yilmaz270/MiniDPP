using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiniDPP.Web.Data;
using MiniDPP.Web.Models;
using MiniDPP.Web.Services;

namespace MiniDPP.Web.Controllers;

public sealed class HomeController(AppDbContext db, CsvImportService importer) : Controller
{
    [HttpGet("/")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var products = await db.ProductModels.AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new ProductSummary(
                x.Gtin, x.Name, x.Batches.Count, x.CurrentVersion))
            .ToListAsync(cancellationToken);

        return View(new HomeIndexViewModel { Products = products });
    }

    [HttpPost("/import")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(5_000_000)]
    public async Task<IActionResult> Import(
        IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return View("ImportResult",
                new ImportReport { GeneralError = "Lütfen bir CSV dosyası seçin." });

        if (file.Length > 2_000_000)
            return View("ImportResult",
                new ImportReport { GeneralError = "Dosya en fazla 2 MB olabilir." });

        await using var source = file.OpenReadStream();
        var report = await importer.ImportAsync(source, cancellationToken);
        return View("ImportResult", report);
    }

    [HttpGet("/error")]
    public IActionResult Error() => View();
}
