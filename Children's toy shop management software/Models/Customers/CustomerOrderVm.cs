namespace Children_s_toy_shop_management_software.Models.Customers;

public sealed class CustomerOrderVm
{
    public int OrderId { get; set; }
    public DateTime OrderDate { get; set; }
    public decimal TotalAmount { get; set; }
}

