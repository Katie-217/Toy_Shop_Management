using System.Data;
using Children_s_toy_shop_management_software.Models.Staff;
using Microsoft.Data.SqlClient;

namespace Children_s_toy_shop_management_software.Data;

public sealed class StaffRepository(IDbConnectionFactory db)
{
    public async Task EnsureSchemaAsync()
    {
        const string sql = @"
IF COL_LENGTH('Employees', 'PositionTitle') IS NULL
BEGIN
    ALTER TABLE Employees ADD PositionTitle NVARCHAR(120) NULL;
END
IF COL_LENGTH('Employees', 'ImagePath') IS NULL
BEGIN
    ALTER TABLE Employees ADD ImagePath NVARCHAR(260) NULL;
END";

        await using var conn = db.CreateConnection();
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<EmployeeVm>> GetEmployeesAsync(string? search, int sortIndex)
    {
        var q = string.IsNullOrWhiteSpace(search) ? "" : search.Trim().ToLowerInvariant();

        var orderClause = sortIndex switch
        {
            1 => "EmployeeID ASC",
            2 => "EmployeeID DESC",
            3 => "FullName ASC",
            4 => "FullName DESC",
            _ => "EmployeeID DESC"
        };

        var sql = $@"
SELECT EmployeeID, FullName, Gender, BirthDate, Email, Address, Phone, ISNULL(PositionTitle,'') AS PositionTitle, ISNULL(ImagePath,'') AS ImagePath
FROM Employees
WHERE (@q = '' OR LOWER(FullName) LIKE @like)
ORDER BY {orderClause}";

        await using var conn = db.CreateConnection();
        await conn.OpenAsync();

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@q", q);
        cmd.Parameters.AddWithValue("@like", $"%{q}%");

        await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection);
        var list = new List<EmployeeVm>();
        while (await reader.ReadAsync())
        {
            list.Add(new EmployeeVm
            {
                Id = reader.GetInt32(reader.GetOrdinal("EmployeeID")),
                FullName = reader["FullName"]?.ToString() ?? "",
                Gender = reader["Gender"]?.ToString() ?? "Male",
                BirthDate = reader["BirthDate"] == DBNull.Value
                    ? new DateTime(2000, 1, 1)
                    : Convert.ToDateTime(reader["BirthDate"]),
                Email = reader["Email"]?.ToString() ?? "",
                Address = reader["Address"]?.ToString() ?? "",
                Phone = reader["Phone"]?.ToString() ?? "",
                PositionTitle = reader["PositionTitle"]?.ToString() ?? "",
                ImagePath = reader["ImagePath"]?.ToString() ?? ""
            });
        }

        return list;
    }

    public async Task<EmployeeVm?> GetByIdAsync(int id)
    {
        const string sql = @"
SELECT TOP 1 EmployeeID, FullName, Gender, BirthDate, Email, Address, Phone, ISNULL(PositionTitle,'') AS PositionTitle, ISNULL(ImagePath,'') AS ImagePath
FROM Employees
WHERE EmployeeID = @id";

        await using var conn = db.CreateConnection();
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@id", id);

        await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection);
        if (!await reader.ReadAsync()) return null;

