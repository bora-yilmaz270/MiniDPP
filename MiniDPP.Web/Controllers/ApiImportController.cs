using Microsoft.AspNetCore.Mvc;
using MiniDPP.Web.Models;
using MiniDPP.Web.Services;

namespace MiniDPP.Web.Controllers;

[ApiController]
[Route("api/import")]
public sealed class ApiImportController(CsvImportService importer) : ControllerBase
{
    [HttpPost]
    [RequestSizeLimit(5_000_000)]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ImportReport>> Upload(
        IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "Lütfen CSV dosyası gönderin (file alanı)." });
        if (file.Length > 2_000_000)
            return BadRequest(new { error = "Dosya en fazla 2 MB olabilir." });

        await using var stream = file.OpenReadStream();
        var report = await importer.ImportAsync(stream, cancellationToken);
        return Ok(report);
    }
}
