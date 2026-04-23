using System.Globalization;
using Children_s_toy_shop_management_software.Models.Dashboard;
using Microsoft.Data.SqlClient;

namespace Children_s_toy_shop_management_software.Data;

public sealed class DashboardRepository(IDbConnectionFactory db)
{
    public async Task<DashboardPageVm> GetKpisAsync(string? detailBy)
    {
        var today = DateTime.Today;
        var tomorrow = today.AddDays(1);
        var newUsersFrom = today.AddDays(-6); // inclusive 7 days
        var newUsersToExclusive = tomorrow;  // exclusive (tomorrow)

        var weekFrom = today.AddDays(-6).Date;
        var weekToExclusive = tomorrow.Date;

        var thisMonthStart = new DateTime(today.Year, today.Month, 1);
        var nextMonthStart = thisMonthStart.AddMonths(1);
        var prevMonthStart = thisMonthStart.AddMonths(-1);

        var detail = string.IsNullOrWhiteSpace(detailBy) ? "week" : detailBy.Trim().ToLowerInvariant();
        if (detail != "week" && detail != "month" && detail != "quarter" && detail != "year")
        {
            detail = "week";
        }

        const int lowStockThreshold = 5;

        // Use one connection for multiple aggregates.
        await using var conn = db.CreateConnection();
        await conn.OpenAsync();

        // 1) Dashboard KPIs:
        // - Giữ các field "Today" để không làm hỏng view đang có.
        // - Thêm các field aggregate để chuẩn bị port dashboard mới theo MVC.
        const string kpiSql = @"
DECLARE @hasStatusCol INT = CASE WHEN COL_LENGTH('dbo.Orders', 'Status') IS NULL THEN 0 ELSE 1 END;

SELECT
    -- Today KPIs (existing view expects these)
    ISNULL((
        SELECT SUM(o.TotalAmount)
        FROM dbo.Orders o
        WHERE o.OrderDate >= @from AND o.OrderDate < @to
          AND (@hasStatusCol = 0 OR LOWER(ISNULL(CONVERT(nvarchar(50), o.Status), N'')) IN (N'paid', N''))
    ), 0) AS RevenueToday,
    ISNULL((
        SELECT COUNT(*)
        FROM dbo.Orders o
        WHERE o.OrderDate >= @from AND o.OrderDate < @to
          AND (@hasStatusCol = 0 OR LOWER(ISNULL(CONVERT(nvarchar(50), o.Status), N'')) IN (N'paid', N''))
    ), 0) AS OrdersToday,
    ISNULL((
        SELECT COUNT(*)
        FROM dbo.StockMovementHeaders h
        WHERE UPPER(ISNULL(h.MovementType, N'')) = N'OUT'
          AND h.CreatedAt >= @from AND h.CreatedAt < @to
    ), 0) AS ReturnedToday,
    (ISNULL((
        SELECT SUM(o.TotalAmount)
        FROM dbo.Orders o
        WHERE o.OrderDate >= @from AND o.OrderDate < @to
          AND (@hasStatusCol = 0 OR LOWER(ISNULL(CONVERT(nvarchar(50), o.Status), N'')) IN (N'paid', N''))
    ), 0) * CAST(0.8 AS DECIMAL(18,2))) AS IncomeToday,

    ISNULL((SELECT COUNT(*) FROM dbo.Products WHERE StockQuantity <= @lowStock), 0) AS LowStockCount,
    ISNULL((SELECT COUNT(*) FROM dbo.Customers), 0) AS CustomersCount,

    -- Dashboard aggregates (new fields)
    ISNULL((
        SELECT SUM(o.TotalAmount)
        FROM dbo.Orders o
        WHERE (@hasStatusCol = 0 OR LOWER(ISNULL(CONVERT(nvarchar(50), o.Status), N'')) IN (N'paid', N''))
    ), 0) AS RevenueTotal,
    ISNULL((
        SELECT COUNT(*)
        FROM dbo.Orders o
        WHERE (@hasStatusCol = 0 OR LOWER(ISNULL(CONVERT(nvarchar(50), o.Status), N'')) IN (N'paid', N''))
    ), 0) AS TotalOrders,
    ISNULL((SELECT COUNT(*) FROM dbo.Customers), 0) AS TotalCustomers,
    ISNULL((
        SELECT COUNT(*)
        FROM (
            SELECT o.CustomerID, MIN(o.OrderDate) AS FirstOrderDate
            FROM dbo.Orders o
            WHERE (@hasStatusCol = 0 OR LOWER(ISNULL(CONVERT(nvarchar(50), o.Status), N'')) IN (N'paid', N''))
            GROUP BY o.CustomerID
        ) x
        WHERE x.FirstOrderDate >= @newUsersFrom AND x.FirstOrderDate < @newUsersToExclusive
    ), 0) AS NewCustomers,
    (
        SELECT MAX(o.OrderDate)
        FROM dbo.Orders o
        WHERE (@hasStatusCol = 0 OR LOWER(ISNULL(CONVERT(nvarchar(50), o.Status), N'')) IN (N'paid', N''))
    ) AS LastOrderDate
;";

        DashboardPageVm vm;
        await using (var cmd = new SqlCommand(kpiSql, conn))
        {
            cmd.Parameters.AddWithValue("@from", today);
            cmd.Parameters.AddWithValue("@to", tomorrow);
            cmd.Parameters.AddWithValue("@newUsersFrom", newUsersFrom);
            cmd.Parameters.AddWithValue("@newUsersToExclusive", newUsersToExclusive);
            cmd.Parameters.AddWithValue("@lowStock", lowStockThreshold);

            await using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                return new DashboardPageVm();
            }

            vm = new DashboardPageVm
            {
                RevenueToday = reader["RevenueToday"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["RevenueToday"]),
                OrdersToday = reader["OrdersToday"] == DBNull.Value ? 0 : Convert.ToInt32(reader["OrdersToday"]),
                ReturnedToday = reader["ReturnedToday"] == DBNull.Value ? 0 : Convert.ToInt32(reader["ReturnedToday"]),

                RevenueTotal = reader["RevenueTotal"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["RevenueTotal"]),
                TotalOrders = reader["TotalOrders"] == DBNull.Value ? 0 : Convert.ToInt32(reader["TotalOrders"]),
                TotalCustomers = reader["TotalCustomers"] == DBNull.Value ? 0 : Convert.ToInt32(reader["TotalCustomers"]),
                NewCustomers = reader["NewCustomers"] == DBNull.Value ? 0 : Convert.ToInt32(reader["NewCustomers"]),
                LastOrderDate = reader["LastOrderDate"] == DBNull.Value ? null : Convert.ToDateTime(reader["LastOrderDate"]),

                // Backward-compatible/unused by the new view
                LowStockCount = reader["LowStockCount"] == DBNull.Value ? 0 : Convert.ToInt32(reader["LowStockCount"]),
                CustomersCount = reader["CustomersCount"] == DBNull.Value ? 0 : Convert.ToInt32(reader["CustomersCount"]),
                IncomeToday = reader["IncomeToday"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["IncomeToday"]),
            };
        }

