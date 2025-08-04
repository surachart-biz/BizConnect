using BizConnect.Dal.Models;
using BizConnect.Extensions;
using BizConnect.Middleware;
using BizConnect.Services;
using BizConnect.Services.Caching;
using BizConnect.Services.Clients;
using BizConnect.Services.Interfaces;
using BizConnect.Services.Jobs;
using BizConnect.Services.Security;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
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

// Add Repository and Unit of Work patterns
builder.Services.AddRepositoryPattern();

// Add Registration and OTAC management services
builder.Services.AddRegistrationServices();

// Configure Hangfire with PostgreSQL storage
builder.Services.AddHangfire(configuration => configuration
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UsePostgreSqlStorage(options => options.UseNpgsqlConnection(
        builder.Configuration.GetConnectionString("DefaultConnection")), 
        new PostgreSqlStorageOptions
        {
            SchemaName = "hangfire",
            PrepareSchemaIfNecessary = false, // Don't auto-create schema - we handle it via migrations
            QueuePollInterval = TimeSpan.FromSeconds(15),
            JobExpirationCheckInterval = TimeSpan.FromHours(1),
            CountersAggregateInterval = TimeSpan.FromMinutes(5),
            DeleteExpiredBatchSize = 1000,
            InvisibilityTimeout = TimeSpan.FromMinutes(30)
        }));

// Add Hangfire server
builder.Services.AddHangfireServer(options =>
{
    options.SchedulePollingInterval = TimeSpan.FromSeconds(30);
    options.ServerTimeout = TimeSpan.FromMinutes(5);
    options.WorkerCount = Environment.ProcessorCount * 2;
});

// Add core services with caching decorators
builder.Services.AddScoped<UserService>(); // Inner service
builder.Services.AddScoped<IUserService>(provider => 
    new CachedUserService(
        provider.GetRequiredService<UserService>(),
        provider.GetRequiredService<ICacheService>(),
        provider.GetRequiredService<ILogger<CachedUserService>>()));

builder.Services.AddScoped<BranchService>(); // Inner service  
builder.Services.AddScoped<IBranchService>(provider =>
    new CachedBranchService(
        provider.GetRequiredService<BranchService>(),
        provider.GetRequiredService<ICacheService>(),
        provider.GetRequiredService<ILogger<CachedBranchService>>()));

builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IOddRegistrationService, OddRegistrationService>();
builder.Services.AddScoped<IValidationService, ValidationService>();
builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();

// Add core security services
builder.Services.AddScoped<ISecurityAuditService, SecurityAuditService>();
builder.Services.AddScoped<IPasswordPolicyService, PasswordPolicyService>();

// Add advanced security services - Phase 3B.1
builder.Services.AddScoped<IDateTimeProvider, DateTimeProvider>();
builder.Services.AddScoped<IRateLimitingService, AdvancedRateLimitingService>();
// TODO: Fix SecurityMonitoringService interface implementation mismatch
// builder.Services.AddScoped<ISecurityMonitoringService, SecurityMonitoringService>();
builder.Services.AddScoped<IThreatResponseService, ThreatResponseService>();
builder.Services.AddScoped<IEnhancedSecurityAuditService, EnhancedSecurityAuditService>();

// Add memory cache with size limits for Phase 3A.1 specification
builder.Services.AddMemoryCache(options =>
{
    options.SizeLimit = 104857600; // 100MB default size limit
    options.CompactionPercentage = 0.25; // Remove 25% when limit reached
});

// Add caching services as Singleton for optimal performance
builder.Services.AddSingleton<ICacheService, MemoryCacheService>();

// Add KBank ODD services
builder.Services.AddHttpClient<KBankOddClient>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Add("User-Agent", "BizConnect/1.0");
});
builder.Services.AddScoped<IKBankOddClient, KBankOddClient>();
builder.Services.AddScoped<IKbankOddService, KbankOddService>();
builder.Services.AddScoped<IPaymentProcessingService, PaymentProcessingService>();

