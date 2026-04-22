using System.Data;
using Children_s_toy_shop_management_software.Models.Account;
using Microsoft.Data.SqlClient;

namespace Children_s_toy_shop_management_software.Data;

public sealed class AccountRepository(IDbConnectionFactory db)
{
    public async Task EnsureSchemaAsync()
    {
        const string sql = @"
IF OBJECT_ID('dbo.Users', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Users (
        UserID INT IDENTITY(1,1) PRIMARY KEY,
        Username NVARCHAR(50) NOT NULL UNIQUE,
        PasswordHash NVARCHAR(255) NOT NULL,
        FullName NVARCHAR(150) NOT NULL,
        Role NVARCHAR(20) NOT NULL, -- 'Admin', 'Cashier'
        EmployeeID INT NULL,        -- Links to Employees table
        CreatedAt DATETIME DEFAULT GETDATE()
    );
END
ELSE
BEGIN
    -- If Role column is INT (legacy), convert it to NVARCHAR
    IF TYPE_NAME(COLUMNPROPERTY(OBJECT_ID('dbo.Users'), 'Role', 'SystemType')) = 'int'
    BEGIN
        DECLARE @DropSql NVARCHAR(MAX) = N'';
        SELECT @DropSql += N'ALTER TABLE dbo.Users DROP CONSTRAINT ' + QUOTENAME(d.name) + N';'
        FROM sys.default_constraints d
        WHERE d.parent_object_id = OBJECT_ID('dbo.Users')
          AND d.parent_column_id = COLUMNPROPERTY(object_id('dbo.Users'), 'Role', 'ColumnId');

        SELECT @DropSql += N'ALTER TABLE dbo.Users DROP CONSTRAINT ' + QUOTENAME(c.name) + N';'
        FROM sys.check_constraints c
        WHERE c.parent_object_id = OBJECT_ID('dbo.Users')
          AND c.parent_column_id = COLUMNPROPERTY(object_id('dbo.Users'), 'Role', 'ColumnId');

        IF LEN(@DropSql) > 0 EXEC sp_executesql @DropSql;

        ALTER TABLE dbo.Users ALTER COLUMN Role NVARCHAR(20) NOT NULL;
    END

    -- Ensure EmployeeID is NULLABLE (for system accounts like admin)
    IF COLUMNPROPERTY(OBJECT_ID('dbo.Users'), 'EmployeeID', 'AllowsNull') = 0
    BEGIN
        ALTER TABLE dbo.Users ALTER COLUMN EmployeeID INT NULL;
    END
END

IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Username = 'admin')
BEGIN
    INSERT INTO dbo.Users (Username, PasswordHash, FullName, Role)
    VALUES ('admin', 'admin@', 'System Administrator', 'Admin');
END";

        await using var conn = db.CreateConnection();
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync();

        await SyncStaffUsersAsync();
    }

    public async Task SyncStaffUsersAsync()
    {
        const string sql = @"
INSERT INTO dbo.Users (Username, PasswordHash, FullName, Role, EmployeeID)
SELECT 
    CAST(e.EmployeeID AS NVARCHAR(50)), 
    CAST(e.EmployeeID AS NVARCHAR(50)) + CAST(ABS(CHECKSUM(NEWID()) % 90 + 10) AS NVARCHAR(2)), 
    e.FullName, 
    'Cashier', 
    e.EmployeeID
FROM dbo.Employees e
WHERE (LOWER(e.PositionTitle) LIKE N'%thu ngân%' OR LOWER(e.PositionTitle) LIKE N'%cashier%')
  AND e.EmployeeID NOT IN (SELECT ISNULL(EmployeeID, 0) FROM dbo.Users)";

        await using var conn = db.CreateConnection();
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<UserVm?> ValidateLoginAsync(string username, string password)
    {
        const string sql = @"
SELECT TOP 1 UserID, Username, FullName, Role, EmployeeID
FROM dbo.Users
WHERE Username = @username AND PasswordHash = @password";

        await using var conn = db.CreateConnection();
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@username", username);
        cmd.Parameters.AddWithValue("@password", password);

        await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection);
        if (!await reader.ReadAsync()) return null;

        return new UserVm
        {
            UserId = reader.GetInt32(reader.GetOrdinal("UserID")),
            Username = reader["Username"]?.ToString() ?? "",
            FullName = reader["FullName"]?.ToString() ?? "",
            Role = reader["Role"]?.ToString() ?? "Cashier",
            EmployeeId = reader["EmployeeID"] == DBNull.Value ? null : Convert.ToInt32(reader["EmployeeID"])
        };
    }
}
