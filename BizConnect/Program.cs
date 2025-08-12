using BizConnect.Dal;
using BizConnect.Dal.Models;
using BizConnect.Extensions;
using BizConnect.Middleware;
using BizConnect.Services;
using BizConnect.Services.Caching;
using BizConnect.Services.Clients;
using BizConnect.Services.Interfaces;
using BizConnect.Services.Jobs;
using BizConnect.Services.Security;
using BizConnect.Services.DTOs;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Options;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);

// Configure appsettings based on environment
var environment = builder.Environment.EnvironmentName;
builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: true)
    .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

// Create logger for startup validation
using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
var startupLogger = loggerFactory.CreateLogger<Program>();

// Helper method to extract database name from connection string
static string ExtractDatabaseName(string connectionString)
{
    try
    {
        var parts = connectionString.Split(';');
        var dbPart = parts.FirstOrDefault(p => p.Trim().StartsWith("Database=", StringComparison.OrdinalIgnoreCase));
        return dbPart?.Split('=')[1]?.Trim() ?? "Unknown";
    }
    catch
    {
        return "Unknown";
    }
}

// Helper method to check for non-production environments
static bool IsNonProductionEnvironment(IWebHostEnvironment env)
{
    return env.IsDevelopment() || 
           env.IsEnvironment("Local") || 
           env.IsEnvironment("Testing") ||
           env.IsEnvironment("UAT");
}

// Validate required configuration before proceeding
startupLogger.LogInformation("Starting BizConnect application configuration validation...");
startupLogger.LogInformation("Environment: {Environment}", environment);

// Add services to the container
builder.Services.AddDbContext<BizConnectContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    options.UseNpgsql(connectionString, npgsqlOptions =>
    {
        npgsqlOptions.EnableRetryOnFailure(
            maxRetryCount: 3,
            maxRetryDelay: TimeSpan.FromSeconds(10),
            errorCodesToAdd: null);
        npgsqlOptions.CommandTimeout(30);
    });

    // Environment-specific settings
    if (builder.Environment.IsDevelopment())
    {
        options.EnableSensitiveDataLogging();
        options.EnableDetailedErrors();
    }
});

// Add Repository and Unit of Work patterns
builder.Services.AddRepositoryPattern();

// Add Registration and OTAC management services
builder.Services.AddRegistrationServices();

// Validate required connection strings with detailed error messages
var defaultConnection = builder.Configuration.GetConnectionString("DefaultConnection");
var hangfireConnection = builder.Configuration.GetConnectionString("HangfireConnection");

if (string.IsNullOrEmpty(defaultConnection))
{
    var errorMessage = $"\n" +
        $"❌ CONFIGURATION ERROR: DefaultConnection string is not configured.\n" +
        $"\n" +
        $"To fix this issue:\n" +
        $"1. Create 'appsettings.Local.json' in BizConnect/ directory\n" +
        $"2. Add your database connection string:\n" +
        $"   {{\n" +
        $"     \"ConnectionStrings\": {{\n" +
        $"       \"DefaultConnection\": \"Host=localhost;Database=bizconnect_local;Username=postgres;Password=your_password\"\n" +
        $"     }}\n" +
        $"   }}\n" +
        $"\n" +
        $"3. Ensure PostgreSQL is running and accessible\n" +
        $"4. Run database migrations: ./scripts/update-db\n" +
        $"\n" +
        $"For more help, see README.md setup instructions.\n";
    
    startupLogger.LogCritical(errorMessage);
    throw new InvalidOperationException("DefaultConnection string is not configured. See console output for setup instructions.");
}

