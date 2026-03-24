namespace Children_s_toy_shop_management_software.Models.Inventory;

public sealed class StockMovementListVm
{
    public int MovementId { get; set; }
    public DateTime CreatedAt { get; set; }
    public string MovementType { get; set; } = "IN"; // IN/OUT
    public int LineCount { get; set; }
    public int TotalQty { get; set; }
    public string? Note { get; set; }
    public string? CreatedBy { get; set; }
    public bool AffectsStock { get; set; } = true;
}