        // 2) Series for selected "Detail by": week/month/quarter/year
        // Used by chart + weekly details table (same model `RevenueByDays`).
        DateTime seriesFrom;
        DateTime seriesToExclusive;
        Func<DateTime, DateTime> nextBucket;
        Func<DateTime, string> labelFn;
        string bucketExpr;

        if (detail == "week")
        {
            seriesFrom = weekFrom;
            seriesToExclusive = weekToExclusive;
            nextBucket = d => d.AddDays(1);
            labelFn = d => d.ToString("ddd", CultureInfo.InvariantCulture);
            bucketExpr = "CAST(o.OrderDate AS date)";
        }
        else if (detail == "month")
        {
            // Show 12 months in the current year.
            seriesFrom = new DateTime(today.Year, 1, 1);
            seriesToExclusive = seriesFrom.AddYears(1);
            nextBucket = d => d.AddMonths(1);
            labelFn = d => d.ToString("MMM", CultureInfo.InvariantCulture);
            bucketExpr = "DATEFROMPARTS(YEAR(o.OrderDate), MONTH(o.OrderDate), 1)";
        }
        else if (detail == "quarter")
        {
            // Show 4 quarters in the current year.
            seriesFrom = new DateTime(today.Year, 1, 1);
            seriesToExclusive = seriesFrom.AddYears(1);
            nextBucket = d => d.AddMonths(3);
            labelFn = d =>
            {
                var q = ((d.Month - 1) / 3) + 1;
                return "Q" + q;
            };
            bucketExpr = "DATEFROMPARTS(YEAR(o.OrderDate), (((MONTH(o.OrderDate) - 1) / 3) * 3) + 1, 1)";
        }
        else
        {
            // Show last 5 years.
            var startYear = today.Year - 4;
            seriesFrom = new DateTime(startYear, 1, 1);
            seriesToExclusive = new DateTime(today.Year + 1, 1, 1);
            nextBucket = d => d.AddYears(1);
            labelFn = d => d.Year.ToString(CultureInfo.InvariantCulture);
            bucketExpr = "DATEFROMPARTS(YEAR(o.OrderDate), 1, 1)";
        }