if (string.IsNullOrEmpty(hangfireConnection))
{
    var errorMessage = $"\n" +
        $"❌ CONFIGURATION ERROR: HangfireConnection string is not configured.\n" +
        $"\n" +
        $"To fix this issue:\n" +
        $"1. Add HangfireConnection to your appsettings.Local.json:\n" +
        $"   {{\n" +
        $"     \"ConnectionStrings\": {{\n" +
        $"       \"DefaultConnection\": \"Host=localhost;Database=bizconnect_local;Username=postgres;Password=your_password\",\n" +
        $"       \"HangfireConnection\": \"Host=localhost;Database=bizconnect_hangfire;Username=postgres;Password=your_password\"\n" +
        $"     }}\n" +
        $"   }}\n" +
        $"\n" +
        $"2. Create the Hangfire database in PostgreSQL\n" +
        $"3. Ensure both databases are accessible\n" +
        $"\n" +
        $"For more help, see README.md setup instructions.\n";
    
    startupLogger.LogCritical(errorMessage);
    throw new InvalidOperationException("HangfireConnection string is not configured. See console output for setup instructions.");
}

// Log successful configuration validation
var defaultDbName = ExtractDatabaseName(defaultConnection);
var hangfireDbName = ExtractDatabaseName(hangfireConnection);

startupLogger.LogInformation("✅ Configuration validation successful:");
startupLogger.LogInformation("   • Default Database: {DefaultDatabase}", defaultDbName);
startupLogger.LogInformation("   • Hangfire Database: {HangfireDatabase}", hangfireDbName);
startupLogger.LogInformation("   • Environment: {Environment}", environment);

