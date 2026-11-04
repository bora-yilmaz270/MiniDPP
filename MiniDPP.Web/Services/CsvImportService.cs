using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualBasic.FileIO;
using MiniDPP.Web.Data;
using MiniDPP.Web.Models;

namespace MiniDPP.Web.Services;

public sealed class CsvImportService(AppDbContext db)
{
    private static readonly string[] ExpectedHeaders =
    [
        "gtin", "model_adi", "batch_no", "elyaf_pamuk_yuzde",
        "elyaf_elastan_yuzde", "uretim_yeri", "karbon_kg_co2e"
    ];

    public async Task<ImportReport> ImportAsync(
        Stream source, CancellationToken cancellationToken = default)
    {
        var report = new ImportReport();
        using var reader = new StreamReader(source, Encoding.UTF8, detectEncodingFromByteOrderMarks: true,
            leaveOpen: true);
        using var parser = new TextFieldParser(reader)
        {
            TextFieldType = FieldType.Delimited,
            HasFieldsEnclosedInQuotes = true,
            TrimWhiteSpace = true
        };
        parser.SetDelimiters(",");

        string[]? headers;
        try
        {
            headers = parser.ReadFields();
        }
        catch (MalformedLineException)
        {
            report.GeneralError = "CSV başlık satırı okunamadı.";
            return report;
        }

        if (headers is null || headers.Length != ExpectedHeaders.Length ||
            !headers.Select(x => x.Trim().TrimStart('\uFEFF'))
                .SequenceEqual(ExpectedHeaders, StringComparer.OrdinalIgnoreCase))
        {
            report.GeneralError = "CSV sütun başlıkları beklenen formatta değil. Örnek CSV dosyasını kullanın.";
            return report;
        }

        while (!parser.EndOfData)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var lineNumber = parser.LineNumber;
            string[]? fields;
            try
            {
                fields = parser.ReadFields();
            }
            catch (MalformedLineException)
            {
                report.Errors.Add(new CsvRowError(parser.ErrorLineNumber,
                    "Bozuk CSV satırı: ayraç veya tırnak işaretlerini kontrol edin."));
                continue;
            }

            if (fields is null || fields.All(string.IsNullOrWhiteSpace))
                continue;

            var (row, errors) = CsvRowValidator.Validate(fields);
            if (row is null)
            {
                report.Errors.Add(new CsvRowError(lineNumber, string.Join(" ", errors)));
                continue;
            }

            try
            {
                switch (await SaveRowAsync(row, cancellationToken))
                {
                    case ImportStatus.Created: report.Created++; break;
                    case ImportStatus.Updated: report.Updated++; break;
                    case ImportStatus.Unchanged: report.Unchanged++; break;
                }
            }
            catch (DbUpdateException)
            {
                db.ChangeTracker.Clear();
                report.Errors.Add(new CsvRowError(lineNumber,
                    "Veritabanına kaydedilemedi (benzersiz alan veya veri kısıtı)."));
            }
        }

        return report;
    }

    private async Task<ImportStatus> SaveRowAsync(
        ProductCsvRow row, CancellationToken cancellationToken)
    {
        var model = await db.ProductModels.Include(x => x.Batches)
            .SingleOrDefaultAsync(x => x.Gtin == row.Gtin, cancellationToken);
        var now = DateTime.UtcNow;

        if (model is null)
        {
            model = new ProductModel
            {
                Gtin = row.Gtin,
                Name = row.ModelName,
                CottonPercent = row.CottonPercent,
                ElastanePercent = row.ElastanePercent,
                Batches =
                [
                    new ProductBatch
                    {
                        BatchNo = row.BatchNo,
                        ProductionPlace = row.ProductionPlace,
                        CarbonKgCo2e = row.CarbonKgCo2e,
                        UpdatedAtUtc = now
                    }
                ]
            };
            db.ProductModels.Add(model);
            await db.SaveChangesAsync(cancellationToken);
            return ImportStatus.Created;
        }

        var batch = model.Batches.SingleOrDefault(x => x.BatchNo == row.BatchNo);
        var hasChanges =
            model.Name != row.ModelName ||
            model.CottonPercent != row.CottonPercent ||
            model.ElastanePercent != row.ElastanePercent ||
            batch is null ||
            batch.ProductionPlace != row.ProductionPlace ||
            batch.CarbonKgCo2e != row.CarbonKgCo2e;

        if (!hasChanges)
            return ImportStatus.Unchanged;

        // Preserve the old state BEFORE changing the existing product (bonus).
        var snapshotJson = JsonSerializer.Serialize(new
        {
            model.Gtin,
            model.Name,
            model.CottonPercent,
            model.ElastanePercent,
            Batches = model.Batches.OrderBy(x => x.BatchNo).Select(x => new
            {
                x.BatchNo, x.ProductionPlace, x.CarbonKgCo2e
            }).ToArray()
        });
        db.ProductVersions.Add(new ProductVersion
        {
            ProductModelId = model.Id,
            VersionNumber = model.CurrentVersion,
            UpdatedAtUtc = now,
            SnapshotJson = snapshotJson
        });
        model.CurrentVersion++;
        model.Name = row.ModelName;
        model.CottonPercent = row.CottonPercent;
        model.ElastanePercent = row.ElastanePercent;

        if (batch is null)
        {
            model.Batches.Add(new ProductBatch
            {
                BatchNo = row.BatchNo,
                ProductionPlace = row.ProductionPlace,
                CarbonKgCo2e = row.CarbonKgCo2e,
                UpdatedAtUtc = now
            });
        }
        else
        {
            batch.ProductionPlace = row.ProductionPlace;
            batch.CarbonKgCo2e = row.CarbonKgCo2e;
            batch.UpdatedAtUtc = now;
        }

        await db.SaveChangesAsync(cancellationToken);
        return ImportStatus.Updated;
    }

    private enum ImportStatus { Created, Updated, Unchanged }
}
