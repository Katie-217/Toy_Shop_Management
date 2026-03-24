namespace Children_s_toy_shop_management_software.Models.Reports;

public sealed class ReportsPageVm
{
    public DateTime From { get; set; } = DateTime.Today;
    public DateTime To { get; set; } = DateTime.Today;
    public string? Search { get; set; }
    public string? Status { get; set; }
    public string? PaymentMethod { get; set; }
    public List<OrderVm> Orders { get; set; } = new();
    public OrderVm? SelectedOrder { get; set; }
    public List<OrderDetailVm> OrderDetails { get; set; } = new();
}