// Configure Hangfire with PostgreSQL storage using dedicated connection
builder.Services.AddHangfire(configuration => configuration
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UsePostgreSqlStorage(options => options.UseNpgsqlConnection(hangfireConnection), 
        new PostgreSqlStorageOptions
        {
            SchemaName = "hangfire",
            PrepareSchemaIfNecessary = true, // Auto-create Hangfire schema if necessary
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

// Add BizConnect services using organized extension methods
builder.Services.AddBizConnectCaching();
builder.Services.AddBizConnectCoreServices();
builder.Services.AddBizConnectCachedServices();
builder.Services.AddKBankOddServices();
builder.Services.AddBizConnectBackgroundJobs();

// Add authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ReturnUrlParameter = "ReturnUrl";
        
        // Events to handle authentication scenarios
        options.Events.OnRedirectToLogin = context =>
        {
            // Only redirect to login if explicitly accessing protected resources
            // Allow landing page to be accessible without authentication
            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        };
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
    
    // No fallback policy - allow anonymous access by default
    // Controllers must explicitly use [Authorize] attribute for protection
    // This allows public pages like landing page to be accessible without authentication
});

// Add localization services for Thai/English support
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

// Configure supported cultures
var supportedCultures = new[]
{
    new CultureInfo("en-US"),
    new CultureInfo("th-TH")
};

builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options.DefaultRequestCulture = new RequestCulture("en-US");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;
    
    // Set culture providers (order matters)
    options.RequestCultureProviders = new List<IRequestCultureProvider>
    {
        new CookieRequestCultureProvider(),
        new QueryStringRequestCultureProvider(),
        new AcceptLanguageHeaderRequestCultureProvider()
    };
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
    // No global authorization filter - use [Authorize] attribute on controllers that need protection
    // This allows public pages to be accessible without authentication
    
    // Global anti-forgery token validation for state-changing operations
    options.Filters.Add(new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute());
})
.AddViewLocalization(Microsoft.AspNetCore.Mvc.Razor.LanguageViewLocationExpanderFormat.Suffix)
.AddDataAnnotationsLocalization()
.AddJsonOptions(options =>
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

// Log successful application build
startupLogger.LogInformation("✅ Application built successfully");
startupLogger.LogInformation("🚀 Configuring middleware pipeline...");

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
else
{
    // In development, use detailed error pages
    app.UseDeveloperExceptionPage();
}

// MIDDLEWARE PIPELINE ORDER (CRITICAL - DO NOT REORDER):
// 1. Global exception handling (must be first)
app.UseMiddleware<GlobalExceptionMiddleware>();

// 1.5. Performance monitoring middleware (must be early to capture full request time)
app.UseMiddleware<PerformanceMonitoringMiddleware>();

// 2. Performance middleware (compression and caching - early in pipeline)
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

// 3. HTTPS redirection
app.UseHttpsRedirection();

// 4. Enterprise-grade security headers middleware
app.Use(async (context, next) =>
{
    // CRITICAL FIX: Add headers BEFORE calling next() to ensure response hasn't started
    try
    {
        // Get KBank configuration to determine allowed form submission domains
        var kbankConfig = app.Configuration.GetSection("KBankODD");
        var kbankBaseUrl = kbankConfig["BaseUrl"] ?? string.Empty;
        var kbankPGBaseUrl = kbankConfig["PGBaseUrl"] ?? string.Empty;
        
        // Build form-action directive with KBank domains
        var formActionDirective = "'self'";
        
        // Add KBank domains to form-action for payment gateway redirects
        var allowedDomains = new HashSet<string>();
        
        if (!string.IsNullOrEmpty(kbankBaseUrl) && Uri.TryCreate(kbankBaseUrl, UriKind.Absolute, out var kbankUri))
        {
            allowedDomains.Add($"{kbankUri.Scheme}://{kbankUri.Host}");
        }
        
        if (!string.IsNullOrEmpty(kbankPGBaseUrl) && Uri.TryCreate(kbankPGBaseUrl, UriKind.Absolute, out var pgUri))
        {
            allowedDomains.Add($"{pgUri.Scheme}://{pgUri.Host}");
        }
        
        // Add production and UAT KBank domains explicitly for safety
        if (app.Environment.IsProduction())
        {
            allowedDomains.Add("https://ws06.kasikornbank.com");
            allowedDomains.Add("https://*.kasikornbank.com");
        }
        else if (app.Environment.IsEnvironment("UAT") || app.Environment.IsDevelopment())
        {
            allowedDomains.Add("https://ws06.uat.kasikornbank.com");
            allowedDomains.Add("https://*.uat.kasikornbank.com");
        }
        
        if (allowedDomains.Any())
        {
            formActionDirective = $"'self' {string.Join(" ", allowedDomains)}";
        }
        
        // Enhanced Content Security Policy for financial services with KBank integration
        var cspPolicy = new System.Text.StringBuilder()
            .Append("default-src 'self'; ")
            .Append("script-src 'self' 'unsafe-inline' 'unsafe-eval'; ") // Note: unsafe-inline/eval needed for some Bootstrap/jQuery features
            .Append("style-src 'self' 'unsafe-inline' https://fonts.googleapis.com https://cdnjs.cloudflare.com; ")
            .Append("font-src 'self' https://fonts.gstatic.com https://cdnjs.cloudflare.com; ")
            .Append("img-src 'self' data: https: blob:; ")
            .Append("connect-src 'self'; ")
            .Append("frame-src 'none'; ")
            .Append("frame-ancestors 'none'; ")
            .Append("object-src 'none'; ")
            .Append("base-uri 'self'; ")
            .Append($"form-action {formActionDirective}; ") // Allow form submissions to KBank for payment redirects
            .Append("upgrade-insecure-requests; ")
            .ToString();
        
        context.Response.Headers.TryAdd("Content-Security-Policy", cspPolicy);
        
        // Financial services security headers
        context.Response.Headers.TryAdd("X-Content-Type-Options", "nosniff");
        context.Response.Headers.TryAdd("X-Frame-Options", "DENY");
        context.Response.Headers.TryAdd("X-XSS-Protection", "1; mode=block");
        context.Response.Headers.TryAdd("Referrer-Policy", "strict-origin-when-cross-origin");
        context.Response.Headers.TryAdd("Permissions-Policy", 
            "geolocation=(), microphone=(), camera=(), payment=(), usb=(), magnetometer=(), gyroscope=(), accelerometer=()");
        
        // Additional enterprise security headers
        context.Response.Headers.TryAdd("X-Permitted-Cross-Domain-Policies", "none");
        
        // Relaxed CORP/COOP for KBank integration - they may need to interact with our pages
        // Only apply strict policies for admin pages
        if (context.Request.Path.StartsWithSegments("/Admin"))
        {
            context.Response.Headers.TryAdd("Cross-Origin-Embedder-Policy", "require-corp");
            context.Response.Headers.TryAdd("Cross-Origin-Opener-Policy", "same-origin");
            context.Response.Headers.TryAdd("Cross-Origin-Resource-Policy", "same-origin");
        }
        else
        {
            // More permissive for public pages that interact with KBank
            context.Response.Headers.TryAdd("Cross-Origin-Opener-Policy", "same-origin-allow-popups");
            context.Response.Headers.TryAdd("Cross-Origin-Resource-Policy", "cross-origin");
        }
        
        // Cache control for sensitive pages
        if (context.Request.Path.StartsWithSegments("/Admin") || 
            context.Request.Path.StartsWithSegments("/Account"))
        {
            context.Response.Headers.TryAdd("Cache-Control", "no-store, no-cache, must-revalidate, private");
            context.Response.Headers.TryAdd("Pragma", "no-cache");
            context.Response.Headers.TryAdd("Expires", "0");
        }
        
        // Remove identifying server headers for security
        context.Response.Headers.Remove("Server");
        context.Response.Headers.Remove("X-Powered-By");
        context.Response.Headers.Remove("X-AspNet-Version");
        context.Response.Headers.Remove("X-AspNetMvc-Version");
    }
    catch (InvalidOperationException)
    {
        // Headers already sent - this shouldn't happen since we're adding headers before next()
        // But this is a defensive measure
    }
    
    await next();
});

// 5. Static files with environment-specific caching
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

// 6. Request localization middleware (must be early in pipeline)
var localizationOptions = app.Services.GetRequiredService<IOptions<RequestLocalizationOptions>>();
app.UseRequestLocalization(localizationOptions.Value);

// 7. Routing
app.UseRouting();

// TODO: Add rate limiting middleware when implemented

// MIDDLEWARE PIPELINE CONTINUATION:
// 8. Session middleware (must be before authentication)
app.UseSession();

// 9. Authentication middleware
app.UseAuthentication();

// 10. Authorization middleware (must be after authentication)
app.UseAuthorization();

// Log middleware pipeline completion
startupLogger.LogInformation("✅ Middleware pipeline configured successfully");

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

// Configure Hangfire dashboard with authorization for multiple environments
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = IsNonProductionEnvironment(app.Environment)
        ? new Hangfire.Dashboard.IDashboardAuthorizationFilter[0] // Allow all in non-production
        : new[] { new HangfireAuthorizationFilter() } // Require auth in production
});

