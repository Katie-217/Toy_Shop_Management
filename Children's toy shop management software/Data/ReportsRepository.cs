using System.Data;
using Children_s_toy_shop_management_software.Models.Reports;
using Microsoft.Data.SqlClient;

namespace Children_s_toy_shop_management_software.Data;

public sealed class ReportsRepository(IDbConnectionFactory db)
{
    public async Task<List<OrderVm>> GetOrdersAsync(DateTime from, DateTime to, string? search, string? status, string? paymentMethod)
    {
        var q = string.IsNullOrWhiteSpace(search) ? "" : search.Trim().ToLowerInvariant();
        var st = string.IsNullOrWhiteSpace(status) ? "" : status.Trim().ToLowerInvariant();
        var pm = string.IsNullOrWhiteSpace(paymentMethod) ? "" : paymentMethod.Trim().ToLowerInvariant();

        await using var conn = db.CreateConnection();
        await conn.OpenAsync();

        // Detect optional columns safely (some DBs don't have these fields yet).
        var hasStatusCol = false;
        var hasPaymentMethodCol = false;
        const string schemaSql = @"
SELECT
    CASE WHEN COL_LENGTH('Orders','Status') IS NULL THEN 0 ELSE 1 END AS HasStatus,
    CASE WHEN COL_LENGTH('Orders','PaymentMethod') IS NULL THEN 0 ELSE 1 END AS HasPaymentMethod;";
        await using (var schemaCmd = new SqlCommand(schemaSql, conn))
        await using (var schemaReader = await schemaCmd.ExecuteReaderAsync())
        {
            if (await schemaReader.ReadAsync())
            {
                hasStatusCol = Convert.ToInt32(schemaReader["HasStatus"]) == 1;
                hasPaymentMethodCol = Convert.ToInt32(schemaReader["HasPaymentMethod"]) == 1;
            }
        }

        var statusSelect = hasStatusCol
            ? "ISNULL(CONVERT(nvarchar(50), o.Status), N'') AS Status"
            : "N'' AS Status";
        var paymentMethodSelect = hasPaymentMethodCol
            ? "ISNULL(CONVERT(nvarchar(50), o.PaymentMethod), N'') AS PaymentMethod"
            : "N'' AS PaymentMethod";
        var statusWhere = hasStatusCol
            ? "AND (@st = '' OR LOWER(ISNULL(CONVERT(nvarchar(50), o.Status), N'')) = @st)"
            : "";
        var paymentWhere = hasPaymentMethodCol
            ? "AND (@pm = '' OR LOWER(ISNULL(CONVERT(nvarchar(50), o.PaymentMethod), N'')) = @pm)"
            : "";

        var sql = $@"
SELECT
    o.OrderID,
    o.OrderDate,
    o.UserID,
    u.FullName AS UserName,
    o.CustomerID,
    ISNULL(c.FullName,'') AS CustomerName,
    ISNULL(c.PhoneNumber,'') AS CustomerPhone,
    o.TotalAmount,
    {statusSelect},
    {paymentMethodSelect}
FROM Orders o
LEFT JOIN Users u ON o.UserID = u.UserID
LEFT JOIN Customers c ON o.CustomerID = c.CustomerID
WHERE
    o.OrderDate >= @from AND o.OrderDate <= @to
    AND (
        @q = ''
        OR CONVERT(varchar(20), o.OrderID) LIKE @like
        OR LOWER(ISNULL(c.FullName,'')) LIKE @like
        OR LOWER(ISNULL(c.PhoneNumber,'')) LIKE @like
    )
    {statusWhere}
    {paymentWhere}
ORDER BY o.OrderDate DESC";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@from", from);
        cmd.Parameters.AddWithValue("@to", to);
        cmd.Parameters.AddWithValue("@q", q);
        cmd.Parameters.AddWithValue("@like", $"%{q}%");
        cmd.Parameters.AddWithValue("@st", st);
        cmd.Parameters.AddWithValue("@pm", pm);

