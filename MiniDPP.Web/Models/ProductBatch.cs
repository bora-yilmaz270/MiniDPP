namespace MiniDPP.Web.Models;

public sealed class ProductBatch
{
    public int Id { get; set; }
    public int ProductModelId { get; set; }
    public ProductModel ProductModel { get; set; } = null!;
    public string BatchNo { get; set; } = "";
    public string ProductionPlace { get; set; } = "";
    public decimal CarbonKgCo2e { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
