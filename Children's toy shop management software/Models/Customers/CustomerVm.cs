namespace Children_s_toy_shop_management_software.Models.Customers;

public sealed class CustomerVm
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public decimal TotalSpent { get; set; }
    public decimal Points { get; set; }
}

