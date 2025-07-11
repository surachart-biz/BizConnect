using BizConnect.Dal;
using BizConnect.Services;
using BizConnect.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Configure appsettings based on environment
var environment = builder.Environment.EnvironmentName;
builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

// Add services to the container.
builder.Services.AddDbContext<BizConnectContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add services
builder.Services.AddScoped<IUserService, UserService>();

// Add authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;

        // Configure cookie security based on environment
        var securityConfig = builder.Configuration.GetSection("Security");
        if (securityConfig.Exists())
        {
            options.Cookie.SecurePolicy = securityConfig.GetValue<bool>("CookieSecure")
                ? CookieSecurePolicy.Always
                : CookieSecurePolicy.SameAsRequest;
        }
    });

// Add authorization
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
    options.AddPolicy("UserOrAdmin", policy => policy.RequireRole("User", "Admin"));
});

// Add MVC services
builder.Services.AddControllersWithViews();

// Add health checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<BizConnectContext>();

// Add performance optimizations based on environment configuration
var performanceConfig = builder.Configuration.GetSection("Performance");
if (performanceConfig.Exists())
{
    if (performanceConfig.GetValue<bool>("EnableResponseCaching"))
    {
        builder.Services.AddResponseCaching();
    }

    if (performanceConfig.GetValue<bool>("EnableResponseCompression"))
    {
        builder.Services.AddResponseCompression();
    }
}

// Configure HSTS options based on environment
var securityConfig = builder.Configuration.GetSection("Security");
if (securityConfig.Exists())
{
    var hstsMaxAge = securityConfig.GetValue<int?>("HstsMaxAge");
    if (hstsMaxAge.HasValue)
    {
        builder.Services.AddHsts(options =>
        {
            options.MaxAge = TimeSpan.FromSeconds(hstsMaxAge.Value);
            options.IncludeSubDomains = true;
            options.Preload = true;
        });
    }
}

var app = builder.Build();

// Configure the HTTP request pipeline based on environment
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");

    // Configure HSTS based on environment settings
    var appSecurityConfig = app.Configuration.GetSection("Security");
    if (appSecurityConfig.Exists() && appSecurityConfig.GetValue<bool>("RequireHttps"))
    {
        app.UseHsts();
    }
}

// Add performance middleware based on configuration
var appPerformanceConfig = app.Configuration.GetSection("Performance");
if (appPerformanceConfig.Exists())
{
    if (appPerformanceConfig.GetValue<bool>("EnableResponseCompression"))
    {
        app.UseResponseCompression();
    }

    if (appPerformanceConfig.GetValue<bool>("EnableResponseCaching"))
    {
        app.UseResponseCaching();
    }
}

app.UseHttpsRedirection();

// Configure static files with environment-specific caching
if (app.Environment.IsDevelopment())
{
    app.UseStaticFiles(new StaticFileOptions
    {
        OnPrepareResponse = ctx =>
        {
            ctx.Context.Response.Headers.Append("Cache-Control", "no-cache, no-store, must-revalidate");
            ctx.Context.Response.Headers.Append("Pragma", "no-cache");
            ctx.Context.Response.Headers.Append("Expires", "-1");
        }
    });
}
else
{
    app.UseStaticFiles();   // default caching; version hash in URLs will prevent staleness
}

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// Configure MVC routing
app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Map health check endpoint
app.MapHealthChecks("/health");

app.Run();

// Make the implicit Program class public so test projects can access it
public partial class Program { }
