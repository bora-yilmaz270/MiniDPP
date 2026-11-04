using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MiniDPP.Web.Data;
using MiniDPP.Web.Services;

namespace MiniDPP.Tests;

public sealed class VersionHistoryTests
{
    private const string Header =
        "gtin,model_adi,batch_no,elyaf_pamuk_yuzde,elyaf_elastan_yuzde,uretim_yeri,karbon_kg_co2e\n";

    [Fact]
    public async Task NewModel_HasOnlyCurrentVersion_AndUnknownVersionsAreNotFound()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = NewDatabase(connection);
        await db.Database.EnsureCreatedAsync();
        var importer = new CsvImportService(db);
        var service = new DppService(db);

        await ImportAsync(importer, "12.4");
        var history = await service.GetHistoryAsync("08690000000012");

        Assert.NotNull(history);
        Assert.Equal(1, history.CurrentVersion);
        Assert.Single(history.Versions);
        Assert.True(history.Versions[0].IsCurrent);
        Assert.Null(history.Versions[0].ArchivedAtUtc);
        Assert.Null(await service.GetPreviousVersionAsync("08690000000012", 1));
        Assert.Null(await service.GetPreviousVersionAsync("08690000000012", 0));
        Assert.Null(await service.GetHistoryAsync("00000000000000"));
        Assert.Null(await service.GetPreviousVersionAsync("00000000000000", 1));
    }

    [Fact]
    public async Task TwoUpdates_KeepOrderedPreviousSnapshotsWithUpdateTimestamps()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = NewDatabase(connection);
        await db.Database.EnsureCreatedAsync();
        var importer = new CsvImportService(db);
        var service = new DppService(db);

        Assert.Equal(1, (await ImportAsync(importer, "12.4")).Created);
        Assert.Equal(1, (await ImportAsync(importer, "10.1")).Updated);
        Assert.Equal(1, (await ImportAsync(importer, "9.5")).Updated);
        Assert.Equal(1, (await ImportAsync(importer, "9.5")).Unchanged);

        var history = await service.GetHistoryAsync("08690000000012");
        Assert.NotNull(history);
        Assert.Equal(3, history.CurrentVersion);
        Assert.Equal(new[] { 3, 2, 1 },
            history.Versions.Select(x => x.VersionNumber));
        Assert.True(history.Versions[0].IsCurrent);
        Assert.All(history.Versions.Skip(1), x =>
        {
            Assert.False(x.IsCurrent);
            Assert.NotNull(x.ArchivedAtUtc);
            Assert.True(x.ArchivedAtUtc > DateTime.UtcNow.AddMinutes(-10));
        });

        var oldV1 = await service.GetPreviousVersionAsync("08690000000012", 1);
        var oldV2 = await service.GetPreviousVersionAsync("08690000000012", 2);
        Assert.NotNull(oldV1);
        Assert.NotNull(oldV2);
        Assert.Equal("08690000000012", oldV1.Product.Gtin);
        Assert.Equal("Slim Denim 501", oldV1.Product.Name);
        Assert.Equal(12.4m, Assert.Single(oldV1.Product.Batches).CarbonKgCo2e);
        Assert.Equal(10.1m, Assert.Single(oldV2.Product.Batches).CarbonKgCo2e);
        Assert.Equal(9.5m, (await service.GetAsync("08690000000012"))!.CarbonKgCo2e);
        Assert.Null(await service.GetPreviousVersionAsync("08690000000012", 3));
        Assert.Equal(2, await db.ProductVersions.CountAsync());
    }

    [Fact]
    public async Task PreviousSnapshot_PreservesAllBatchesBeforeNewBatchIsAdded()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = NewDatabase(connection);
        await db.Database.EnsureCreatedAsync();
        var importer = new CsvImportService(db);
        var service = new DppService(db);

        await ImportAsync(importer, "12.4");
        var additionalBatch = Header +
            "08690000000012,Slim Denim 501,B2027-02,98,2,Bursa,13.1\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(additionalBatch));
        var result = await importer.ImportAsync(stream);

        Assert.Equal(1, result.Updated);
        var old = await service.GetPreviousVersionAsync("08690000000012", 1);
        Assert.NotNull(old);
        Assert.Single(old.Product.Batches);
        Assert.Equal("B2027-01", old.Product.Batches[0].BatchNo);
        Assert.Equal(2, await db.ProductBatches.CountAsync());
    }

    private static AppDbContext NewDatabase(SqliteConnection connection) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);

    private static async Task<MiniDPP.Web.Models.ImportReport> ImportAsync(
        CsvImportService importer, string carbon)
    {
        var csv = Header +
            $"08690000000012,Slim Denim 501,B2027-01,98,2,Denizli,{carbon}\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        return await importer.ImportAsync(stream);
    }
}
