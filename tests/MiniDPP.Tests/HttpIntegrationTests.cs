using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace MiniDPP.Tests;

public sealed class HttpIntegrationTests
{
    [Fact]
    public async Task CsvUpload_Html_JsonLd_AndQrPng_WorkOverActualHttpRoutes()
    {
        var dbPath = Path.Combine(Path.GetTempPath(),
            "minidpp-http-tests-" + Guid.NewGuid().ToString("N") + ".db");

        try
        {
            using var factory = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder =>
                {
                    // Explicit root avoids depending on how the test runner locates .slnx.
                    builder.UseContentRoot(Path.GetFullPath(
                        Path.Combine(AppContext.BaseDirectory, "../../../../../MiniDPP.Web")));
                    builder.ConfigureAppConfiguration((_, configuration) =>
                        configuration.AddInMemoryCollection(
                            new Dictionary<string, string?>
                            {
                                ["ConnectionStrings:Default"] = "Data Source=" + dbPath
                            }));
                });

            using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost")
            });

            var homepage = await client.GetAsync("/");
            Assert.Equal(HttpStatusCode.OK, homepage.StatusCode);

            var csv = "gtin,model_adi,batch_no,elyaf_pamuk_yuzde,elyaf_elastan_yuzde,uretim_yeri,karbon_kg_co2e\n" +
                      "08690000000012,Slim Denim 501,B2027-01,98,2,Denizli,12.4\n" +
                      ",Bad Denim,B2027-02,99,1,Denizli,10.0\n";
            using var form = new MultipartFormDataContent();
            form.Add(new ByteArrayContent(Encoding.UTF8.GetBytes(csv)), "file", "example.csv");

            var upload = await client.PostAsync("/api/import", form);
            Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
            using var import = JsonDocument.Parse(await upload.Content.ReadAsStringAsync());
            Assert.Equal(1, import.RootElement.GetProperty("created").GetInt32());
            Assert.Equal(1, import.RootElement.GetProperty("rejected").GetInt32());

            const string gtin = "08690000000012";
            var publicHtml = await client.GetStringAsync("/dpp/" + gtin);
            Assert.Contains("Slim Denim 501", publicHtml);
            Assert.DoesNotContain("12.4 kg", publicHtml);

            var authorizedHtml = await client.GetStringAsync("/dpp/" + gtin + "?rol=yetkili");
            Assert.Contains("12.4 kg", authorizedHtml);

            using var publicJson = JsonDocument.Parse(
                await client.GetStringAsync("/dpp/" + gtin + ".jsonld"));
            using var authorizedJson = JsonDocument.Parse(
                await client.GetStringAsync("/dpp/" + gtin + ".jsonld?rol=yetkili"));
            Assert.Equal("Product", publicJson.RootElement.GetProperty("@type").GetString());
            Assert.DoesNotContain("karbon_kg_co2e", publicJson.RootElement.GetRawText());
            Assert.Contains("karbon_kg_co2e", authorizedJson.RootElement.GetRawText());

            var qr = await client.GetAsync("/dpp/" + gtin + "/qr.png");
            Assert.Equal(HttpStatusCode.OK, qr.StatusCode);
            Assert.Equal("image/png", qr.Content.Headers.ContentType?.MediaType);
            var bytes = await qr.Content.ReadAsByteArrayAsync();
            Assert.True(bytes.Length > 100);
            Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, bytes[..8]);

            Assert.Equal(HttpStatusCode.NotFound,
                (await client.GetAsync("/dpp/00000000000000")).StatusCode);
        }
        finally
        {
            if (File.Exists(dbPath)) File.Delete(dbPath);
            if (File.Exists(dbPath + "-wal")) File.Delete(dbPath + "-wal");
            if (File.Exists(dbPath + "-shm")) File.Delete(dbPath + "-shm");
        }
    }
}
