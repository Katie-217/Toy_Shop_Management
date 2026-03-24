namespace Children_s_toy_shop_management_software.Models.POS;

public sealed class PosCartVm
{
    public List<PosCartLineVm> Lines { get; set; } = new();
}

