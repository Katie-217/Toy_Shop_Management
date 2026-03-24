using System.Data;
using Children_s_toy_shop_management_software.Models.Inventory;
using Microsoft.Data.SqlClient;

namespace Children_s_toy_shop_management_software.Data;

public sealed class InventoryRepository(IDbConnectionFactory db)
{
    public async Task EnsureSchemaAsync()
    {
        // Create inventory tables if the WinForms app already created them, these will be no-ops.
        const string createHeaders = @"
IF OBJECT_ID('dbo.StockMovementHeaders', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.StockMovementHeaders(
        MovementID INT IDENTITY(1,1) PRIMARY KEY,
        MovementType NVARCHAR(10) NOT NULL,
        AffectsStock BIT NOT NULL,
        Note NVARCHAR(255) NULL,
        CreatedByUserID INT NULL,
        CreatedAt DATETIME NOT NULL DEFAULT(GETDATE())
    );
END";

        const string createLines = @"
IF OBJECT_ID('dbo.StockMovementLines', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.StockMovementLines(
        LineID INT IDENTITY(1,1) PRIMARY KEY,
        MovementID INT NOT NULL,
        ProductID INT NOT NULL,
        Quantity INT NOT NULL,
        UnitPrice DECIMAL(18,2) NOT NULL,
        CONSTRAINT FK_StockMovementLines_Headers FOREIGN KEY (MovementID)
            REFERENCES dbo.StockMovementHeaders(MovementID)
            ON DELETE CASCADE
    );
END";

        await using var conn = db.CreateConnection();
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(createHeaders + createLines, conn);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<StockMovementListVm>> GetMovementsAsync(DateTime from, DateTime to, string? typeFilter, string? q)
    {
        var qNorm = string.IsNullOrWhiteSpace(q) ? "" : q.Trim().ToLowerInvariant();
        var typeNorm = string.IsNullOrWhiteSpace(typeFilter) ? "ALL" : typeFilter.Trim().ToUpperInvariant();

        const string sql = @"
SELECT
    h.MovementID,
    h.CreatedAt,
    h.MovementType,
    ISNULL(h.Note,'') AS Note,
    h.AffectsStock,
    h.CreatedByUserID,
    ISNULL(SUM(l.Quantity),0) AS TotalQty,
    COUNT(l.LineID) AS LineCount
FROM StockMovementHeaders h
LEFT JOIN StockMovementLines l ON h.MovementID = l.MovementID
WHERE
    h.CreatedAt >= @from AND h.CreatedAt <= @to
    AND (@type = 'ALL' OR h.MovementType = @type)
    AND (
        @q = ''
        OR LOWER(ISNULL(h.Note,'')) LIKE @like
        OR CONVERT(VARCHAR(50), h.MovementID) LIKE @like
    )
GROUP BY
    h.MovementID, h.CreatedAt, h.MovementType, h.Note, h.AffectsStock, h.CreatedByUserID
ORDER BY h.CreatedAt DESC";

        await using var conn = db.CreateConnection();
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@from", from);
        cmd.Parameters.AddWithValue("@to", to);
        cmd.Parameters.AddWithValue("@type", typeNorm);
        cmd.Parameters.AddWithValue("@q", qNorm);
        cmd.Parameters.AddWithValue("@like", $"%{qNorm}%");

        await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection);
        var list = new List<StockMovementListVm>();
        while (await reader.ReadAsync())
        {
            list.Add(new StockMovementListVm
            {
                MovementId = reader.GetInt32(reader.GetOrdinal("MovementID")),
                CreatedAt = reader["CreatedAt"] == DBNull.Value ? DateTime.Today : Convert.ToDateTime(reader["CreatedAt"]),
                MovementType = reader["MovementType"]?.ToString() ?? "IN",
                Note = reader["Note"]?.ToString(),
                AffectsStock = reader["AffectsStock"] != DBNull.Value && Convert.ToBoolean(reader["AffectsStock"]),
                CreatedBy = reader["CreatedByUserID"] == DBNull.Value ? null : reader["CreatedByUserID"]!.ToString(),
                TotalQty = reader["TotalQty"] == DBNull.Value ? 0 : Convert.ToInt32(reader["TotalQty"]),
                LineCount = reader["LineCount"] == DBNull.Value ? 0 : Convert.ToInt32(reader["LineCount"])
            });
        }

        return list;
    }

    public async Task<(StockMovementListVm? header, List<StockMovementLineVm> lines)> GetMovementAsync(int movementId)
    {
        const string headerSql = @"
SELECT TOP 1
    MovementID,
    MovementType,
    AffectsStock,
    Note,
    CreatedByUserID,
    CreatedAt
FROM StockMovementHeaders
WHERE MovementID = @id";

        const string linesSql = @"
SELECT
    ProductID,
    Quantity,
    UnitPrice
FROM StockMovementLines
WHERE MovementID = @id
ORDER BY LineID";

        await using var conn = db.CreateConnection();
        await conn.OpenAsync();

        StockMovementListVm? header;
        {
            await using var cmd = new SqlCommand(headerSql, conn);
            cmd.Parameters.AddWithValue("@id", movementId);

            await using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                return (null, new List<StockMovementLineVm>());
            }

            header = new StockMovementListVm
            {
                MovementId = reader.GetInt32(reader.GetOrdinal("MovementID")),
                MovementType = reader["MovementType"]?.ToString() ?? "IN",
                AffectsStock = reader["AffectsStock"] != DBNull.Value && Convert.ToBoolean(reader["AffectsStock"]),
                Note = reader["Note"]?.ToString(),
                CreatedBy = reader["CreatedByUserID"] == DBNull.Value ? null : reader["CreatedByUserID"]!.ToString(),
                CreatedAt = reader["CreatedAt"] == DBNull.Value ? DateTime.Today : Convert.ToDateTime(reader["CreatedAt"])
            };
        }

        var lines = new List<StockMovementLineVm>();
        {
            await using var cmd2 = new SqlCommand(linesSql, conn);
            cmd2.Parameters.AddWithValue("@id", movementId);
            await using var reader2 = await cmd2.ExecuteReaderAsync();

            while (await reader2.ReadAsync())
            {
                lines.Add(new StockMovementLineVm
                {
                    ProductId = reader2.GetInt32(reader2.GetOrdinal("ProductID")),
                    Quantity = reader2.GetInt32(reader2.GetOrdinal("Quantity")),
                    UnitPrice = reader2["UnitPrice"] == DBNull.Value ? 0 : Convert.ToDecimal(reader2["UnitPrice"])
                });
            }
        }

        return (header, lines);
    }

    public async Task<List<(int ProductId, string Barcode, string Name, int StockQuantity)>> GetProductsForDropdownAsync()
    {
        const string sql = @"
SELECT TOP 2000 ProductID, Barcode, Name, StockQuantity
FROM Products
ORDER BY Name";

        await using var conn = db.CreateConnection();
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection);

        var list = new List<(int, string, string, int)>();
        while (await reader.ReadAsync())
        {
            list.Add((
                reader.GetInt32(reader.GetOrdinal("ProductID")),
                reader["Barcode"]?.ToString() ?? "",
                reader["Name"]?.ToString() ?? "",
                reader["StockQuantity"] == DBNull.Value ? 0 : Convert.ToInt32(reader["StockQuantity"])
            ));
        }

        return list;
    }

