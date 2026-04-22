namespace Children_s_toy_shop_management_software.Models.Account;

public sealed class UserVm
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty; // "Admin" or "Cashier"
    public int? EmployeeId { get; set; }
}
