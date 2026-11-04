namespace MiniDPP.Web.Models;

// Optional assessment bonus: snapshots preserve the PREVIOUS product state.
public sealed class ProductVersion
{
    public int Id { get; set; }
    public int ProductModelId { get; set; }
    public ProductModel ProductModel { get; set; } = null!;
    public int VersionNumber { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public string SnapshotJson { get; set; } = "";
}
