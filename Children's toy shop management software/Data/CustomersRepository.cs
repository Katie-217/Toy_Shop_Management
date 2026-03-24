using System.Data;
using Children_s_toy_shop_management_software.Models.Customers;
using Microsoft.Data.SqlClient;

namespace Children_s_toy_shop_management_software.Data;

public sealed class CustomersRepository(IDbConnectionFactory db)
{
    public async Task<List<CustomerVm>> GetCustomersAsync(string? search)
    {
        var q = string.IsNullOrWhiteSpace(search) ? "" : search.Trim().ToLowerInvariant();

        const string sql = @"
SELECT CustomerID, FullName, PhoneNumber, Address
FROM Customers
WHERE (@q = '' OR LOWER(FullName) LIKE @like OR LOWER(PhoneNumber) LIKE @like)
ORDER BY FullName";

        await using var conn = db.CreateConnection();
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@q", q);
        cmd.Parameters.AddWithValue("@like", $"%{q}%");

        await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection);
        var customers = new List<CustomerVm>();
        while (await reader.ReadAsync())
        {
            customers.Add(new CustomerVm
            {
                Id = reader.GetInt32(reader.GetOrdinal("CustomerID")),
                FullName = reader["FullName"]?.ToString() ?? "",
                Phone = reader["PhoneNumber"]?.ToString() ?? "",
                Address = reader["Address"]?.ToString() ?? "",
                TotalSpent = 0,
                Points = 0
            });
        }

        // Compute TotalSpent per WinForms behavior.
        foreach (var c in customers)
        {
            c.TotalSpent = await GetTotalSpentAsync(c.Id);
            c.Points = Math.Round(c.TotalSpent * 0.1m, 0);
        }

        return customers;
    }

    public async Task<CustomerVm?> GetByIdAsync(int id)
    {
        const string sql = @"
SELECT CustomerID, FullName, PhoneNumber, Address
FROM Customers
WHERE CustomerID = @id";

        await using var conn = db.CreateConnection();
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@id", id);

        await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection);
        if (!await reader.ReadAsync()) return null;

        var totalSpent = await GetTotalSpentAsync(id);
        return new CustomerVm
        {
            Id = reader.GetInt32(reader.GetOrdinal("CustomerID")),
            FullName = reader["FullName"]?.ToString() ?? "",
            Phone = reader["PhoneNumber"]?.ToString() ?? "",
            Address = reader["Address"]?.ToString() ?? "",
            TotalSpent = totalSpent,
            Points = Math.Round(totalSpent * 0.1m, 0)
        };
    }

    public async Task<List<CustomerOrderVm>> GetOrdersAsync(int customerId)
    {
        const string sql = @"
SELECT OrderID, OrderDate, TotalAmount
FROM Orders
WHERE CustomerID = @cid
ORDER BY OrderDate DESC";

        await using var conn = db.CreateConnection();
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@cid", customerId);

        await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection);
        var orders = new List<CustomerOrderVm>();
        while (await reader.ReadAsync())
        {
            orders.Add(new CustomerOrderVm
            {
                OrderId = reader.GetInt32(reader.GetOrdinal("OrderID")),
                OrderDate = reader["OrderDate"] == DBNull.Value ? DateTime.Today : Convert.ToDateTime(reader["OrderDate"]),
                TotalAmount = reader["TotalAmount"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["TotalAmount"])
            });
        }

        return orders;
    }

    private async Task<decimal> GetTotalSpentAsync(int customerId)
    {
        const string sql = @"SELECT ISNULL(SUM(TotalAmount),0) FROM Orders WHERE CustomerID=@CustomerID";
        await using var conn = db.CreateConnection();
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@CustomerID", customerId);

        var obj = await cmd.ExecuteScalarAsync();
        return obj == null || obj == DBNull.Value ? 0 : Convert.ToDecimal(obj);
    }
}

