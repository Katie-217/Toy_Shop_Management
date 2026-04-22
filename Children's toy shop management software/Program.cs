var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.AddSingleton<Children_s_toy_shop_management_software.Data.IDbConnectionFactory,
    Children_s_toy_shop_management_software.Data.SqlConnectionFactory>();

builder.Services.AddScoped<Children_s_toy_shop_management_software.Data.ProductsRepository>();
builder.Services.AddScoped<Children_s_toy_shop_management_software.Data.StaffRepository>();
builder.Services.AddScoped<Children_s_toy_shop_management_software.Data.CustomersRepository>();
builder.Services.AddScoped<Children_s_toy_shop_management_software.Data.ReportsRepository>();
builder.Services.AddScoped<Children_s_toy_shop_management_software.Data.InventoryRepository>();
builder.Services.AddScoped<Children_s_toy_shop_management_software.Data.PosRepository>();
builder.Services.AddScoped<Children_s_toy_shop_management_software.Data.DashboardRepository>();
builder.Services.AddScoped<Children_s_toy_shop_management_software.Data.AccountRepository>();

builder.Services.AddAuthentication(Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
    });

var app = builder.Build();

app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Exception ex)
    {
        context.Response.StatusCode = 500;
        context.Response.ContentType = "text/plain";
        await context.Response.WriteAsync("GLOBAL_ERROR: " + ex.GetType().Name + " - " + ex.Message + "\r\n" + ex.StackTrace);
    }
});

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

app.Run();