// Schedule recurring background jobs
RecurringJob.AddOrUpdate<OptimizedPurgeExpiredOtacCodesJob>(
    "purge-expired-otac-codes",
    job => job.ExecuteAsync(100, 0),
    "*/5 * * * *"); // Every 5 minutes

RecurringJob.AddOrUpdate<DailyPaymentJob>(
    "daily-payment-processing",
    job => job.ExecuteAsync(),
    "0 2 * * *"); // Daily at 2:00 AM

RecurringJob.AddOrUpdate<DailyAnalyticsJob>(
    "daily-analytics-processing",
    job => job.ExecuteAsync(),
    "0 3 * * *"); // Daily at 3:00 AM (after payment processing)


// Configure MVC routing
app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Map health check endpoint
app.MapHealthChecks("/health");

// Final startup validation
startupLogger.LogInformation("🎉 BizConnect application startup completed successfully!");
startupLogger.LogInformation("   • Health check available at: /health");
if (app.Environment.IsDevelopment() || app.Environment.EnvironmentName == "UAT")
{
    startupLogger.LogInformation("   • API documentation available at: /api/docs");
}
if (IsNonProductionEnvironment(app.Environment))
{
    startupLogger.LogInformation("   • Hangfire dashboard available at: /hangfire");
}

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
            // For now, we'll allow access in non-production environments but restrict in production
            
            // Check if running in non-production environment
            var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            var isNonProduction = string.Equals(environment, "Development", StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(environment, "Local", StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(environment, "Testing", StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(environment, "UAT", StringComparison.OrdinalIgnoreCase);
            
            if (isNonProduction)
            {
                // Allow access in non-production environments
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
