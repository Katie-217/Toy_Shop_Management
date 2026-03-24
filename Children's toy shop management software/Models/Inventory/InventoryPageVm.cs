namespace Children_s_toy_shop_management_software.Models.Inventory;

public sealed class InventoryPageVm
{
    /// <summary>purchase = nhập hàng (IN), sales = bán hàng (OUT).</summary>
    public string Section { get; set; } = "purchase";

    public string? Q { get; set; }
    public string? TypeFilter { get; set; } // IN/OUT (theo Section)
    public DateTime From { get; set; } = DateTime.Today.AddDays(-30);
    public DateTime To { get; set; } = DateTime.Today;

    public List<StockMovementListVm> Movements { get; set; } = new();
    public StockMovementListVm? SelectedMovement { get; set; }
    public StockMovementFormVm Form { get; set; } = new();

    public List<(int ProductId, string Barcode, string Name, int StockQuantity)> Products { get; set; } = new();
    public string? Error { get; set; }
}

