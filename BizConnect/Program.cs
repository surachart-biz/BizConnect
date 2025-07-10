using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using BizConnect.Dal.Data;
using BizConnect.Dal.Repositories;
using BizConnect.Services.Interfaces;
using BizConnect.Services.Services;
using BizConnect.Configuration;
using BizConnect.Middleware;
using Serilog;
using Serilog.Events;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "BizConnect")
    .Enrich.WithProperty("Environment", builder.Environment.EnvironmentName)
    .CreateLogger();

// Use Serilog as the logging provider
builder.Host.UseSerilog();

// Add environment variable substitution to configuration
builder.Configuration.AddEnvironmentVariableSubstitution();

// Add services to the container.
builder.Services.AddControllersWithViews();

// Configure Entity Framework with production settings
builder.Services.AddDbContext<BizConnectDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionStringWithSubstitution("DefaultConnection");
    options.UseNpgsql(connectionString, npgsqlOptions =>
    {
        npgsqlOptions.CommandTimeout(builder.Configuration.GetValue<int>("Database:CommandTimeout", 30));
        npgsqlOptions.EnableRetryOnFailure(
            maxRetryCount: builder.Configuration.GetValue<int>("Database:MaxRetryCount", 3),
            maxRetryDelay: TimeSpan.Parse(builder.Configuration.GetValue<string>("Database:MaxRetryDelay") ?? "00:00:30"),
            errorCodesToAdd: null);
    });

    // Configure logging and error handling based on environment
    if (builder.Environment.IsDevelopment())
    {
        if (builder.Configuration.GetValue<bool>("Database:EnableSensitiveDataLogging", false))
            options.EnableSensitiveDataLogging();
        if (builder.Configuration.GetValue<bool>("Database:EnableDetailedErrors", false))
            options.EnableDetailedErrors();
    }
});

// Add database connection service
builder.Services.AddDatabaseConnection(builder.Configuration);

// Register validation services
builder.Services.AddScoped<BizConnect.Dal.Validation.IEntityValidator, BizConnect.Dal.Validation.EntityValidator>();

// Register repositories
builder.Services.AddScoped<IUserRepository, UserRepository>();

// Register Unit of Work
builder.Services.AddScoped<BizConnect.Dal.UnitOfWork.IUnitOfWork, BizConnect.Dal.UnitOfWork.UnitOfWork>();

// Register services
builder.Services.AddScoped<IPasswordHashingService, PasswordHashingService>();
builder.Services.AddScoped<IPasswordMigrationService, PasswordMigrationService>();
builder.Services.AddScoped<BizConnect.Services.Interfaces.IAuthenticationService, BizConnect.Services.Services.AuthenticationService>();
builder.Services.AddScoped<IDatabaseHealthService, DatabaseHealthService>();

// Add rate limiting
builder.Services.AddRateLimiting(builder.Configuration);

// Add security configuration
builder.Services.AddSecurityConfiguration(builder.Configuration, builder.Environment);

// Add SSL certificate service
builder.Services.AddHttpClient<ISslCertificateService, SslCertificateService>();
builder.Services.AddScoped<ISslCertificateService, SslCertificateService>();

// Add input validation
builder.Services.AddInputValidation(builder.Configuration);

// Add audit services
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<BizConnect.Services.IAuditContextProvider, BizConnect.Services.HttpAuditContextProvider>();
builder.Services.AddSingleton<BizConnect.Services.Services.AuditService>();
builder.Services.AddSingleton<IAuditService, BizConnect.Services.EnhancedAuditService>();

// Add monitoring services
builder.Services.AddScoped<IMonitoringService, BizConnect.Services.Services.InMemoryMonitoringService>();
builder.Services.AddHostedService<BizConnect.Middleware.SystemMetricsCollectorService>();

// Add performance optimization services
builder.Services.AddPerformanceOptimization(builder.Configuration);

// Add user management services
builder.Services.AddScoped<BizConnect.Dal.Interfaces.IUserProfileRepository, BizConnect.Dal.Repositories.UserProfileRepository>();
builder.Services.AddScoped<BizConnect.Dal.Interfaces.IUserRoleRepository, BizConnect.Dal.Repositories.UserRoleRepository>();
builder.Services.AddScoped<BizConnect.Dal.Interfaces.IUserRoleAssignmentRepository, BizConnect.Dal.Repositories.UserRoleAssignmentRepository>();
builder.Services.AddScoped<IUserProfileService, BizConnect.Services.Services.UserProfileService>();
builder.Services.AddScoped<IUserManagementService, BizConnect.Services.Services.UserManagementService>();
builder.Services.AddScoped<IDashboardService, BizConnect.Services.Services.DashboardService>();
builder.Services.AddScoped<ILobbyService, BizConnect.Services.Services.LobbyService>();
// Password reset and email services removed - not needed for simplified business logic
builder.Services.AddScoped<IAuthorizationService, BizConnect.Services.Services.AuthorizationService>();

