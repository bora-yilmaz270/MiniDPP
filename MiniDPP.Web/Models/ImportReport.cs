namespace MiniDPP.Web.Models;

public sealed record CsvRowError(long Line, string Reason);

public sealed class ImportReport
{
    public int Created { get; set; }
    public int Updated { get; set; }
    public int Unchanged { get; set; }
    public List<CsvRowError> Errors { get; } = [];
    public int Rejected => Errors.Count;
    public string? GeneralError { get; set; }
}
