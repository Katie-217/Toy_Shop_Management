namespace Children_s_toy_shop_management_software.Models.POS;

public sealed class PosCartLineVm
{
    // Barcode (Code) in WinForms.
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Qty { get; set; }

    public decimal Amount => Price * Qty;
}

