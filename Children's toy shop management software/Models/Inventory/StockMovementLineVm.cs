namespace Children_s_toy_shop_management_software.Models.Inventory;

public sealed class StockMovementLineVm
{
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}

