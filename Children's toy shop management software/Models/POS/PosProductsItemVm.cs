namespace Children_s_toy_shop_management_software.Models.POS;

public sealed class PosProductsItemVm
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Supplier { get; set; } = string.Empty;
    public string? ImagePath { get; set; }
}

