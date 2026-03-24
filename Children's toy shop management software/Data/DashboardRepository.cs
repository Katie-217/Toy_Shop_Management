using Children_s_toy_shop_management_software.Models.Dashboard;
using Microsoft.Data.SqlClient;

namespace Children_s_toy_shop_management_software.Data;

public sealed class DashboardRepository(IDbConnectionFactory db)
{
    public async Task<DashboardPageVm> GetKpisAsync()
    {
        var today = DateTime.Today;
        var next = today.AddDays(1);

        const string sql = @"
SELECT
    ISNULL((SELECT SUM(TotalAmount) FROM Orders WHERE OrderDate >= @from AND OrderDate < @to), 0) AS RevenueToday,
    ISNULL((SELECT COUNT(*) FROM Orders WHERE OrderDate >= @from AND OrderDate < @to), 0) AS OrdersToday,
    ISNULL((SELECT COUNT(*) FROM Products WHERE StockQuantity <= @lowStock), 0) AS LowStockCount,
    ISNULL((SELECT COUNT(*) FROM Customers), 0) AS CustomersCount";

        await using var conn = db.CreateConnection();
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@from", today);
        cmd.Parameters.AddWithValue("@to", next);
        cmd.Parameters.AddWithValue("@lowStock", 5);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return new DashboardPageVm();
        }

        return new DashboardPageVm
        {
            RevenueToday = reader["RevenueToday"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["RevenueToday"]),
            OrdersToday = reader["OrdersToday"] == DBNull.Value ? 0 : Convert.ToInt32(reader["OrdersToday"]),
            LowStockCount = reader["LowStockCount"] == DBNull.Value ? 0 : Convert.ToInt32(reader["LowStockCount"]),
            CustomersCount = reader["CustomersCount"] == DBNull.Value ? 0 : Convert.ToInt32(reader["CustomersCount"])
        };
    }
}

