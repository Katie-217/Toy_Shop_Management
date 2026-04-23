namespace Children_s_toy_shop_management_software.Models.Inventory;

public sealed class StockCheckVm
{
    public int ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Barcode { get; set; } = string.Empty;
    public int SystemStock { get; set; }
    public int SoldToday { get; set; }
}

public sealed class StockCheckPageVm
{
    public List<StockCheckVm> Items { get; set; } = new();
    public List<StockAuditHeaderVm> History { get; set; } = new();
    public StockAuditHeaderVm? SelectedAudit { get; set; }
    public List<StockAuditLineVm> SelectedLines { get; set; } = new();
    public DateTime Date { get; set; } = DateTime.Today;
}

public class StockAuditHeaderVm
{
    public int AuditId { get; set; }
    public DateTime AuditDate { get; set; }
    public string Note { get; set; } = "";
    public string CreatedByUserName { get; set; } = "";
    public int ItemCount { get; set; }
}

public class StockAuditLineVm
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public string Barcode { get; set; } = "";
    public int SystemQty { get; set; }
    public int PhysicalQty { get; set; }
    public int Difference { get; set; }
    public string Note { get; set; } = "";
}
