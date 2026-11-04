namespace MiniDPP.Web.Models;

public sealed class ProductModel
{
    public int Id { get; set; }
    public string Gtin { get; set; } = "";
    public string Name { get; set; } = "";
    public decimal CottonPercent { get; set; }
    public decimal ElastanePercent { get; set; }
    public int CurrentVersion { get; set; } = 1;

    public List<ProductBatch> Batches { get; set; } = [];
    public List<ProductVersion> Versions { get; set; } = [];
}