        var seriesOrdersMap = new Dictionary<DateTime, (int OrdersCount, decimal Revenue)>();
        var seriesSoldMap = new Dictionary<DateTime, int>();

        // Keep "Status" filtering consistent with the KPI query:
        // - if dbo.Orders.Status column does not exist -> treat as paid
        // - else accept statuses: 'paid' or '' or NULL
        var ordersBucketExpr = bucketExpr;

        const string statusColCheckAndFilter = @"DECLARE @hasStatusCol INT = CASE WHEN COL_LENGTH('dbo.Orders', 'Status') IS NULL THEN 0 ELSE 1 END;";
        var ordersSql = $@"
{statusColCheckAndFilter}
SELECT
    {ordersBucketExpr} AS [Bucket],
    COUNT(DISTINCT o.OrderID) AS OrdersCount,
    ISNULL(SUM(o.TotalAmount), 0) AS Revenue
FROM dbo.Orders o
WHERE o.OrderDate >= @from AND o.OrderDate < @to
  AND (@hasStatusCol = 0 OR LOWER(ISNULL(CONVERT(nvarchar(50), o.Status), N'')) IN (N'paid', N''))
GROUP BY {ordersBucketExpr}
ORDER BY [Bucket];";

        await using (var cmd = new SqlCommand(ordersSql, conn))
        {
            cmd.Parameters.AddWithValue("@from", seriesFrom);
            cmd.Parameters.AddWithValue("@to", seriesToExclusive);

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var bucket = reader["Bucket"] == DBNull.Value ? DateTime.MinValue : Convert.ToDateTime(reader["Bucket"]).Date;
                if (bucket == DateTime.MinValue) continue;

                var ordersCount = reader["OrdersCount"] == DBNull.Value ? 0 : Convert.ToInt32(reader["OrdersCount"]);
                var revenue = reader["Revenue"] == DBNull.Value ? 0m : Convert.ToDecimal(reader["Revenue"]);
                seriesOrdersMap[bucket] = (ordersCount, revenue);
            }
        }

        var soldSql = $@"
{statusColCheckAndFilter}
SELECT
    {ordersBucketExpr} AS [Bucket],
    ISNULL(SUM(od.Quantity), 0) AS ProductsSold
FROM dbo.Orders o
INNER JOIN dbo.OrderDetails od ON od.OrderID = o.OrderID
WHERE o.OrderDate >= @from AND o.OrderDate < @to
  AND (@hasStatusCol = 0 OR LOWER(ISNULL(CONVERT(nvarchar(50), o.Status), N'')) IN (N'paid', N''))
GROUP BY {ordersBucketExpr}
ORDER BY [Bucket];";

        await using (var cmdSold = new SqlCommand(soldSql, conn))
        {
            cmdSold.Parameters.AddWithValue("@from", seriesFrom);
            cmdSold.Parameters.AddWithValue("@to", seriesToExclusive);

            await using var reader = await cmdSold.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var bucket = reader["Bucket"] == DBNull.Value ? DateTime.MinValue : Convert.ToDateTime(reader["Bucket"]).Date;
                if (bucket == DateTime.MinValue) continue;

                var sold = reader["ProductsSold"] == DBNull.Value ? 0 : Convert.ToInt32(reader["ProductsSold"]);
                seriesSoldMap[bucket] = sold;
            }
        }

        // Order status split per bucket (all orders in range — for Detail table "Orders" tab).
        var seriesStatusMap = new Dictionary<DateTime, (int Completed, int Pending, int Cancelled)>();
        var statusSql = $@"
{statusColCheckAndFilter}
SELECT
    {ordersBucketExpr} AS [Bucket],
    SUM(CASE
            WHEN @hasStatusCol = 0 THEN 1
            WHEN LOWER(ISNULL(CONVERT(nvarchar(50), o.Status), N'')) IN (N'cancelled', N'canceled', N'refunded') THEN 0
            WHEN LOWER(ISNULL(CONVERT(nvarchar(50), o.Status), N'')) IN (N'pending', N'processing') THEN 0
            ELSE 1
        END) AS Completed,
    SUM(CASE
            WHEN @hasStatusCol = 0 THEN 0
            WHEN LOWER(ISNULL(CONVERT(nvarchar(50), o.Status), N'')) IN (N'pending', N'processing') THEN 1
            ELSE 0
        END) AS Pending,
    SUM(CASE
            WHEN @hasStatusCol = 0 THEN 0
            WHEN LOWER(ISNULL(CONVERT(nvarchar(50), o.Status), N'')) IN (N'cancelled', N'canceled', N'refunded') THEN 1
            ELSE 0
        END) AS Cancelled
