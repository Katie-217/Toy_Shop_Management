namespace Children_s_toy_shop_management_software.Models.Staff;

public sealed class EmployeeVm
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Gender { get; set; } = "Male";
    public DateTime BirthDate { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string PositionTitle { get; set; } = string.Empty;
    public string ImagePath { get; set; } = string.Empty;
}

