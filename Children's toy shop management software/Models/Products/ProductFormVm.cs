using Microsoft.AspNetCore.Http;

namespace Children_s_toy_shop_management_software.Models.Products;

public sealed class ProductFormVm
{
    public int? ProductId { get; set; }

    public string Barcode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    // We keep this as a text field so the backend can auto-create categories (same behavior as WinForms).
    public string CategoryName { get; set; } = string.Empty;
    public string AgeRange { get; set; } = string.Empty;

    public decimal ImportPrice { get; set; }
    public decimal SellPrice { get; set; }

    public string? ExistingImagePath { get; set; }
    public IFormFile? ImageFile { get; set; }
}