FROM dbo.Orders o
WHERE o.OrderDate >= @from AND o.OrderDate < @to
GROUP BY {ordersBucketExpr}
ORDER BY [Bucket];";

        await using (var cmdSt = new SqlCommand(statusSql, conn))
        {
            cmdSt.Parameters.AddWithValue("@from", seriesFrom);
            cmdSt.Parameters.AddWithValue("@to", seriesToExclusive);

            await using var reader = await cmdSt.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var bucket = reader["Bucket"] == DBNull.Value ? DateTime.MinValue : Convert.ToDateTime(reader["Bucket"]).Date;
                if (bucket == DateTime.MinValue) continue;

                var completed = reader["Completed"] == DBNull.Value ? 0 : Convert.ToInt32(reader["Completed"]);
                var pending = reader["Pending"] == DBNull.Value ? 0 : Convert.ToInt32(reader["Pending"]);
                var cancelled = reader["Cancelled"] == DBNull.Value ? 0 : Convert.ToInt32(reader["Cancelled"]);
                seriesStatusMap[bucket] = (completed, pending, cancelled);
            }
        }

        // Top-selling product name per time bucket (for "Products sold" detail table).
        var seriesTopProductMap = new Dictionary<DateTime, string>();
        var topSql = $@"
{statusColCheckAndFilter}
WITH agg AS (
    SELECT
        {ordersBucketExpr} AS [Bucket],
        od.ProductID,
        SUM(od.Quantity) AS Qty
    FROM dbo.Orders o
    INNER JOIN dbo.OrderDetails od ON od.OrderID = o.OrderID
    WHERE o.OrderDate >= @from AND o.OrderDate < @to
      AND (@hasStatusCol = 0 OR LOWER(ISNULL(CONVERT(nvarchar(50), o.Status), N'')) IN (N'paid', N''))
    GROUP BY {ordersBucketExpr}, od.ProductID
),
ranked AS (
    SELECT [Bucket], ProductID, Qty,
           ROW_NUMBER() OVER (PARTITION BY [Bucket] ORDER BY Qty DESC, ProductID ASC) AS rn
    FROM agg
)
SELECT r.[Bucket], ISNULL(p.Name, N'') AS TopProductName
FROM ranked r
INNER JOIN dbo.Products p ON p.ProductID = r.ProductID
WHERE r.rn = 1
ORDER BY r.[Bucket];";

        await using (var cmdTop = new SqlCommand(topSql, conn))
        {
            cmdTop.Parameters.AddWithValue("@from", seriesFrom);
            cmdTop.Parameters.AddWithValue("@to", seriesToExclusive);

            await using var reader = await cmdTop.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var bucket = reader["Bucket"] == DBNull.Value ? DateTime.MinValue : Convert.ToDateTime(reader["Bucket"]).Date;
                if (bucket == DateTime.MinValue) continue;

                var name = reader["TopProductName"] == DBNull.Value ? string.Empty : Convert.ToString(reader["TopProductName"]) ?? string.Empty;
                seriesTopProductMap[bucket] = name;
            }
        }

        var series = new List<DashboardDayVm>();
        for (var d = seriesFrom; d < seriesToExclusive; d = nextBucket(d))
        {
            seriesOrdersMap.TryGetValue(d, out var ov);
            var revenue = ov.Revenue;
            var profit = revenue * 0.8m;
            if (profit < 0) profit = 0m;
            var ordersCount = ov.OrdersCount;
            var productsSold = seriesSoldMap.TryGetValue(d, out var sold) ? sold : 0;
            seriesStatusMap.TryGetValue(d, out var st);
            seriesTopProductMap.TryGetValue(d, out var topName);

            series.Add(new DashboardDayVm
            {
                Date = d,
                Label = labelFn(d),
                OrdersCount = ordersCount,
                Revenue = revenue,
                Income = profit,
                ProductsSold = productsSold,
                OrdersCompleted = st.Completed,
                OrdersPending = st.Pending,
                OrdersCancelled = st.Cancelled,
                DiscountAmount = 0m,
                TopProductName = topName ?? string.Empty
            });
        }

        vm.RevenueByDays = series;

        // Month trend for side panel (last 4 months): used by "Monthly comparison" line chart.
        // Always computed independent of `detailBy`, so the side panel can show a nice 4-point trend.
        var monthTrendStart = thisMonthStart.AddMonths(-3);
        var monthTrendToExclusive = nextMonthStart;

        var monthTrendOrdersMap = new Dictionary<DateTime, (int OrdersCount, decimal Revenue)>();
        var monthTrendSoldMap = new Dictionary<DateTime, int>();

        var monthBucketExpr = "DATEFROMPARTS(YEAR(o.OrderDate), MONTH(o.OrderDate), 1)";

        var monthOrdersSql = $@"
{statusColCheckAndFilter}
SELECT
    {monthBucketExpr} AS [Bucket],
    COUNT(DISTINCT o.OrderID) AS OrdersCount,
    ISNULL(SUM(o.TotalAmount), 0) AS Revenue
