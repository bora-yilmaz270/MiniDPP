using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using MiniDPP.Web.Models;
using MiniDPP.Web.Services;
using QRCoder;

namespace MiniDPP.Web.Controllers;

public sealed class DppController(DppService service) : Controller
{
    [HttpGet("/dpp/{gtin}")]
    public async Task<IActionResult> Passport(
        string gtin, [FromQuery] string? rol, CancellationToken cancellationToken)
    {
        var product = await service.GetAsync(gtin, cancellationToken);
        if (product is null) return NotFound();

        ViewData["ShowCarbon"] = DppService.CanSeeCarbon(rol);
        return View(product);
    }

    [HttpGet("/dpp/{gtin}.jsonld")]
    public async Task<IActionResult> JsonLd(
        string gtin, [FromQuery] string? rol, CancellationToken cancellationToken)
    {
        var product = await service.GetAsync(gtin, cancellationToken);
        if (product is null) return NotFound();

        var properties = new List<Dictionary<string, object>>
        {
            Property("elyaf_pamuk_yuzde", product.CottonPercent),
            Property("elyaf_elastan_yuzde", product.ElastanePercent),
            Property("batch_no", product.BatchNo),
            Property("uretim_yeri", product.ProductionPlace)
        };

        if (DppService.CanSeeCarbon(rol))
            properties.Add(Property("karbon_kg_co2e", product.CarbonKgCo2e));

        var jsonLd = new Dictionary<string, object>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "Product",
            ["name"] = product.ModelName,
            ["gtin14"] = product.Gtin,
            ["material"] = $"{product.CottonPercent}% cotton, {product.ElastanePercent}% elastane",
            ["sku"] = product.BatchNo,
            ["additionalProperty"] = properties
        };

        return Content(JsonSerializer.Serialize(jsonLd, new JsonSerializerOptions
        {
            WriteIndented = true
        }), "application/ld+json; charset=utf-8");
    }

    [HttpGet("/dpp/{gtin}/qr.png")]
    public async Task<IActionResult> Qr(
        string gtin, CancellationToken cancellationToken)
    {
        if (await service.GetAsync(gtin, cancellationToken) is null)
            return NotFound();

        // QR always points to the public HTML page (never a role-elevated URL).
        var url = Url.Action(nameof(Passport), "Dpp", new { gtin }, Request.Scheme);
        if (url is null) return StatusCode(500, "Pasaport bağlantısı oluşturulamadı.");

        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(url, QRCodeGenerator.ECCLevel.Q);
        var png = new PngByteQRCode(data).GetGraphic(12);
        return File(png, "image/png");
    }

    private static Dictionary<string, object> Property(string name, object value) =>
        new()
        {
            ["@type"] = "PropertyValue",
            ["name"] = name,
            ["value"] = value
        };
}
