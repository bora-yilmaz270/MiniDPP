using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MiniDPP.Web.Controllers;
using MiniDPP.Web.Data;
using MiniDPP.Web.Models;
using MiniDPP.Web.Services;

namespace MiniDPP.Tests;

public sealed class DppControllerTests
{
    [Fact]
    public async Task HtmlAndJsonLd_HideCarbonByDefault_AndShowOnlyForAuthorizedDemoRole()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using var db = new AppDbContext(options);
        await db.Database.EnsureCreatedAsync();
        db.ProductModels.Add(new ProductModel
        {
            Gtin = "08690000000012", Name = "Slim Denim 501",
            CottonPercent = 98m, ElastanePercent = 2m,
            Batches = [new ProductBatch
            {
                BatchNo = "B2027-01", ProductionPlace = "Denizli", CarbonKgCo2e = 12.4m
            }]
        });
        await db.SaveChangesAsync();

        var controller = new DppController(new DppService(db));
        var publicHtml = Assert.IsType<ViewResult>(
            await controller.Passport("08690000000012", null, CancellationToken.None));
        Assert.Equal(false, publicHtml.ViewData["ShowCarbon"]);

        var authorizedHtml = Assert.IsType<ViewResult>(
            await controller.Passport("08690000000012", "yetkili", CancellationToken.None));
        Assert.Equal(true, authorizedHtml.ViewData["ShowCarbon"]);

        var publicJson = Assert.IsType<ContentResult>(
            await controller.JsonLd("08690000000012", null, CancellationToken.None));
        var authorizedJson = Assert.IsType<ContentResult>(
            await controller.JsonLd("08690000000012", "yetkili", CancellationToken.None));

        Assert.DoesNotContain("karbon_kg_co2e", publicJson.Content!);
        Assert.Contains("karbon_kg_co2e", authorizedJson.Content!);

        using var doc = JsonDocument.Parse(authorizedJson.Content!);
        Assert.Equal("Product", doc.RootElement.GetProperty("@type").GetString());
        Assert.Equal("08690000000012", doc.RootElement.GetProperty("gtin14").GetString());
        Assert.StartsWith("application/ld+json", authorizedJson.ContentType);
    }

    [Fact]
    public async Task MissingProduct_Returns404ForAllFormats()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var controller = new DppController(new DppService(db));

        Assert.IsType<NotFoundResult>(
            await controller.Passport("missing", null, CancellationToken.None));
        Assert.IsType<NotFoundResult>(
            await controller.JsonLd("missing", null, CancellationToken.None));
        Assert.IsType<NotFoundResult>(
            await controller.Qr("missing", CancellationToken.None));
    }
}