FROM dbo.Orders o
WHERE o.OrderDate >= @from AND o.OrderDate < @to
  AND (@hasStatusCol = 0 OR LOWER(ISNULL(CONVERT(nvarchar(50), o.Status), N'')) IN (N'paid', N''))
GROUP BY {monthBucketExpr}
ORDER BY [Bucket];";

        await using (var cmd = new SqlCommand(monthOrdersSql, conn))
        {
            cmd.Parameters.AddWithValue("@from", monthTrendStart);
            cmd.Parameters.AddWithValue("@to", monthTrendToExclusive);

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var bucket = reader["Bucket"] == DBNull.Value ? DateTime.MinValue : Convert.ToDateTime(reader["Bucket"]).Date;
                if (bucket == DateTime.MinValue) continue;
                var ordersCount = reader["OrdersCount"] == DBNull.Value ? 0 : Convert.ToInt32(reader["OrdersCount"]);
                var revenue = reader["Revenue"] == DBNull.Value ? 0m : Convert.ToDecimal(reader["Revenue"]);
                monthTrendOrdersMap[bucket] = (ordersCount, revenue);
            }
        }

        var monthSoldSql = $@"
{statusColCheckAndFilter}
SELECT
    {monthBucketExpr} AS [Bucket],
    ISNULL(SUM(od.Quantity), 0) AS ProductsSold
FROM dbo.Orders o
INNER JOIN dbo.OrderDetails od ON od.OrderID = o.OrderID
WHERE o.OrderDate >= @from AND o.OrderDate < @to
  AND (@hasStatusCol = 0 OR LOWER(ISNULL(CONVERT(nvarchar(50), o.Status), N'')) IN (N'paid', N''))
GROUP BY {monthBucketExpr}
ORDER BY [Bucket];";

        await using (var cmdSold = new SqlCommand(monthSoldSql, conn))
        {
            cmdSold.Parameters.AddWithValue("@from", monthTrendStart);
            cmdSold.Parameters.AddWithValue("@to", monthTrendToExclusive);

            await using var reader = await cmdSold.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var bucket = reader["Bucket"] == DBNull.Value ? DateTime.MinValue : Convert.ToDateTime(reader["Bucket"]).Date;
                if (bucket == DateTime.MinValue) continue;
                var sold = reader["ProductsSold"] == DBNull.Value ? 0 : Convert.ToInt32(reader["ProductsSold"]);
                monthTrendSoldMap[bucket] = sold;
            }
        }

        var monthTrend = new List<DashboardDayVm>(capacity: 4);
        for (int i = 0; i < 4; i++)
        {
            var d = monthTrendStart.AddMonths(i);
            monthTrendOrdersMap.TryGetValue(d, out var ov);
            var revenue = ov.Revenue;
            var profit = revenue * 0.8m;
            if (profit < 0) profit = 0m;
            var ordersCount = ov.OrdersCount;
            var productsSold = monthTrendSoldMap.TryGetValue(d, out var sold) ? sold : 0;

            monthTrend.Add(new DashboardDayVm
            {
                Date = d,
                Label = d.ToString("MMM yyyy", CultureInfo.InvariantCulture),
                OrdersCount = ordersCount,
                Revenue = revenue,
                Income = profit,
                ProductsSold = productsSold
            });
        }

        vm.MonthTrendLast4 = monthTrend;

        // 3) Monthly revenue/orders/products sold and profit comparison (this month vs last month)
        const string monthlySql = @"
