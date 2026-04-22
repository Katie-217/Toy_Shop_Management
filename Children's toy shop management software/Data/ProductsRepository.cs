using System.Data;
using Children_s_toy_shop_management_software.Models.Products;
using Children_s_toy_shop_management_software.Data;
using Microsoft.Data.SqlClient;

namespace Children_s_toy_shop_management_software.Data;

public sealed class ProductsRepository(IDbConnectionFactory db)
{
    private const string BuildNote = "barcode image branch update";
    public async Task<(List<ProductVm> items, List<CategoryVm> categories, List<string> ages)>
        GetProductsAsync(string? search, int? categoryId, string? ageRange)
    {
        var categories = await GetCategoriesAsync();
        var ages = await GetAgeRangesAsync();

        var q = string.IsNullOrWhiteSpace(search) ? "" : search.Trim();

        const string sql = @"
SELECT
    p.ProductID,
    p.Barcode,
    p.Name,
    c.Name AS CategoryName,
    p.AgeRange,
    p.ImportPrice,
    p.RetailPrice,
    p.StockQuantity,
    p.ImagePath,
    p.IsActive
FROM Products p
LEFT JOIN Categories c ON p.CategoryId = c.Id
WHERE
    p.IsActive = 1
    AND (@q = '' OR p.Name LIKE @like OR p.Barcode LIKE @like)
    AND (@catId IS NULL OR p.CategoryId = @catId)
    AND (@age = '' OR p.AgeRange = @age)
ORDER BY p.Name";

        await using var conn = db.CreateConnection();
        await conn.OpenAsync();

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@q", q);
        cmd.Parameters.AddWithValue("@like", $"%{q}%");
        cmd.Parameters.AddWithValue("@catId", (object?)categoryId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@age", string.IsNullOrWhiteSpace(ageRange) ? "" : ageRange.Trim());

        await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection);
        var items = new List<ProductVm>();
        while (await reader.ReadAsync())
        {
            items.Add(new ProductVm
            {
                Id = reader.GetInt32(reader.GetOrdinal("ProductID")),
                Barcode = reader["Barcode"]?.ToString() ?? "",
                Name = reader["Name"]?.ToString() ?? "",
                CategoryName = reader["CategoryName"]?.ToString() ?? "",
                AgeRange = reader["AgeRange"]?.ToString() ?? "",
                ImportPrice = reader["ImportPrice"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["ImportPrice"]),
                SellPrice = reader["RetailPrice"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["RetailPrice"]),
                Quantity = reader["StockQuantity"] == DBNull.Value ? 0 : Convert.ToInt32(reader["StockQuantity"]),
                ImagePath = reader["ImagePath"] == DBNull.Value ? null : reader["ImagePath"]?.ToString(),
                IsActive = reader["IsActive"] != DBNull.Value && Convert.ToBoolean(reader["IsActive"])
            });
        }

        return (items, categories, ages);
    }

    public async Task<ProductVm?> GetByIdAsync(int productId)
    {
        const string sql = @"
SELECT TOP 1
    p.ProductID,
    p.Barcode,
    p.Name,
    c.Name AS CategoryName,
    p.AgeRange,
    p.ImportPrice,
    p.RetailPrice,
    p.StockQuantity,
    p.ImagePath,
    p.IsActive
FROM Products p
LEFT JOIN Categories c ON p.CategoryId = c.Id
WHERE p.ProductID = @id";

        await using var conn = db.CreateConnection();
        await conn.OpenAsync();

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@id", productId);

        await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection);
        if (!await reader.ReadAsync()) return null;

