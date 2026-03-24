using Children_s_toy_shop_management_software.Data;
using Microsoft.AspNetCore.Mvc;

namespace Children_s_toy_shop_management_software.Controllers;

[ApiController]
[Route("health")]
public sealed class HealthController(IDbConnectionFactory db) : ControllerBase
{
    private readonly IDbConnectionFactory _db = db;

    [HttpGet("db")]
    public async Task<IActionResult> Db(CancellationToken cancellationToken)
    {
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync(cancellationToken);

        return Ok(new { status = "ok" });
    }
}

