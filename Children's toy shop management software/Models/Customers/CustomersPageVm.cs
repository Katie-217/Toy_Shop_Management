namespace Children_s_toy_shop_management_software.Models.Customers;

public sealed class CustomersPageVm
{
    public string? Search { get; set; }

    public List<CustomerVm> Customers { get; set; } = new();
    public CustomerVm? SelectedCustomer { get; set; }

    public List<CustomerOrderVm> Orders { get; set; } = new();
}

