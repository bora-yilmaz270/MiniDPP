using System.Globalization;
using MiniDPP.Web.Models;

namespace MiniDPP.Web.Services;

public static class CsvRowValidator
{
    public static (ProductCsvRow? Row, List<string> Errors) Validate(string[] fields)
    {
        var errors = new List<string>();
        if (fields.Length != 7)
            return (null, ["Tam olarak 7 sütun bekleniyor."]);

        var values = fields.Select(x => x.Trim()).ToArray();
        var gtin = values[0];
        if (gtin.Length == 0)
            errors.Add("GTIN boş olamaz.");
        else if (!GtinValidator.IsValid(gtin))
            errors.Add("GTIN 14 haneli sayısal metin olmalı ve GS1 kontrol basamağı doğru olmalı.");

        if (values[1].Length == 0) errors.Add("Model adı boş olamaz.");
        if (values[2].Length == 0) errors.Add("Batch numarası boş olamaz.");
        if (values[5].Length == 0) errors.Add("Üretim yeri boş olamaz.");

        var cottonOk = TryPercent(values[3], "Pamuk yüzdesi", errors, out var cotton);
        var elastaneOk = TryPercent(values[4], "Elastan yüzdesi", errors, out var elastane);
        if (cottonOk && elastaneOk && cotton + elastane != 100m)
            errors.Add("Pamuk ve elastan yüzdeleri toplamı %100 olmalıdır.");

        if (!decimal.TryParse(values[6],
                NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture, out var carbon))
            errors.Add("Karbon değeri sayısal olmalıdır (örnek: 12.4).");
        else if (carbon < 0m)
            errors.Add("Karbon değeri negatif olamaz.");

        if (errors.Count > 0) return (null, errors);
        return (new ProductCsvRow(gtin, values[1], values[2], cotton, elastane,
            values[5], carbon), errors);
    }

    private static bool TryPercent(string input, string name, List<string> errors,
        out decimal value)
    {
        if (!decimal.TryParse(input,
                NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture, out value))
        {
            errors.Add($"{name} sayısal olmalıdır.");
            return false;
        }
        if (value is < 0m or > 100m)
        {
            errors.Add($"{name} 0–100 aralığında olmalıdır.");
            return false;
        }
        return true;
    }
}
