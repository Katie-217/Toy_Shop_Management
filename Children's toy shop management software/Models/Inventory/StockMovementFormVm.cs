namespace Children_s_toy_shop_management_software.Models.Inventory;

public sealed class StockMovementFormVm
{
    public int? MovementId { get; set; }
    public string MovementType { get; set; } = "IN";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public string? Note { get; set; }

    public bool AffectsStock { get; set; } = true;

    public List<StockMovementLineVm> Lines { get; set; } = new();
}

