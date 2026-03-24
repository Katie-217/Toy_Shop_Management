namespace Children_s_toy_shop_management_software.Models.Products;

public sealed class ProductsPageVm
{
    public string? Search { get; set; }
    public int? CategoryId { get; set; }
    public string? AgeRange { get; set; }

    public List<CategoryVm> Categories { get; set; } = new();
    public List<string> AgeRanges { get; set; } = new();

    public List<ProductVm> Products { get; set; } = new();
    public ProductVm? SelectedProduct { get; set; }

    public ProductFormVm Form { get; set; } = new();

    public string? Error { get; set; }
}