    public async Task<int> InsertMovementAsync(StockMovementFormVm form, int createdByUserId)
    {
        if (form.Lines.Count == 0) throw new InvalidOperationException("No movement lines.");

        var sign = string.Equals(form.MovementType, "IN", StringComparison.OrdinalIgnoreCase) ? 1 : -1;

        await using var conn = db.CreateConnection();
        await conn.OpenAsync();
        using var tx = conn.BeginTransaction();

        try
        {
            const string insertHeaderSql = @"
INSERT INTO StockMovementHeaders (MovementType, AffectsStock, Note, CreatedByUserID, CreatedAt)
VALUES (@type, @affects, @note, @createdBy, @createdAt);
SELECT CAST(SCOPE_IDENTITY() AS int);";

            await using var cmdHeader = new SqlCommand(insertHeaderSql, conn, tx);
            cmdHeader.Parameters.AddWithValue("@type", form.MovementType);
            cmdHeader.Parameters.AddWithValue("@affects", form.AffectsStock);
            cmdHeader.Parameters.AddWithValue("@note", (object?)form.Note ?? DBNull.Value);
            cmdHeader.Parameters.AddWithValue("@createdBy", createdByUserId);
            cmdHeader.Parameters.AddWithValue("@createdAt", form.CreatedAt);

            var obj = await cmdHeader.ExecuteScalarAsync();
            var movementId = obj == null || obj == DBNull.Value ? 0 : Convert.ToInt32(obj);
            if (movementId <= 0) throw new InvalidOperationException("Could not insert movement header.");

            // Stock updates & line inserts.
            foreach (var line in form.Lines)
            {
                if (line.Quantity == 0) continue;

                if (form.AffectsStock)
                {
                    // Validate stock after delta.
                    const string stockSql = @"SELECT StockQuantity FROM Products WHERE ProductID=@pid";
                    await using var stockCmd = new SqlCommand(stockSql, conn, tx);
                    stockCmd.Parameters.AddWithValue("@pid", line.ProductId);
                    var stockObj = await stockCmd.ExecuteScalarAsync();
                    var stock = stockObj == null || stockObj == DBNull.Value ? 0 : Convert.ToInt32(stockObj);

                    var delta = sign * line.Quantity;
                    var next = stock + delta;
                    if (next < 0) throw new InvalidOperationException($"Insufficient stock for product {line.ProductId}.");

                    const string updateSql = @"UPDATE Products SET StockQuantity = StockQuantity + @delta WHERE ProductID=@pid";
                    await using var updCmd = new SqlCommand(updateSql, conn, tx);
                    updCmd.Parameters.AddWithValue("@delta", delta);
                    updCmd.Parameters.AddWithValue("@pid", line.ProductId);
                    await updCmd.ExecuteNonQueryAsync();
                }

                const string insertLineSql = @"
INSERT INTO StockMovementLines (MovementID, ProductID, Quantity, UnitPrice)
VALUES (@mid, @pid, @qty, @price);";
                await using var lineCmd = new SqlCommand(insertLineSql, conn, tx);
                lineCmd.Parameters.AddWithValue("@mid", movementId);
                lineCmd.Parameters.AddWithValue("@pid", line.ProductId);
                lineCmd.Parameters.AddWithValue("@qty", line.Quantity);
                lineCmd.Parameters.AddWithValue("@price", line.UnitPrice);
                await lineCmd.ExecuteNonQueryAsync();
            }

            tx.Commit();
            return movementId;
        }
        catch
        {
            try { tx.Rollback(); } catch { }
            throw;
        }
    }

