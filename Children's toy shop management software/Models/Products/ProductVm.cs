namespace Children_s_toy_shop_management_software.Models.Products;

public sealed class ProductVm
{
    public int Id { get; set; }
    public string Barcode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string AgeRange { get; set; } = string.Empty;
    public decimal ImportPrice { get; set; }
    public decimal SellPrice { get; set; }
    public int Quantity { get; set; }
    public string? ImagePath { get; set; }
    public string? BarcodeImagePath { get; set; }
    public bool IsActive { get; set; }
}