// Session management services
builder.Services.AddScoped<BizConnect.Dal.Interfaces.IUserSessionRepository, BizConnect.Dal.Repositories.UserSessionRepository>();
builder.Services.AddScoped<ISessionManagementService, BizConnect.Services.Services.SessionManagementService>();
builder.Services.Configure<SessionManagementOptions>(builder.Configuration.GetSection("SessionManagement"));

// Audit logging services
builder.Services.AddScoped<BizConnect.Dal.Interfaces.IAuditLogRepository, BizConnect.Dal.Repositories.AuditLogRepository>();

// System health monitoring services
builder.Services.AddScoped<ISystemHealthService, BizConnect.Services.Services.SystemHealthService>();

// Entity validation services
builder.Services.AddScoped<BizConnect.Dal.Validation.IEntityValidator, BizConnect.Dal.Validation.SimpleEntityValidator>();

// Update DashboardService registration to include new dependencies
// Note: The existing registration will be updated automatically by DI container

// Add health checks
builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("DefaultConnection")!, name: "database")
    .AddCheck<BizConnect.Services.Services.AuthenticationHealthCheck>("authentication")
    .AddCheck<BizConnect.Services.Services.PasswordHashingHealthCheck>("password_hashing")
    .AddCheck<BizConnect.Services.Services.RateLimitingHealthCheck>("rate_limiting")
    .AddCheck<BizConnect.Services.Services.InputValidationHealthCheck>("input_validation")
    .AddCheck<BizConnect.Services.Services.AuditServiceHealthCheck>("audit_service")
    .AddCheck<BizConnect.Services.Services.MemoryHealthCheck>("memory")
    .AddCheck<BizConnect.Services.Services.DiskSpaceHealthCheck>("disk_space");

// Add basic health checks
builder.Services.AddHealthChecks();

// Configure Authentication
var authConfig = builder.Configuration.GetSection("Authentication");
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = authConfig["CookieName"] ?? "BizConnect.Auth";
        options.LoginPath = authConfig["LoginPath"] ?? "/Account/Login";
        options.LogoutPath = authConfig["LogoutPath"] ?? "/Account/Logout";
        options.AccessDeniedPath = authConfig["AccessDeniedPath"] ?? "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.Parse(authConfig["ExpireTimeSpan"] ?? "01:00:00");
        options.SlidingExpiration = bool.Parse(authConfig["SlidingExpiration"] ?? "true");
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = builder.Environment.IsProduction() ?
            CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.IsEssential = true;

        // Enhanced session security
        options.Events.OnValidatePrincipal = async context =>
        {
            // Check for concurrent sessions and session timeout
            var sessionService = context.HttpContext.RequestServices.GetService<ISessionManagementService>();
            if (sessionService != null)
            {
                var userId = context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!string.IsNullOrEmpty(userId) && int.TryParse(userId, out int userIdInt))
                {
                    var isValidSession = await sessionService.ValidateSessionAsync(userIdInt, context.HttpContext);
                    if (!isValidSession)
                    {
                        context.RejectPrincipal();
                        await context.HttpContext.SignOutAsync();
                    }
                }
            }
        };
    });

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// Add Serilog request logging
app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
    options.GetLevel = (httpContext, elapsed, ex) => ex != null
        ? LogEventLevel.Error
        : httpContext.Response.StatusCode > 499
            ? LogEventLevel.Error
            : LogEventLevel.Information;
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
        diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
        diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent.FirstOrDefault());
        diagnosticContext.Set("ClientIP", httpContext.Connection.RemoteIpAddress?.ToString());

        if (httpContext.User?.Identity?.IsAuthenticated == true)
        {
            diagnosticContext.Set("UserId", httpContext.User.FindFirst("sub")?.Value);
            diagnosticContext.Set("Username", httpContext.User.Identity.Name);
        }
    };
});

// Add response compression (before other middleware)
app.UseResponseCompression();

// Add global exception handling (before other middleware)
app.UseGlobalExceptionHandling();

// Add performance optimization
app.UsePerformanceOptimization();

// Add performance monitoring
app.UsePerformanceMonitoring();

// Add security configuration (HTTPS, HSTS, security headers)
app.UseSecurityConfiguration(app.Environment);

app.UseStaticFiles();

// Add input validation middleware (before authentication)
app.UseInputValidation();

// Add rate limiting middleware (before authentication)
app.UseRateLimiting();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Add health check endpoints
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var response = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(x => new
            {
                name = x.Key,
                status = x.Value.Status.ToString(),
                description = x.Value.Description,
                duration = x.Value.Duration.TotalMilliseconds,
                data = x.Value.Data
            }),
            totalDuration = report.TotalDuration.TotalMilliseconds
        };
        await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(response, new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
        }));
    }
});

app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false // Only basic liveness check
});

app.Run();

// Make Program class accessible for testing
public partial class Program { }
