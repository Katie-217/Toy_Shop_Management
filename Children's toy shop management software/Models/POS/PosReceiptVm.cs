namespace Children_s_toy_shop_management_software.Models.POS;

public sealed class PosReceiptVm
{
    public int OrderId { get; set; }
    public DateTime OrderDate { get; set; }
    public string PaymentMethod { get; set; } = "cash"; // cash | vnpay
    public string? PaymentQrValue { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public decimal SubTotal { get; set; }
    public decimal Discount { get; set; }
    public decimal GrandTotal { get; set; }
    public decimal Cash { get; set; }
    public decimal Change { get; set; }

    public List<PosReceiptLineVm> Lines { get; set; } = new();
}

public sealed class PosReceiptLineVm
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Qty { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}