DECLARE @hasStatusCol INT = CASE WHEN COL_LENGTH('dbo.Orders', 'Status') IS NULL THEN 0 ELSE 1 END;
SELECT
    -- Revenue
    ISNULL((
        SELECT SUM(o.TotalAmount)
        FROM dbo.Orders o
        WHERE o.OrderDate >= @prevFrom AND o.OrderDate < @prevTo
          AND (@hasStatusCol = 0 OR LOWER(ISNULL(CONVERT(nvarchar(50), o.Status), N'')) IN (N'paid', N''))
    ), 0) AS RevenueLastMonth,
    ISNULL((
        SELECT SUM(o.TotalAmount)
        FROM dbo.Orders o
        WHERE o.OrderDate >= @thisFrom AND o.OrderDate < @thisTo
          AND (@hasStatusCol = 0 OR LOWER(ISNULL(CONVERT(nvarchar(50), o.Status), N'')) IN (N'paid', N''))
    ), 0) AS RevenueThisMonth,
    -- Orders count
    ISNULL((
        SELECT COUNT(*)
        FROM dbo.Orders o
        WHERE o.OrderDate >= @prevFrom AND o.OrderDate < @prevTo
          AND (@hasStatusCol = 0 OR LOWER(ISNULL(CONVERT(nvarchar(50), o.Status), N'')) IN (N'paid', N''))
    ), 0) AS OrdersLastMonth,
    ISNULL((
        SELECT COUNT(*)
        FROM dbo.Orders o
        WHERE o.OrderDate >= @thisFrom AND o.OrderDate < @thisTo
          AND (@hasStatusCol = 0 OR LOWER(ISNULL(CONVERT(nvarchar(50), o.Status), N'')) IN (N'paid', N''))
    ), 0) AS OrdersThisMonth,
    -- Products sold (sum quantity from order details)
    ISNULL((
        SELECT SUM(od.Quantity)
        FROM dbo.Orders o
        INNER JOIN dbo.OrderDetails od ON od.OrderID = o.OrderID
        WHERE o.OrderDate >= @prevFrom AND o.OrderDate < @prevTo
          AND (@hasStatusCol = 0 OR LOWER(ISNULL(CONVERT(nvarchar(50), o.Status), N'')) IN (N'paid', N''))
    ), 0) AS ProductsSoldLastMonth,
    ISNULL((
        SELECT SUM(od.Quantity)
        FROM dbo.Orders o
        INNER JOIN dbo.OrderDetails od ON od.OrderID = o.OrderID
        WHERE o.OrderDate >= @thisFrom AND o.OrderDate < @thisTo
          AND (@hasStatusCol = 0 OR LOWER(ISNULL(CONVERT(nvarchar(50), o.Status), N'')) IN (N'paid', N''))
    ), 0) AS ProductsSoldThisMonth;";

        await using (var cmd = new SqlCommand(monthlySql, conn))
        {
            cmd.Parameters.AddWithValue("@prevFrom", prevMonthStart);
            cmd.Parameters.AddWithValue("@prevTo", thisMonthStart);
            cmd.Parameters.AddWithValue("@thisFrom", thisMonthStart);
            cmd.Parameters.AddWithValue("@thisTo", nextMonthStart);

            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                vm.RevenueLastMonth = reader["RevenueLastMonth"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["RevenueLastMonth"]);
                vm.RevenueThisMonth = reader["RevenueThisMonth"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["RevenueThisMonth"]);

                vm.OrdersLastMonth = reader["OrdersLastMonth"] == DBNull.Value ? 0 : Convert.ToInt32(reader["OrdersLastMonth"]);
                vm.OrdersThisMonth = reader["OrdersThisMonth"] == DBNull.Value ? 0 : Convert.ToInt32(reader["OrdersThisMonth"]);

                vm.ProductsSoldLastMonth = reader["ProductsSoldLastMonth"] == DBNull.Value ? 0 : Convert.ToInt32(reader["ProductsSoldLastMonth"]);
                vm.ProductsSoldThisMonth = reader["ProductsSoldThisMonth"] == DBNull.Value ? 0 : Convert.ToInt32(reader["ProductsSoldThisMonth"]);
            }
        }

        // profit = revenue * 0.8
        vm.IncomeLastMonth = vm.RevenueLastMonth * 0.8m;
        vm.IncomeThisMonth = vm.RevenueThisMonth * 0.8m;
        if (vm.IncomeLastMonth < 0) vm.IncomeLastMonth = 0;
        if (vm.IncomeThisMonth < 0) vm.IncomeThisMonth = 0;

        vm.LastMonthLabel = prevMonthStart.ToString("MMM yyyy", CultureInfo.InvariantCulture);
        vm.ThisMonthLabel = thisMonthStart.ToString("MMM yyyy", CultureInfo.InvariantCulture);

        // 4) Top products (best-selling by quantity) for current month,
        // plus revenue change (%) vs previous month.
        const string topProductsSql = @"
