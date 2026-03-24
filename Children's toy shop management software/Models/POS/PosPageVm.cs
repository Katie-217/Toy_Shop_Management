namespace Children_s_toy_shop_management_software.Models.POS;

public sealed class PosPageVm
{
    public string CurrentTabId { get; set; } = "default";
    public string? Search { get; set; }
    public List<PosProductsItemVm> Products { get; set; } = new();
    public PosCartVm Cart { get; set; } = new();
    public string? Error { get; set; }
}

