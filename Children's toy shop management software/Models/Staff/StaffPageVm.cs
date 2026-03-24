namespace Children_s_toy_shop_management_software.Models.Staff;

public sealed class StaffPageVm
{
    public string? Search { get; set; }
    public int SortIndex { get; set; } = 0;

    public List<EmployeeVm> Employees { get; set; } = new();
    public EmployeeVm? SelectedEmployee { get; set; }

    public EmployeeFormVm Form { get; set; } = new();
    public string? Error { get; set; }
}