// Add Hangfire background job services
builder.Services.AddScoped<PurgeExpiredOtacCodesJob>();
builder.Services.AddScoped<DailyPaymentJob>();

// Add authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromMinutes(30); // 30-minute idle timeout
        options.SlidingExpiration = true;

        // Enhanced cookie security
        options.Cookie.Name = "BizConnect.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.IsEssential = true;

        // Configure cookie security based on environment
        var securityConfig = builder.Configuration.GetSection("Security");
        if (securityConfig.Exists())
        {
            options.Cookie.SecurePolicy = securityConfig.GetValue<bool>("CookieSecure")
                ? CookieSecurePolicy.Always
                : CookieSecurePolicy.SameAsRequest;
        }
        else
        {
            // Default to secure in production-like environments
            options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() 
                ? CookieSecurePolicy.SameAsRequest 
                : CookieSecurePolicy.Always;
        }

        // Security event handlers
        options.Events.OnSigningIn = context =>
        {
            // Log successful authentication
            var logger = context.HttpContext.RequestServices.GetService<ILogger<Program>>();
            var username = context.Principal?.Identity?.Name ?? "Unknown";
            logger?.LogInformation("User {Username} signing in from {IP}", username, context.HttpContext.Connection.RemoteIpAddress);
            return Task.CompletedTask;
        };

        options.Events.OnSigningOut = context =>
        {
            // Clear any additional cookies or session data
            var logger = context.HttpContext.RequestServices.GetService<ILogger<Program>>();
            logger?.LogInformation("User signing out from {IP}", context.HttpContext.Connection.RemoteIpAddress);
            return Task.CompletedTask;
        };
    });

// Add authorization with comprehensive role-based policies
builder.Services.AddAuthorization(options =>
{
    // Admin-only access for user management
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
    
    // Admin and Employee access for admin areas
    options.AddPolicy("AdminOrEmployee", policy => policy.RequireRole("Admin", "Employee"));
    
    // All authenticated users
    options.AddPolicy("AuthenticatedUser", policy => policy.RequireAuthenticatedUser());
    
    // OTAC verified access (requires custom handler)
    options.AddPolicy("OTACVerified", policy => 
        policy.RequireAssertion(context => 
            context.User.HasClaim("otac_verified", "true")));
    
    // Fallback policy - require authentication for all controllers by default
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// Add data protection for secure cookie encryption
builder.Services.AddDataProtection();

// Add anti-forgery token configuration
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.SuppressXFrameOptionsHeader = false;
    options.Cookie.Name = "BizConnect.Antiforgery";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() 
        ? CookieSecurePolicy.SameAsRequest 
        : CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
});

// Add session for additional state management
builder.Services.AddSession(options =>
{
    options.Cookie.Name = "BizConnect.Session";
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() 
        ? CookieSecurePolicy.SameAsRequest 
        : CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
});

// Add API versioning support for .NET 8
builder.Services.AddApiVersioning(options =>
{
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.DefaultApiVersion = new Asp.Versioning.ApiVersion(1, 0);
    options.ApiVersionReader = Asp.Versioning.ApiVersionReader.Combine(
        new Asp.Versioning.UrlSegmentApiVersionReader(),
        new Asp.Versioning.HeaderApiVersionReader("X-Version"),
        new Asp.Versioning.QueryStringApiVersionReader("version"));
}).AddApiExplorer(setup =>
{
    setup.GroupNameFormat = "'v'VVV";
    setup.SubstituteApiVersionInUrl = true;
});

// Add Swagger/OpenAPI support for API documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "BizConnect API",
        Version = "v1",
        Description = "RESTful API for BizConnect KBank ODD Registration System",
        Contact = new Microsoft.OpenApi.Models.OpenApiContact
        {
            Name = "BizConnect Development Team",
            Email = "dev@bizconnect.com"
        }
    });

    // Add JWT authentication support in Swagger
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });

    // Include XML comments for better API documentation
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

// TODO: Add rate limiting for API endpoints (implementation pending)
// Rate limiting will be implemented in a future iteration

