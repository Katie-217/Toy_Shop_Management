using System.Data;
using Children_s_toy_shop_management_software.Models.POS;
using Microsoft.Data.SqlClient;

namespace Children_s_toy_shop_management_software.Data;

public sealed class PosRepository(IDbConnectionFactory db)
{
    public async Task<PosProductsItemVm?> GetProductByBarcodeAsync(string barcode)
    {
        if (string.IsNullOrWhiteSpace(barcode)) return null;

        const string sql = @"
SELECT
    p.Barcode,
    p.Name,
    p.RetailPrice,
    p.StockQuantity,
    p.ImagePath,
    ISNULL(c.Name,'') AS CategoryName,
    ISNULL(s.Name,'') AS SupplierName
FROM Products p
LEFT JOIN Categories c ON p.CategoryId = c.Id
LEFT JOIN Suppliers s ON p.SupplierId = s.Id
WHERE ISNULL(p.IsActive,1)=1 AND p.Barcode = @barcode";

        await using var conn = db.CreateConnection();
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@barcode", barcode.Trim());

        await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection);
        if (!await reader.ReadAsync()) return null;

        return MapProductRow(reader);
    }

    /// <summary>
    /// Finds a product from scanner/camera/USB input: exact barcode, digits-only, normalized (no dashes/spaces),
    /// then prefix/suffix match for common EAN/UPC length differences.
    /// </summary>
    public async Task<PosProductsItemVm?> FindProductByScanCodeAsync(string? scan)
    {
        if (string.IsNullOrWhiteSpace(scan)) return null;
        var raw = scan.Trim();
        if (raw.Length == 0) return null;

        // 1) Exact as stored
        var p = await GetProductByBarcodeAsync(raw);
        if (p != null) return p;

        // 2) Digits only (camera may include spaces or symbols)
        var digits = new string(raw.Where(char.IsDigit).ToArray());
        if (digits.Length > 0 && !string.Equals(digits, raw, StringComparison.Ordinal))
        {
            p = await GetProductByBarcodeAsync(digits);
            if (p != null) return p;
        }

        // 3) Alphanumeric only (Code128, etc.)
        var alphanum = new string(raw.Where(char.IsLetterOrDigit).ToArray());
        if (alphanum.Length > 0 && !string.Equals(alphanum, raw, StringComparison.OrdinalIgnoreCase))
        {
            p = await GetProductByBarcodeAsync(alphanum);
            if (p != null) return p;
        }

        var key = digits.Length > 0 ? digits : alphanum.Length > 0 ? alphanum : raw;

        // 4) Match DB barcode with dashes/spaces stripped
        p = await FindProductByNormalizedBarcodeKeyAsync(key);
        if (p != null) return p;

        // 5) Prefix / superset (e.g. 12 vs 13 digit EAN, missing check digit)
        if (key.Length >= 4)
        {
            p = await FindProductByBarcodePrefixOrSupersetAsync(key);
        }

        return p;
    }

    private async Task<PosProductsItemVm?> FindProductByNormalizedBarcodeKeyAsync(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;

        const string sql = @"
SELECT TOP 1
    p.Barcode,
    p.Name,
    p.RetailPrice,
    p.StockQuantity,
    p.ImagePath,
    ISNULL(c.Name,'') AS CategoryName,
    ISNULL(s.Name,'') AS SupplierName
FROM Products p
LEFT JOIN Categories c ON p.CategoryId = c.Id
LEFT JOIN Suppliers s ON p.SupplierId = s.Id
WHERE ISNULL(p.IsActive,1)=1
AND REPLACE(REPLACE(LTRIM(RTRIM(p.Barcode)),N'-',N''),N' ',N'') = @key";

        await using var conn = db.CreateConnection();
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@key", key.Trim());

        await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection);
        if (!await reader.ReadAsync()) return null;
        return MapProductRow(reader);
    }

    private async Task<PosProductsItemVm?> FindProductByBarcodePrefixOrSupersetAsync(string key)
    {
        const string sql = @"
SELECT TOP 1
    p.Barcode,
    p.Name,
    p.RetailPrice,
    p.StockQuantity,
    p.ImagePath,
    ISNULL(c.Name,'') AS CategoryName,
    ISNULL(s.Name,'') AS SupplierName
FROM Products p
LEFT JOIN Categories c ON p.CategoryId = c.Id
LEFT JOIN Suppliers s ON p.SupplierId = s.Id
WHERE ISNULL(p.IsActive,1)=1
AND (
    (LEN(@key) >= 4 AND p.Barcode LIKE @key + N'%')
    OR (LEN(@key) >= 4 AND @key LIKE p.Barcode + N'%')
    OR (LEN(@key) >= 4 AND REPLACE(REPLACE(LTRIM(RTRIM(p.Barcode)),N'-',N''),N' ',N'') LIKE @key + N'%')
    OR (LEN(@key) >= 4 AND @key LIKE REPLACE(REPLACE(LTRIM(RTRIM(p.Barcode)),N'-',N''),N' ',N'') + N'%')
)
ORDER BY
    CASE WHEN REPLACE(REPLACE(LTRIM(RTRIM(p.Barcode)),N'-',N''),N' ',N'') = @key THEN 0 ELSE 1 END,
    CASE WHEN p.Barcode = @key THEN 0 ELSE 1 END,
    ABS(LEN(REPLACE(REPLACE(LTRIM(RTRIM(p.Barcode)),N'-',N''),N' ',N'')) - LEN(@key)),
    LEN(p.Barcode)";

        await using var conn = db.CreateConnection();
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@key", key.Trim());

        await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection);
        if (!await reader.ReadAsync()) return null;
        return MapProductRow(reader);
    }

    private static PosProductsItemVm MapProductRow(SqlDataReader reader)
    {
        return new PosProductsItemVm
        {
            Code = reader["Barcode"]?.ToString() ?? "",
            Name = reader["Name"]?.ToString() ?? "",
            Price = reader["RetailPrice"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["RetailPrice"]),
            StockQuantity = reader["StockQuantity"] == DBNull.Value ? 0 : Convert.ToInt32(reader["StockQuantity"]),
            ImagePath = reader["ImagePath"] == DBNull.Value ? null : reader["ImagePath"]?.ToString(),
            Category = reader["CategoryName"]?.ToString() ?? "",
            Supplier = reader["SupplierName"]?.ToString() ?? ""
        };
    }

    /// <summary>Escape %, _, [ for SQL LIKE pattern.</summary>
    private static string EscapeSqlLike(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        return s.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]");
    }

    /// <summary>
    /// Tìm sản phẩm POS — cùng logic với WinForms <c>UcPos.SearchProducts</c>:
    /// từ khóa khớp Barcode hoặc Name (LIKE %%), tùy chọn lọc đúng tên Category / Supplier.
    /// </summary>
    public Task<List<PosProductsItemVm>> SearchProductsAsync(string? keyword)
        => SearchProductsAsync(keyword, null, null);

    public async Task<List<PosProductsItemVm>> SearchProductsAsync(string? keyword, string? categoryName, string? supplierName)
    {
        var q = string.IsNullOrWhiteSpace(keyword) ? "" : keyword.Trim();
        var like = q.Length == 0 ? "" : "%" + EscapeSqlLike(q) + "%";
        var cat = string.IsNullOrWhiteSpace(categoryName) ? null : categoryName.Trim();
        var sup = string.IsNullOrWhiteSpace(supplierName) ? null : supplierName.Trim();

        var sql = @"
SELECT
    p.Barcode,
    p.Name,
    p.RetailPrice,
    p.StockQuantity,
    p.ImagePath,
    ISNULL(c.Name,'') AS CategoryName,
    ISNULL(s.Name,'') AS SupplierName
FROM Products p
LEFT JOIN Categories c ON p.CategoryId = c.Id
LEFT JOIN Suppliers s ON p.SupplierId = s.Id
WHERE ISNULL(p.IsActive, 1) = 1
  AND (@q = N'' OR LOWER(p.Barcode) LIKE LOWER(@like) OR LOWER(p.Name) LIKE LOWER(@like))
  AND (@cat IS NULL OR c.Name = @cat)
  AND (@sup IS NULL OR s.Name = @sup)
ORDER BY p.Name";

        await using var conn = db.CreateConnection();
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@q", q);
        cmd.Parameters.AddWithValue("@like", like);
        cmd.Parameters.AddWithValue("@cat", (object?)cat ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@sup", (object?)sup ?? DBNull.Value);

        await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection);
        var list = new List<PosProductsItemVm>();
        while (await reader.ReadAsync())
        {
            list.Add(MapProductRow(reader));
        }
        return list;
    }

    public async Task<(int customerId, string fullName, int points)?> FindMemberCustomerByPhoneAsync(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone) || phone.Trim().Length < 3)
        {
            return null;
        }

        const string sql = @"
