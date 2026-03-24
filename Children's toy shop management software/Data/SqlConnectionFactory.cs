using Microsoft.Data.SqlClient;

namespace Children_s_toy_shop_management_software.Data;

public sealed class SqlConnectionFactory(IConfiguration configuration) : IDbConnectionFactory
{
    private readonly IConfiguration _configuration = configuration;

    public SqlConnection CreateConnection()
    {
        var connectionString = _configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Missing ConnectionStrings:DefaultConnection in appsettings.json");

        return new SqlConnection(connectionString);
    }
}

