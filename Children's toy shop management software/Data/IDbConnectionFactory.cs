using Microsoft.Data.SqlClient;

namespace Children_s_toy_shop_management_software.Data;

public interface IDbConnectionFactory
{
    SqlConnection CreateConnection();
}

