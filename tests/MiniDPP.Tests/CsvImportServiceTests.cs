using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MiniDPP.Web.Data;
using MiniDPP.Web.Services;

namespace MiniDPP.Tests;

public sealed class CsvImportServiceTests
{
    private const string Header =
        "gtin,model_adi,batch_no,elyaf_pamuk_yuzde,elyaf_elastan_yuzde,uretim_yeri,karbon_kg_co2e\n";

    private const string Sample =
        Header +
        "08690000000012,Slim Denim 501,B2027-01,98,2,Denizli,12.4\n" +
        "08690000000029,Regular Denim 502,B2027-02,99,1,Denizli,11.8\n" +
        ",Wide Leg Denim 503,B2027-03,100,0,Bursa,13.1\n" +
        "08690000000043,Skinny Denim 504,B2027-04,95,3,Denizli,12.9\n" +
        "08690000000050,Mom Jean 505,B2027-05,97,3,Bursa,12.2\n";

    [Fact]
    public async Task Sample_StoresThreeValidRows_AndReportsPhysicalCsvLineNumbers()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = NewDatabase(connection);
        await db.Database.EnsureCreatedAsync();

        var report = await Import(new CsvImportService(db), Sample);

        Assert.Null(report.GeneralError);
        Assert.Equal(3, report.Created);
        Assert.Equal(2, report.Rejected);
        Assert.Contains(report.Errors, x => x.Line == 4 && x.Reason.Contains("GTIN"));
        Assert.Contains(report.Errors, x => x.Line == 5 && x.Reason.Contains("%100"));
        Assert.Equal(3, await db.ProductModels.CountAsync());
        Assert.Equal(3, await db.ProductBatches.CountAsync());
        Assert.Contains(await db.ProductModels.ToListAsync(),
            p => p.Gtin == "08690000000012");
    }

    [Fact]
    public async Task ReimportIsIdempotent_AndChangeCreatesVersionWithOldSnapshot()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = NewDatabase(connection);
        await db.Database.EnsureCreatedAsync();
        var importer = new CsvImportService(db);
        var first = Header + "08690000000012,Slim Denim 501,B2027-01,98,2,Denizli,12.4\n";
        var updated = Header + "08690000000012,Slim Denim 501,B2027-01,98,2,Denizli,10.1\n";

        Assert.Equal(1, (await Import(importer, first)).Created);
        Assert.Equal(1, (await Import(importer, first)).Unchanged);
        Assert.Empty(await db.ProductVersions.ToListAsync());

        Assert.Equal(1, (await Import(importer, updated)).Updated);
        var model = await db.ProductModels.Include(x => x.Batches).SingleAsync();
        var version = await db.ProductVersions.SingleAsync();
        Assert.Equal(2, model.CurrentVersion);
        Assert.Equal(1, version.VersionNumber);
        Assert.Equal(10.1m, model.Batches.Single().CarbonKgCo2e);

        using var old = JsonDocument.Parse(version.SnapshotJson);
        Assert.Equal(12.4m, old.RootElement.GetProperty("Batches")[0]
            .GetProperty("CarbonKgCo2e").GetDecimal());
        Assert.Equal(1, (await Import(importer, updated)).Unchanged);
        Assert.Single(await db.ProductVersions.ToListAsync());
    }

    [Fact]
    public async Task SameGtinWithNewBatch_UsesOneModelAndTwoRelatedBatches()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = NewDatabase(connection);
        await db.Database.EnsureCreatedAsync();

        var input = Header +
            "08690000000012,Slim Denim 501,B2027-01,98,2,Denizli,12.4\n" +
            "08690000000012,Slim Denim 501,B2027-99,98,2,Bursa,13.4\n";
        var report = await Import(new CsvImportService(db), input);

        Assert.Equal(1, report.Created);
        Assert.Equal(1, report.Updated);
        Assert.Equal(1, await db.ProductModels.CountAsync());
        Assert.Equal(2, await db.ProductBatches.CountAsync());
        Assert.Equal(1, await db.ProductVersions.CountAsync());
    }

    [Fact]
    public async Task RejectsWrongCsvHeadersWithoutWritingRows()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = NewDatabase(connection);
        await db.Database.EnsureCreatedAsync();

        var result = await Import(new CsvImportService(db), "bad,headers\n1,2\n");
        Assert.NotNull(result.GeneralError);
        Assert.Equal(0, await db.ProductModels.CountAsync());
    }

    private static AppDbContext NewDatabase(SqliteConnection connection) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);

    private static async Task<MiniDPP.Web.Models.ImportReport> Import(
        CsvImportService importer, string content)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        return await importer.ImportAsync(stream);
    }
}
