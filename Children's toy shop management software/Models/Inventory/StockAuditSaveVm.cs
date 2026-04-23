namespace Children_s_toy_shop_management_software.Models.Inventory;

public class StockAuditSaveVm
{
    public string? Note { get; set; }
    public List<StockAuditLineSaveVm> Items { get; set; } = new();
}

public class StockAuditLineSaveVm
{
    public int ProductId { get; set; }
    public int SystemQty { get; set; }
    public int PhysicalQty { get; set; }
    public int Difference { get; set; }
    public string? Note { get; set; }
}