    public async Task UpdateMovementAsync(StockMovementFormVm form, int createdByUserId)
    {
        if (form.MovementId == null) throw new InvalidOperationException("Missing MovementId.");

        // Update strategy:
        // 1) Load old header & lines.
        // 2) Revert old stock delta (if old header affects stock).
        // 3) Delete old lines.
        // 4) Update header.
        // 5) Apply new stock delta (if new form affects stock) + insert new lines.
        var (oldHeader, oldLines) = await GetMovementAsync(form.MovementId.Value);
        if (oldHeader == null) throw new InvalidOperationException("Movement not found.");

        var oldSign = string.Equals(oldHeader.MovementType, "IN", StringComparison.OrdinalIgnoreCase) ? 1 : -1;
        var newSign = string.Equals(form.MovementType, "IN", StringComparison.OrdinalIgnoreCase) ? 1 : -1;

        await using var conn = db.CreateConnection();
        await conn.OpenAsync();
        using var tx = conn.BeginTransaction();

        try
        {
            if (oldHeader.AffectsStock)
            {
                foreach (var oldLine in oldLines)
                {
                    if (oldLine.Quantity == 0) continue;
                    var revertDelta = -oldSign * oldLine.Quantity;
                    const string updateSql = @"UPDATE Products SET StockQuantity = StockQuantity + @delta WHERE ProductID=@pid";
                    await using var upd = new SqlCommand(updateSql, conn, tx);
                    upd.Parameters.AddWithValue("@delta", revertDelta);
                    upd.Parameters.AddWithValue("@pid", oldLine.ProductId);
                    await upd.ExecuteNonQueryAsync();
                }
            }

            // Delete old lines.
            const string deleteLinesSql = @"DELETE FROM StockMovementLines WHERE MovementID=@mid";
            await using (var delCmd = new SqlCommand(deleteLinesSql, conn, tx))
            {
                delCmd.Parameters.AddWithValue("@mid", form.MovementId.Value);
                await delCmd.ExecuteNonQueryAsync();
            }

            // Update header.
            const string updateHeaderSql = @"
UPDATE StockMovementHeaders
SET MovementType=@type,
    AffectsStock=@affects,
    Note=@note,
    CreatedByUserID=@createdBy,
    CreatedAt=@createdAt
WHERE MovementID=@mid";
            await using (var updHeader = new SqlCommand(updateHeaderSql, conn, tx))
            {
                updHeader.Parameters.AddWithValue("@type", form.MovementType);
                updHeader.Parameters.AddWithValue("@affects", form.AffectsStock);
                updHeader.Parameters.AddWithValue("@note", (object?)form.Note ?? DBNull.Value);
                updHeader.Parameters.AddWithValue("@createdBy", createdByUserId);
                updHeader.Parameters.AddWithValue("@createdAt", form.CreatedAt);
                updHeader.Parameters.AddWithValue("@mid", form.MovementId.Value);
                await updHeader.ExecuteNonQueryAsync();
            }

            // Apply new lines.
            foreach (var line in form.Lines)
            {
                if (line.Quantity == 0) continue;

                if (form.AffectsStock)
                {
                    const string stockSql = @"SELECT StockQuantity FROM Products WHERE ProductID=@pid";
                    await using var stockCmd = new SqlCommand(stockSql, conn, tx);
                    stockCmd.Parameters.AddWithValue("@pid", line.ProductId);
                    var stockObj = await stockCmd.ExecuteScalarAsync();
                    var stock = stockObj == null || stockObj == DBNull.Value ? 0 : Convert.ToInt32(stockObj);

                    var delta = newSign * line.Quantity;
                    var next = stock + delta;
                    if (next < 0) throw new InvalidOperationException($"Insufficient stock for product {line.ProductId}.");

                    const string updateSql = @"UPDATE Products SET StockQuantity = StockQuantity + @delta WHERE ProductID=@pid";
                    await using var updCmd = new SqlCommand(updateSql, conn, tx);
                    updCmd.Parameters.AddWithValue("@delta", delta);
                    updCmd.Parameters.AddWithValue("@pid", line.ProductId);
                    await updCmd.ExecuteNonQueryAsync();
                }

                const string insertLineSql = @"
INSERT INTO StockMovementLines (MovementID, ProductID, Quantity, UnitPrice)
VALUES (@mid, @pid, @qty, @price);";
                await using var lineCmd = new SqlCommand(insertLineSql, conn, tx);
                lineCmd.Parameters.AddWithValue("@mid", form.MovementId.Value);
                lineCmd.Parameters.AddWithValue("@pid", line.ProductId);
                lineCmd.Parameters.AddWithValue("@qty", line.Quantity);
                lineCmd.Parameters.AddWithValue("@price", line.UnitPrice);
                await lineCmd.ExecuteNonQueryAsync();
            }

            tx.Commit();
        }
        catch
        {
            try { tx.Rollback(); } catch { }
            throw;
        }
    }