DECLARE @hasStatusCol INT = CASE WHEN COL_LENGTH('dbo.Orders', 'Status') IS NULL THEN 0 ELSE 1 END;

;WITH curr AS (
    SELECT
        od.ProductID,
        SUM(od.Quantity) AS QuantitySold,
        SUM(od.Quantity * od.UnitPrice) AS Revenue
    FROM dbo.Orders o
    INNER JOIN dbo.OrderDetails od ON od.OrderID = o.OrderID
    WHERE o.OrderDate >= @from AND o.OrderDate < @to
      AND (@hasStatusCol = 0 OR LOWER(ISNULL(CONVERT(nvarchar(50), o.Status), N'')) IN (N'paid', N''))
    GROUP BY od.ProductID
), prev AS (
    SELECT
        od.ProductID,
        SUM(od.Quantity * od.UnitPrice) AS PrevRevenue
    FROM dbo.Orders o
    INNER JOIN dbo.OrderDetails od ON od.OrderID = o.OrderID
    WHERE o.OrderDate >= @prevFrom AND o.OrderDate < @prevTo
      AND (@hasStatusCol = 0 OR LOWER(ISNULL(CONVERT(nvarchar(50), o.Status), N'')) IN (N'paid', N''))
    GROUP BY od.ProductID
)
SELECT TOP 10
    c.ProductID,
    ISNULL(p.Name, N'') AS Name,
    c.QuantitySold,
    c.Revenue,
    CASE
        WHEN ISNULL(pr.PrevRevenue, 0) = 0
            THEN CASE WHEN ISNULL(c.Revenue, 0) = 0 THEN 0 ELSE 100 END
        ELSE ((c.Revenue - pr.PrevRevenue) / pr.PrevRevenue) * 100
    END AS RevenueChangePercent
FROM curr c
LEFT JOIN prev pr ON pr.ProductID = c.ProductID
LEFT JOIN dbo.Products p ON p.ProductID = c.ProductID
ORDER BY c.QuantitySold DESC, c.Revenue DESC;";

        await using (var cmd = new SqlCommand(topProductsSql, conn))
        {
            cmd.Parameters.AddWithValue("@from", thisMonthStart);
            cmd.Parameters.AddWithValue("@to", nextMonthStart);
            cmd.Parameters.AddWithValue("@prevFrom", prevMonthStart);
            cmd.Parameters.AddWithValue("@prevTo", thisMonthStart);

            await using var reader = await cmd.ExecuteReaderAsync();
            var list = new List<DashboardTopProductVm>();
            var rank = 1;
            while (await reader.ReadAsync())
            {
                var productId = reader["ProductID"] == DBNull.Value ? 0 : Convert.ToInt32(reader["ProductID"]);
                var name = reader["Name"]?.ToString() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(name)) name = $"Product #{productId}";

                var qty = reader["QuantitySold"] == DBNull.Value ? 0 : Convert.ToInt32(reader["QuantitySold"]);
                var revenue = reader["Revenue"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["Revenue"]);
                var revenueChangePercent = reader["RevenueChangePercent"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["RevenueChangePercent"]);

                list.Add(new DashboardTopProductVm
                {
                    Rank = rank++,
                    ProductId = productId,
                    Name = name,
                    QuantitySold = qty,
                    Revenue = revenue,
                    RevenueChangePercent = revenueChangePercent
                });
            }

            vm.TopProducts = list;
        }

        return vm;
    }
}

