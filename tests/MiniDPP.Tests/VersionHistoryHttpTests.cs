using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace MiniDPP.Tests;

public sealed class VersionHistoryHttpTests
{
    [Fact]
    public async Task ArchivedVersionPages_ShowHistoricalData_WithoutLeakingCarbonByDefault()
    {
        var databasePath = Path.Combine(Path.GetTempPath(),
            "minidpp-versions-" + Guid.NewGuid().ToString("N") + ".db");

        try
        {
            using var factory = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.UseContentRoot(Path.GetFullPath(
                        Path.Combine(AppContext.BaseDirectory, "../../../../../MiniDPP.Web")));
                    builder.ConfigureAppConfiguration((_, config) =>
                        config.AddInMemoryCollection(new Dictionary<string, string?>
                        {
                            ["ConnectionStrings:Default"] = "Data Source=" + databasePath
                        }));
                });
            using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost")
            });

            const string gtin = "08690000000012";
            const string header = "gtin,model_adi,batch_no,elyaf_pamuk_yuzde,elyaf_elastan_yuzde,uretim_yeri,karbon_kg_co2e\n";
            async Task Upload(string carbon)
            {
                var csv = header +
                    $"08690000000012,Slim Denim 501,B2027-01,98,2,Denizli,{carbon}\n";
                using var form = new MultipartFormDataContent();
                form.Add(new ByteArrayContent(Encoding.UTF8.GetBytes(csv)), "file", "update.csv");
                var response = await client.PostAsync("/api/import", form);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            }

            await Upload("12.4");
            await Upload("10.1");
            await Upload("9.5");

            var current = await client.GetStringAsync("/dpp/" + gtin);
            Assert.Contains("Sürüm geçmişi", current);

            var history = await client.GetStringAsync($"/dpp/{gtin}/versions");
            Assert.Contains("V3", history);
            Assert.Contains("V2", history);
            Assert.Contains("V1", history);
            Assert.Contains("Güncelleme tarihi (UTC)", history);
            Assert.DoesNotContain("12.4 kg", history);

            var oldPublic = await client.GetStringAsync($"/dpp/{gtin}/versions/1");
            Assert.Contains("Slim Denim 501", oldPublic);
            Assert.Contains("B2027-01", oldPublic);
            Assert.DoesNotContain("12.4 kg", oldPublic);

            var oldAuthorized = await client.GetStringAsync(
                $"/dpp/{gtin}/versions/1?rol=yetkili");
            Assert.Contains("12.4 kg CO₂e", oldAuthorized);

            var previousAuthorized = await client.GetStringAsync(
                $"/dpp/{gtin}/versions/2?rol=yetkili");
            Assert.Contains("10.1 kg CO₂e", previousAuthorized);
            Assert.DoesNotContain("12.4 kg CO₂e", previousAuthorized);

            var currentAuthorized = await client.GetStringAsync(
                $"/dpp/{gtin}?rol=yetkili");
            Assert.Contains("9.5 kg CO₂e", currentAuthorized);

            Assert.Equal(HttpStatusCode.NotFound,
                (await client.GetAsync($"/dpp/{gtin}/versions/3")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound,
                (await client.GetAsync($"/dpp/{gtin}/versions/0")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound,
                (await client.GetAsync("/dpp/00000000000000/versions")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound,
                (await client.GetAsync("/dpp/00000000000000/versions/1")).StatusCode);
        }
        finally
        {
            if (File.Exists(databasePath)) File.Delete(databasePath);
            if (File.Exists(databasePath + "-wal")) File.Delete(databasePath + "-wal");
            if (File.Exists(databasePath + "-shm")) File.Delete(databasePath + "-shm");
        }
    }
}
