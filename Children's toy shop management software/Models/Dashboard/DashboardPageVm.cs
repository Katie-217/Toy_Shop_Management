using System.Globalization;

namespace Children_s_toy_shop_management_software.Models.Dashboard;

public sealed class DashboardDayVm
{
    public DateTime Date { get; set; }
    public string Label { get; set; } = string.Empty; // e.g. Mon/Tue/...
    public int OrdersCount { get; set; }
    public decimal Revenue { get; set; }
    // Profit (NodeJS fallback): profit = revenue * 0.8 (clamped by 0).
    // Kept property name `Income` for backward compatibility with existing JS code.
    public decimal Income { get; set; }

    // Total quantity sold in that day.
    public int ProductsSold { get; set; }

    // Detail table (per tab): order status split, optional discount, top product name in bucket.
    public int OrdersCompleted { get; set; }
    public int OrdersPending { get; set; }
    public int OrdersCancelled { get; set; }
    public decimal DiscountAmount { get; set; }
    public string TopProductName { get; set; } = string.Empty;
}

public sealed class DashboardTopProductVm
{
    public int Rank { get; set; }
    public int ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int QuantitySold { get; set; }
    public decimal Revenue { get; set; }
    // Revenue change vs previous month (%). Positive => up, negative => down.
    public decimal RevenueChangePercent { get; set; }
}

public sealed class DashboardPageVm
{
    // Dashboard aggregates (use domain "Customers" for naming consistency)
    public int TotalCustomers { get; set; }
    public int NewCustomers { get; set; }
    public int TotalOrders { get; set; }
    public decimal RevenueTotal { get; set; }

    // For "Updated X ago"
    public DateTime? LastOrderDate { get; set; }

    // Backward-compatible fields (existing view JS can still work if needed)
    public decimal RevenueToday { get; set; }
    public decimal IncomeToday { get; set; }
    public int OrdersToday { get; set; }
    public int ReturnedToday { get; set; }
    public int LowStockCount { get; set; }
    public int CustomersCount { get; set; }

    // Charts + tables
    public List<DashboardDayVm> RevenueByDays { get; set; } = new();
    public decimal RevenueThisMonth { get; set; }
    public decimal RevenueLastMonth { get; set; }
    public decimal IncomeThisMonth { get; set; }
    public decimal IncomeLastMonth { get; set; }
    public int OrdersThisMonth { get; set; }
    public int OrdersLastMonth { get; set; }
    public int ProductsSoldThisMonth { get; set; }
    public int ProductsSoldLastMonth { get; set; }
    public string ThisMonthLabel { get; set; } = string.Empty;
    public string LastMonthLabel { get; set; } = string.Empty;
    public List<DashboardTopProductVm> TopProducts { get; set; } = new();

    // Side panel: trend for the last 4 months (used by "Monthly comparison").
    public List<DashboardDayVm> MonthTrendLast4 { get; set; } = new();

    // Dashboard UI state (used by MVC reload).
    public string ActiveMetric { get; set; } = "sales"; // sales | orders | profit | productsSold
    public string ActiveDetailBy { get; set; } = "week"; // week | month | quarter | year
}