        return new EmployeeVm
        {
            Id = reader.GetInt32(reader.GetOrdinal("EmployeeID")),
            FullName = reader["FullName"]?.ToString() ?? "",
            Gender = reader["Gender"]?.ToString() ?? "Male",
            BirthDate = reader["BirthDate"] == DBNull.Value ? new DateTime(2000, 1, 1) : Convert.ToDateTime(reader["BirthDate"]),
            Email = reader["Email"]?.ToString() ?? "",
            Address = reader["Address"]?.ToString() ?? "",
            Phone = reader["Phone"]?.ToString() ?? "",
            PositionTitle = reader["PositionTitle"]?.ToString() ?? "",
            ImagePath = reader["ImagePath"]?.ToString() ?? ""
        };
    }

    public async Task<int> InsertAsync(EmployeeFormVm form)
    {
        const string sql = @"
INSERT INTO Employees (FullName, Gender, BirthDate, Email, Address, Phone, PositionTitle, ImagePath)
VALUES (@FullName, @Gender, @BirthDate, @Email, @Address, @Phone, @PositionTitle, @ImagePath);
SELECT CAST(SCOPE_IDENTITY() AS int);";

        await using var conn = db.CreateConnection();
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@FullName", (form.FullName ?? "").Trim());
        cmd.Parameters.AddWithValue("@Gender", (form.Gender ?? "").Trim());
        cmd.Parameters.AddWithValue("@BirthDate", form.BirthDate);
        cmd.Parameters.AddWithValue("@Email", (form.Email ?? "").Trim());
        cmd.Parameters.AddWithValue("@Address", (form.Address ?? "").Trim());
        cmd.Parameters.AddWithValue("@Phone", (form.Phone ?? "").Trim());
        cmd.Parameters.AddWithValue("@PositionTitle", (form.PositionTitle ?? "").Trim());
        cmd.Parameters.AddWithValue("@ImagePath", (form.ImagePath ?? "").Trim());

        var obj = await cmd.ExecuteScalarAsync();
        return obj == null || obj == DBNull.Value ? 0 : Convert.ToInt32(obj);
    }

    public async Task UpdateAsync(EmployeeFormVm form)
    {
        if (form.EmployeeId == null) throw new InvalidOperationException("Missing EmployeeId");

        const string sql = @"
UPDATE Employees
SET FullName=@FullName, Gender=@Gender, BirthDate=@BirthDate, Email=@Email, Address=@Address, Phone=@Phone, PositionTitle=@PositionTitle, ImagePath=COALESCE(NULLIF(@ImagePath,''), ImagePath)
WHERE EmployeeID=@EmployeeID";

        await using var conn = db.CreateConnection();
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@FullName", (form.FullName ?? "").Trim());
        cmd.Parameters.AddWithValue("@Gender", (form.Gender ?? "").Trim());
        cmd.Parameters.AddWithValue("@BirthDate", form.BirthDate);
        cmd.Parameters.AddWithValue("@Email", (form.Email ?? "").Trim());
        cmd.Parameters.AddWithValue("@Address", (form.Address ?? "").Trim());
        cmd.Parameters.AddWithValue("@Phone", (form.Phone ?? "").Trim());
        cmd.Parameters.AddWithValue("@PositionTitle", (form.PositionTitle ?? "").Trim());
        cmd.Parameters.AddWithValue("@ImagePath", (form.ImagePath ?? "").Trim());
        cmd.Parameters.AddWithValue("@EmployeeID", form.EmployeeId.Value);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<int> UpsertByPhoneAsync(EmployeeFormVm form)
    {
        var phone = (form.Phone ?? "").Trim();
        if (string.IsNullOrWhiteSpace(phone))
        {
            throw new InvalidOperationException("Phone is required for upsert.");
        }

        await using var conn = db.CreateConnection();
        await conn.OpenAsync();

        const string lookupSql = @"
SELECT TOP 1 EmployeeID
FROM Employees
WHERE Phone = @Phone";

        await using var lookupCmd = new SqlCommand(lookupSql, conn);
        lookupCmd.Parameters.AddWithValue("@Phone", phone);
        var existingObj = await lookupCmd.ExecuteScalarAsync();
        var existingId = existingObj == null || existingObj == DBNull.Value ? (int?)null : Convert.ToInt32(existingObj);

        if (existingId.HasValue)
        {
            const string updateSql = @"
UPDATE Employees
SET FullName=@FullName, Gender=@Gender, BirthDate=@BirthDate, Email=@Email, Address=@Address, Phone=@Phone, PositionTitle=@PositionTitle, ImagePath=COALESCE(NULLIF(@ImagePath,''), ImagePath)
WHERE EmployeeID=@EmployeeID";

            await using var updateCmd = new SqlCommand(updateSql, conn);
            updateCmd.Parameters.AddWithValue("@FullName", (form.FullName ?? "").Trim());
            updateCmd.Parameters.AddWithValue("@Gender", (form.Gender ?? "").Trim());
            updateCmd.Parameters.AddWithValue("@BirthDate", form.BirthDate);
            updateCmd.Parameters.AddWithValue("@Email", (form.Email ?? "").Trim());
            updateCmd.Parameters.AddWithValue("@Address", (form.Address ?? "").Trim());
            updateCmd.Parameters.AddWithValue("@Phone", phone);
            updateCmd.Parameters.AddWithValue("@PositionTitle", (form.PositionTitle ?? "").Trim());
            updateCmd.Parameters.AddWithValue("@ImagePath", (form.ImagePath ?? "").Trim());
            updateCmd.Parameters.AddWithValue("@EmployeeID", existingId.Value);
            await updateCmd.ExecuteNonQueryAsync();
            return existingId.Value;
        }

        const string insertSql = @"
INSERT INTO Employees (FullName, Gender, BirthDate, Email, Address, Phone, PositionTitle, ImagePath)
VALUES (@FullName, @Gender, @BirthDate, @Email, @Address, @Phone, @PositionTitle, @ImagePath);
SELECT CAST(SCOPE_IDENTITY() AS int);";

        await using var insertCmd = new SqlCommand(insertSql, conn);
        insertCmd.Parameters.AddWithValue("@FullName", (form.FullName ?? "").Trim());
        insertCmd.Parameters.AddWithValue("@Gender", (form.Gender ?? "").Trim());
        insertCmd.Parameters.AddWithValue("@BirthDate", form.BirthDate);
        insertCmd.Parameters.AddWithValue("@Email", (form.Email ?? "").Trim());
        insertCmd.Parameters.AddWithValue("@Address", (form.Address ?? "").Trim());
        insertCmd.Parameters.AddWithValue("@Phone", phone);
        insertCmd.Parameters.AddWithValue("@PositionTitle", (form.PositionTitle ?? "").Trim());
        insertCmd.Parameters.AddWithValue("@ImagePath", (form.ImagePath ?? "").Trim());
        var obj = await insertCmd.ExecuteScalarAsync();
        return obj == null || obj == DBNull.Value ? 0 : Convert.ToInt32(obj);
    }

    public async Task DeleteAsync(int id)
    {
        const string sql = "DELETE FROM Employees WHERE EmployeeID=@id";
        await using var conn = db.CreateConnection();
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync();
    }
}

