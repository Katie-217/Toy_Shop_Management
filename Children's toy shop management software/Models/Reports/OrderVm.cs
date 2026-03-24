namespace Children_s_toy_shop_management_software.Models.Reports;

public sealed class OrderVm
{
    public int OrderId { get; set; }
    public DateTime OrderDate { get; set; }
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public int? CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string PaymentMethod { get; set; } = string.Empty;
}