        await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection);
        var orders = new List<OrderVm>();
        while (await reader.ReadAsync())
        {
            orders.Add(new OrderVm
            {
                OrderId = reader.GetInt32(reader.GetOrdinal("OrderID")),
                OrderDate = reader["OrderDate"] == DBNull.Value ? DateTime.Today : Convert.ToDateTime(reader["OrderDate"]),
                UserId = reader.GetInt32(reader.GetOrdinal("UserID")),
                UserName = reader["UserName"]?.ToString() ?? "",
                CustomerId = reader["CustomerID"] == DBNull.Value ? null : Convert.ToInt32(reader["CustomerID"]),
                CustomerName = reader["CustomerName"]?.ToString() ?? "",
                CustomerPhone = reader["CustomerPhone"]?.ToString() ?? "",
                TotalAmount = reader["TotalAmount"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["TotalAmount"]),
                Status = reader["Status"]?.ToString() ?? "",
                PaymentMethod = reader["PaymentMethod"]?.ToString() ?? ""
            });
        }

        return orders;
    }

    public async Task<(OrderVm? order, List<OrderDetailVm> details)> GetOrderDetailsAsync(int orderId)
    {
        const string orderSql = @"
SELECT
    o.OrderID,
    o.OrderDate,
    o.UserID,
    u.FullName AS UserName,
    o.CustomerID,
    ISNULL(c.FullName,'') AS CustomerName,
    ISNULL(c.PhoneNumber,'') AS CustomerPhone,
    o.TotalAmount
FROM Orders o
LEFT JOIN Users u ON o.UserID = u.UserID
LEFT JOIN Customers c ON o.CustomerID = c.CustomerID
WHERE o.OrderID = @id";

        const string detailSql = @"
SELECT
    od.ProductID,
    ISNULL(p.Name,'') AS ProductName,
    od.Quantity,
    od.UnitPrice,
    (od.Quantity * od.UnitPrice) AS LineTotal
FROM OrderDetails od
LEFT JOIN Products p ON od.ProductID = p.ProductID
WHERE od.OrderID = @id";

        await using var conn = db.CreateConnection();
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(orderSql + ";", conn);
        cmd.Parameters.AddWithValue("@id", orderId);

        await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection);
        OrderVm? order = null;
        if (await reader.ReadAsync())
        {
            order = new OrderVm
            {
                OrderId = reader.GetInt32(reader.GetOrdinal("OrderID")),
                OrderDate = reader["OrderDate"] == DBNull.Value ? DateTime.Today : Convert.ToDateTime(reader["OrderDate"]),
                UserId = reader.GetInt32(reader.GetOrdinal("UserID")),
                UserName = reader["UserName"]?.ToString() ?? "",
                CustomerId = reader["CustomerID"] == DBNull.Value ? null : Convert.ToInt32(reader["CustomerID"]),
                CustomerName = reader["CustomerName"]?.ToString() ?? "",
                CustomerPhone = reader["CustomerPhone"]?.ToString() ?? "",
                TotalAmount = reader["TotalAmount"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["TotalAmount"])
            };
        }

        // second query
        await using var conn2 = db.CreateConnection();
        await conn2.OpenAsync();
        await using var cmd2 = new SqlCommand(detailSql, conn2);
        cmd2.Parameters.AddWithValue("@id", orderId);
        await using var reader2 = await cmd2.ExecuteReaderAsync(CommandBehavior.CloseConnection);

        var details = new List<OrderDetailVm>();
        while (await reader2.ReadAsync())
        {
            details.Add(new OrderDetailVm
            {
                ProductId = reader2.GetInt32(reader2.GetOrdinal("ProductID")),
                ProductName = reader2["ProductName"]?.ToString() ?? "",
                Quantity = reader2["Quantity"] == DBNull.Value ? 0 : Convert.ToInt32(reader2["Quantity"]),
                UnitPrice = reader2["UnitPrice"] == DBNull.Value ? 0 : Convert.ToDecimal(reader2["UnitPrice"]),
                LineTotal = reader2["LineTotal"] == DBNull.Value ? 0 : Convert.ToDecimal(reader2["LineTotal"])
            });
        }

        return (order, details);
    }
}