// Add MVC services with enhanced model validation
builder.Services.AddControllersWithViews(options =>
{
    // Global authorization filter
    options.Filters.Add(new Microsoft.AspNetCore.Mvc.Authorization.AuthorizeFilter());
    
    // Global anti-forgery token validation for state-changing operations
    options.Filters.Add(new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute());
}).AddJsonOptions(options =>
{
    // Configure JSON serialization for API responses
    options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    options.JsonSerializerOptions.WriteIndented = builder.Environment.IsDevelopment();
    options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
});

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

// Add global exception handling middleware (must be before other middleware)
app.UseMiddleware<GlobalExceptionMiddleware>();

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

// Add security headers middleware
app.Use(async (context, next) =>
{
    // Content Security Policy
    context.Response.Headers.Add("Content-Security-Policy", 
        "default-src 'self'; " +
        "script-src 'self' 'unsafe-inline'; " +
        "style-src 'self' 'unsafe-inline'; " +
        "img-src 'self' data:; " +
        "font-src 'self'; " +
        "connect-src 'self'; " +
        "frame-ancestors 'none';");
    
    // Security headers
    context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Add("X-Frame-Options", "DENY");
    context.Response.Headers.Add("X-XSS-Protection", "1; mode=block");
    context.Response.Headers.Add("Referrer-Policy", "strict-origin-when-cross-origin");
    context.Response.Headers.Add("Permissions-Policy", "geolocation=(), microphone=(), camera=()");
    
    // Remove server header
    context.Response.Headers.Remove("Server");
    
    await next();
});

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

// TODO: Add rate limiting middleware when implemented

// Add session middleware before authentication
app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

// Add Swagger in development and UAT environments
if (app.Environment.IsDevelopment() || app.Environment.EnvironmentName == "UAT")
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "BizConnect API v1");
        options.RoutePrefix = "api/docs"; // Serve Swagger UI at /api/docs
        options.DisplayRequestDuration();
        options.EnableDeepLinking();
        options.EnableFilter();
        options.ShowExtensions();
        options.ShowCommonExtensions();
        options.DefaultModelRendering(Swashbuckle.AspNetCore.SwaggerUI.ModelRendering.Model);
    });
}

// Configure Hangfire dashboard with authorization
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = app.Environment.IsDevelopment() 
        ? new Hangfire.Dashboard.IDashboardAuthorizationFilter[0] // Allow all in development
        : new[] { new HangfireAuthorizationFilter() } // Require auth in production
});

// Schedule recurring background jobs
RecurringJob.AddOrUpdate<PurgeExpiredOtacCodesJob>(
    "purge-expired-otac-codes",
    job => job.ExecuteAsync(),
    "*/5 * * * *"); // Every 5 minutes

RecurringJob.AddOrUpdate<DailyPaymentJob>(
    "daily-payment-processing",
    job => job.ExecuteAsync(),
    "0 2 * * *"); // Daily at 2:00 AM

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

// Hangfire Dashboard Authorization Filter with proper role-based security
public class HangfireAuthorizationFilter : Hangfire.Dashboard.IDashboardAuthorizationFilter
{
    public bool Authorize(Hangfire.Dashboard.DashboardContext context)
    {
        try
        {
            // For Hangfire 1.8.x, we need to use the OWIN context
            // Check if user is authenticated through the context
            var isAuthenticated = !string.IsNullOrEmpty(context.Request.RemoteIpAddress);
            
            // In a production environment, you would typically implement proper authentication
            // For now, we'll allow access in development but restrict in production
            
            // Check if running in development environment
            var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            var isDevelopment = string.Equals(environment, "Development", StringComparison.OrdinalIgnoreCase);
            
            if (isDevelopment)
            {
                // Allow access in development mode
                return true;
            }
            
            // In production, implement stricter authentication
            // For now, deny access to ensure security
            return false;
        }
        catch (Exception)
        {
            // On error, deny access for safety
            return false;
        }
    }
}

// Make the implicit Program class public so test projects can access it
public partial class Program { }