    public async Task DeleteMovementAsync(int movementId)
    {
        var (header, lines) = await GetMovementAsync(movementId);
        if (header == null) return;

        var sign = string.Equals(header.MovementType, "IN", StringComparison.OrdinalIgnoreCase) ? 1 : -1;

        await using var conn = db.CreateConnection();
        await conn.OpenAsync();
        using var tx = conn.BeginTransaction();

        try
        {
            if (header.AffectsStock)
            {
                // Revert original stock delta.
                foreach (var line in lines)
                {
                    if (line.Quantity == 0) continue;
                    var revertDelta = -sign * line.Quantity;
                    const string updateSql = @"UPDATE Products SET StockQuantity = StockQuantity + @delta WHERE ProductID=@pid";
                    await using var upd = new SqlCommand(updateSql, conn, tx);
                    upd.Parameters.AddWithValue("@delta", revertDelta);
                    upd.Parameters.AddWithValue("@pid", line.ProductId);
                    await upd.ExecuteNonQueryAsync();
                }
            }

            // Delete header (lines cascade if FK exists; still safe to delete explicitly).
            const string deleteLinesSql = @"DELETE FROM StockMovementLines WHERE MovementID=@mid";
            await using (var delLines = new SqlCommand(deleteLinesSql, conn, tx))
            {
                delLines.Parameters.AddWithValue("@mid", movementId);
                await delLines.ExecuteNonQueryAsync();
            }

            const string deleteHeaderSql = @"DELETE FROM StockMovementHeaders WHERE MovementID=@mid";
            await using (var delHeader = new SqlCommand(deleteHeaderSql, conn, tx))
            {
                delHeader.Parameters.AddWithValue("@mid", movementId);
                await delHeader.ExecuteNonQueryAsync();
            }

            tx.Commit();
        }
        catch
        {
            try { tx.Rollback(); } catch { }
            throw;
        }
    }
}