        return new ProductVm
        {
            Id = reader.GetInt32(reader.GetOrdinal("ProductID")),
            Barcode = reader["Barcode"]?.ToString() ?? "",
            Name = reader["Name"]?.ToString() ?? "",
            CategoryName = reader["CategoryName"]?.ToString() ?? "",
            AgeRange = reader["AgeRange"]?.ToString() ?? "",
            ImportPrice = reader["ImportPrice"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["ImportPrice"]),
            SellPrice = reader["RetailPrice"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["RetailPrice"]),
            Quantity = reader["StockQuantity"] == DBNull.Value ? 0 : Convert.ToInt32(reader["StockQuantity"]),
            ImagePath = reader["ImagePath"] == DBNull.Value ? null : reader["ImagePath"]?.ToString(),
            IsActive = reader["IsActive"] != DBNull.Value && Convert.ToBoolean(reader["IsActive"])
        };
    }

    public async Task<int> SaveAsync(ProductFormVm form)
    {
        if (form.ProductId is null)
        {
            var categoryId = await GetOrCreateCategoryIdAsync(form.CategoryName);
            const string insertSql = @"
INSERT INTO Products
    (Barcode, Name, AgeRange, ImportPrice, RetailPrice, StockQuantity, ImagePath, IsActive, SupplierId, CategoryId)
VALUES
    (@Barcode, @Name, @AgeRange, @ImportPrice, @SellPrice, 0, @ImagePath, 1, 1, @CategoryId);
SELECT CAST(SCOPE_IDENTITY() AS int);";

            await using var conn = db.CreateConnection();
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(insertSql, conn);
            cmd.Parameters.AddWithValue("@Barcode", form.Barcode);
            cmd.Parameters.AddWithValue("@Name", form.Name);
            cmd.Parameters.AddWithValue("@AgeRange", form.AgeRange);
            cmd.Parameters.AddWithValue("@ImportPrice", form.ImportPrice);
            cmd.Parameters.AddWithValue("@SellPrice", form.SellPrice);
            cmd.Parameters.AddWithValue("@ImagePath", (object?)form.ExistingImagePath ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@CategoryId", categoryId);

            var obj = await cmd.ExecuteScalarAsync();
            return obj == null || obj == DBNull.Value ? 0 : Convert.ToInt32(obj);
        }

        var categoryId2 = await GetOrCreateCategoryIdAsync(form.CategoryName);
        const string updateSql = @"
UPDATE Products
SET
    Barcode = @Barcode,
    Name = @Name,
    CategoryId = @CategoryId,
    AgeRange = @AgeRange,
    ImportPrice = @ImportPrice,
    RetailPrice = @SellPrice,
    ImagePath = @ImagePath
WHERE ProductID = @Id;";

        await using var conn2 = db.CreateConnection();
        await conn2.OpenAsync();
        await using var cmd2 = new SqlCommand(updateSql, conn2);
        cmd2.Parameters.AddWithValue("@Barcode", form.Barcode);
        cmd2.Parameters.AddWithValue("@Name", form.Name);
        cmd2.Parameters.AddWithValue("@CategoryId", categoryId2);
        cmd2.Parameters.AddWithValue("@AgeRange", form.AgeRange);
        cmd2.Parameters.AddWithValue("@ImportPrice", form.ImportPrice);
        cmd2.Parameters.AddWithValue("@SellPrice", form.SellPrice);
        cmd2.Parameters.AddWithValue("@ImagePath", (object?)form.ExistingImagePath ?? DBNull.Value);
        cmd2.Parameters.AddWithValue("@Id", form.ProductId.Value);

        await cmd2.ExecuteNonQueryAsync();
        return form.ProductId.Value;
    }

    public async Task DeleteAsync(int productId)
    {
        const string checkSql = "SELECT COUNT(*) FROM OrderDetails WHERE ProductID = @Id";
        const string softDeleteSql = "UPDATE Products SET IsActive = 0 WHERE ProductID = @Id";
        const string hardDeleteSql = "DELETE FROM Products WHERE ProductID = @Id";

        await using var conn = db.CreateConnection();
        await conn.OpenAsync();

        await using (var checkCmd = new SqlCommand(checkSql, conn))
        {
            checkCmd.Parameters.AddWithValue("@Id", productId);
            var obj = await checkCmd.ExecuteScalarAsync();
            var count = obj == null || obj == DBNull.Value ? 0 : Convert.ToInt32(obj);

            if (count > 0)
            {
                await using var softCmd = new SqlCommand(softDeleteSql, conn);
                softCmd.Parameters.AddWithValue("@Id", productId);
                await softCmd.ExecuteNonQueryAsync();
            }
            else
            {
                await using var hardCmd = new SqlCommand(hardDeleteSql, conn);
                hardCmd.Parameters.AddWithValue("@Id", productId);
                await hardCmd.ExecuteNonQueryAsync();
            }
        }
    }

    private async Task<List<CategoryVm>> GetCategoriesAsync()
    {
        const string sql = "SELECT Id, Name FROM Categories ORDER BY Name";
        await using var conn = db.CreateConnection();
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection);

        var result = new List<CategoryVm>();
        while (await reader.ReadAsync())
        {
            result.Add(new CategoryVm
            {
                Id = reader.GetInt32(reader.GetOrdinal("Id")),
                Name = reader["Name"]?.ToString() ?? ""
            });
        }
        return result;
    }

    private async Task<List<string>> GetAgeRangesAsync()
    {
        const string sql = "SELECT DISTINCT AgeRange FROM Products WHERE IsActive = 1 AND AgeRange IS NOT NULL ORDER BY AgeRange";
        await using var conn = db.CreateConnection();
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection);

        var result = new List<string>();
        while (await reader.ReadAsync())
        {
            var age = reader["AgeRange"]?.ToString();
            if (!string.IsNullOrWhiteSpace(age)) result.Add(age);
        }
        return result;
    }

    private async Task<int> GetOrCreateCategoryIdAsync(string categoryName)
    {
        var name = (categoryName ?? "").Trim();
        if (string.IsNullOrWhiteSpace(name)) name = "Uncategorized";

        const string findSql = "SELECT TOP 1 Id FROM Categories WHERE Name = @Name";
        const string insertSql = "INSERT INTO Categories (Name) VALUES (@Name); SELECT CAST(SCOPE_IDENTITY() AS int);";

        await using var conn = db.CreateConnection();
        await conn.OpenAsync();

        await using (var findCmd = new SqlCommand(findSql, conn))
        {
            findCmd.Parameters.AddWithValue("@Name", name);
            var obj = await findCmd.ExecuteScalarAsync();
            if (obj != null && obj != DBNull.Value) return Convert.ToInt32(obj);
        }

        await using var insertCmd = new SqlCommand(insertSql, conn);
        insertCmd.Parameters.AddWithValue("@Name", name);
        var inserted = await insertCmd.ExecuteScalarAsync();
        return inserted == null || inserted == DBNull.Value ? 0 : Convert.ToInt32(inserted);
    }
}