SELECT TOP 1 CustomerID, Fullname, Points
FROM Customers
WHERE PhoneNumber = @Phone AND IsMember = 1";

        await using var conn = db.CreateConnection();
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Phone", phone.Trim());

        await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection);
        if (!await reader.ReadAsync()) return null;

        return (
            reader.GetInt32(reader.GetOrdinal("CustomerID")),
            reader["Fullname"]?.ToString() ?? "",
            reader["Points"] == DBNull.Value ? 0 : Convert.ToInt32(reader["Points"])
        );
    }

    public async Task<int> SaveOrderAsync(
        IEnumerable<PosCartLineVm> cart,
        int userId,
        int? customerId,
        decimal grandTotal)
    {
        var items = cart?.Where(x => x != null && x.Qty > 0).ToList() ?? new List<PosCartLineVm>();
        if (items.Count == 0) throw new InvalidOperationException("Cart is empty.");

        await using var conn = db.CreateConnection();
        await conn.OpenAsync();
        using var tx = conn.BeginTransaction();

        try
        {
            const string insertOrderSql = @"
INSERT INTO Orders (OrderDate, UserID, CustomerID, TotalAmount)
VALUES (@OrderDate, @UserID, @CustomerID, @TotalAmount);
SELECT CAST(SCOPE_IDENTITY() AS int);";

            await using var cmdOrder = new SqlCommand(insertOrderSql, conn, tx);
            cmdOrder.Parameters.AddWithValue("@OrderDate", DateTime.Now);
            cmdOrder.Parameters.AddWithValue("@UserID", userId);
            cmdOrder.Parameters.AddWithValue("@CustomerID", customerId.HasValue ? (object)customerId.Value : DBNull.Value);
            cmdOrder.Parameters.AddWithValue("@TotalAmount", grandTotal);
            var idObj = await cmdOrder.ExecuteScalarAsync();
            var orderId = idObj == null || idObj == DBNull.Value ? 0 : Convert.ToInt32(idObj);
            if (orderId <= 0) throw new InvalidOperationException("Could not create order.");

            foreach (var item in items)
            {
                // Resolve ProductID by barcode.
                const string resolveSql = @"SELECT TOP 1 ProductID FROM Products WHERE Barcode = @Barcode";
                await using var resolveCmd = new SqlCommand(resolveSql, conn, tx);
                resolveCmd.Parameters.AddWithValue("@Barcode", item.Code);
                var pidObj = await resolveCmd.ExecuteScalarAsync();
                var productId = pidObj == null || pidObj == DBNull.Value ? 0 : Convert.ToInt32(pidObj);
                if (productId <= 0) throw new InvalidOperationException($"Product not found for barcode '{item.Code}'.");

                // Check stock.
                const string stockSql = @"SELECT StockQuantity FROM Products WHERE ProductID = @Id";
                await using var stockCmd = new SqlCommand(stockSql, conn, tx);
                stockCmd.Parameters.AddWithValue("@Id", productId);
                var stockObj = await stockCmd.ExecuteScalarAsync();
                var stock = stockObj == null || stockObj == DBNull.Value ? 0 : Convert.ToInt32(stockObj);
                if (stock < item.Qty)
                    throw new InvalidOperationException($"Insufficient stock for '{item.Name}'. Stock={stock}, Qty={item.Qty}");

                const string insertDetailSql = @"
INSERT INTO OrderDetails (OrderID, ProductID, Quantity, UnitPrice)
VALUES (@OrderID, @ProductID, @Quantity, @UnitPrice);";
                await using var detailCmd = new SqlCommand(insertDetailSql, conn, tx);
                detailCmd.Parameters.AddWithValue("@OrderID", orderId);
                detailCmd.Parameters.AddWithValue("@ProductID", productId);
                detailCmd.Parameters.AddWithValue("@Quantity", item.Qty);
                detailCmd.Parameters.AddWithValue("@UnitPrice", item.Price);
                await detailCmd.ExecuteNonQueryAsync();

                const string updateStockSql = @"UPDATE Products SET StockQuantity = StockQuantity - @Qty WHERE ProductID = @Id";
                await using var updateCmd = new SqlCommand(updateStockSql, conn, tx);
                updateCmd.Parameters.AddWithValue("@Qty", item.Qty);
                updateCmd.Parameters.AddWithValue("@Id", productId);
                await updateCmd.ExecuteNonQueryAsync();
            }

            tx.Commit();
            return orderId;
        }
        catch
        {
            try { tx.Rollback(); } catch { }
            throw;
        }
    }

    public async Task<PosReceiptVm?> GetReceiptAsync(int orderId)
    {
        const string orderSql = @"
SELECT
    o.OrderID,
    o.OrderDate,
    c.Fullname AS CustomerName,
    c.PhoneNumber AS CustomerPhone,
    o.TotalAmount
FROM Orders o
LEFT JOIN Customers c ON o.CustomerID = c.CustomerID
WHERE o.OrderID = @id";

        const string detailsSql = @"
SELECT
    od.ProductID,
    ISNULL(p.Barcode,'') AS ProductCode,
    ISNULL(p.Name,'') AS ProductName,
    od.Quantity,
    od.UnitPrice,
    (od.Quantity * od.UnitPrice) AS LineTotal
FROM OrderDetails od
LEFT JOIN Products p ON od.ProductID = p.ProductID
WHERE od.OrderID = @id";

        await using var conn = db.CreateConnection();
        await conn.OpenAsync();

        await using var cmd = new SqlCommand(orderSql, conn);
        cmd.Parameters.AddWithValue("@id", orderId);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;

        var receipt = new PosReceiptVm
        {
            OrderId = reader.GetInt32(reader.GetOrdinal("OrderID")),
            OrderDate = reader["OrderDate"] == DBNull.Value ? DateTime.Today : Convert.ToDateTime(reader["OrderDate"]),
            CustomerName = reader["CustomerName"]?.ToString(),
            CustomerPhone = reader["CustomerPhone"]?.ToString(),
            GrandTotal = reader["TotalAmount"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["TotalAmount"])
        };

        // second query for lines + compute subtotal/discount is not stored in DB currently,
        // so we compute subtotal from lines and set Discount = subtotal - grand total.
        await using var cmd2 = new SqlCommand(detailsSql, conn);
        cmd2.Parameters.AddWithValue("@id", orderId);
        await using var reader2 = await cmd2.ExecuteReaderAsync();

        decimal subtotal = 0m;
        while (await reader2.ReadAsync())
        {
            var qty = reader2["Quantity"] == DBNull.Value ? 0 : Convert.ToInt32(reader2["Quantity"]);
            var unit = reader2["UnitPrice"] == DBNull.Value ? 0 : Convert.ToDecimal(reader2["UnitPrice"]);
            var lineTotal = reader2["LineTotal"] == DBNull.Value ? 0 : Convert.ToDecimal(reader2["LineTotal"]);

            receipt.Lines.Add(new PosReceiptLineVm
            {
                Code = reader2["ProductCode"]?.ToString() ?? "",
                Name = reader2["ProductName"]?.ToString() ?? "",
                Qty = qty,
                UnitPrice = unit,
                LineTotal = lineTotal
            });
            subtotal += lineTotal;
        }

        receipt.SubTotal = subtotal;
        receipt.Discount = subtotal - receipt.GrandTotal;
        if (receipt.Discount < 0) receipt.Discount = 0;

        return receipt;
    }
}

