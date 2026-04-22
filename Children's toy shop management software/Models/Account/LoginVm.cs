using System.ComponentModel.DataAnnotations;

namespace Children_s_toy_shop_management_software.Models.Account;

public sealed class LoginVm
{
    [Required(ErrorMessage = "Please enter username")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please enter password")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}
