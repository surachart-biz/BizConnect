using BizConnect.Dal.Repositories;
using BizConnect.Dal.UnitOfWork;
using BizConnect.Services;
using BizConnect.Services.Interfaces;
using BizConnect.Services.Caching;
using BizConnect.Services.Clients;
using BizConnect.Services.Jobs;
using BizConnect.Services.Security;
using Microsoft.Extensions.DependencyInjection;

namespace BizConnect.Extensions;

/// <summary>
/// Extension methods for IServiceCollection to register Repository and Unit of Work patterns.
/// Provides clean, fluent configuration of data access layer dependencies.
/// </summary>
public static partial class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Repository and Unit of Work patterns with the dependency injection container.
    /// This method configures:
    /// - Generic Repository pattern as Scoped with Caching Decorator
    /// - Unit of Work pattern as Scoped
    /// - OptimizedRegistrationRepository for KbankOddRegistration
    /// - Proper lifetime management for database operations
    /// </summary>
    /// <param name="services">The service collection to configure</param>
    /// <returns>The service collection for method chaining</returns>
    public static IServiceCollection AddRepositoryPattern(this IServiceCollection services)
    {
        // Register base repository implementation
        services.AddScoped(typeof(Repository<>));
        
        // Register base repository (caching is handled at Services layer via CachedUserService, etc.)
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

        // Register specialized optimized repository for KbankOddRegistration
        services.AddScoped<OptimizedRegistrationRepository>();

        // Register Unit of Work interface and implementation
        // Using Scoped lifetime to ensure consistent transaction boundaries
        // within a single HTTP request or operation scope
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        return services;
    }

    /// <summary>
    /// Registers OTAC and Registration management services with the dependency injection container.
    /// This method configures all business logic services for KBank ODD registration workflow.
    /// </summary>
    /// <param name="services">The service collection to configure</param>
    /// <returns>The service collection for method chaining</returns>
    public static IServiceCollection AddRegistrationServices(this IServiceCollection services)
    {
        // Register utility services
        services.AddScoped<IDateTimeProvider, DateTimeProvider>();
        services.AddScoped<IOtacCodeGenerator, OtacCodeGenerator>();

        // Register business logic services
        services.AddScoped<IOtacManagementService, OtacManagementService>();
        services.AddScoped<IRegistrationManagementService, RegistrationManagementService>();
        services.AddScoped<IRegistrationQueryService, RegistrationQueryService>();
        
        // Register OTAC state management services for enhanced lifecycle control
        services.AddScoped<IOtacStateService, OtacStateService>();
        services.AddScoped<IOtacLifecycleMonitoringService, OtacLifecycleMonitoringService>();

        return services;
    }

    /// <summary>
    /// Registers the Repository and Unit of Work patterns with custom configuration.
    /// This overload allows for advanced configuration scenarios.
    /// </summary>
    /// <param name="services">The service collection to configure</param>
    /// <param name="configureOptions">Optional action to configure repository options</param>
    /// <returns>The service collection for method chaining</returns>
    public static IServiceCollection AddRepositoryPattern(
        this IServiceCollection services, 
        Action<RepositoryOptions>? configureOptions = null)
    {
        // Configure options if provided
        if (configureOptions != null)
        {
            services.Configure(configureOptions);
        }

        // Register the core repository pattern services
        return services.AddRepositoryPattern();
    }

    /// <summary>
    /// Validates that the Repository and Unit of Work patterns are properly registered.
    /// This is a helper method for testing and validation scenarios.
    /// </summary>
    /// <param name="services">The service collection to validate</param>
    /// <returns>True if properly configured, false otherwise</returns>
    public static bool IsRepositoryPatternRegistered(this IServiceCollection services)
    {
        var hasGenericRepository = services.Any(s => 
            s.ServiceType.IsGenericTypeDefinition && 
            s.ServiceType == typeof(IRepository<>) &&
            s.Lifetime == ServiceLifetime.Scoped);

        var hasUnitOfWork = services.Any(s => 
            s.ServiceType == typeof(IUnitOfWork) &&
            s.Lifetime == ServiceLifetime.Scoped);

        return hasGenericRepository && hasUnitOfWork;
    }
}

/// <summary>
/// Configuration options for the Repository pattern.
/// Allows for future extensibility and configuration scenarios.
/// </summary>
public class RepositoryOptions
{
    /// <summary>
    /// Whether to enable detailed logging for repository operations.
    /// Default is false for production performance.
    /// </summary>
    public bool EnableDetailedLogging { get; set; } = false;

    /// <summary>
    /// Default page size for paginated queries when none is specified.
    /// Default is 20 items per page.
    /// </summary>
    public int DefaultPageSize { get; set; } = 20;

    /// <summary>
    /// Maximum allowed page size to prevent performance issues.
    /// Default is 1000 items per page.
    /// </summary>
    public int MaxPageSize { get; set; } = 1000;

    /// <summary>
    /// Whether to automatically detach entities after read-only operations.
    /// This can improve memory usage but may impact some scenarios.
    /// Default is false to maintain compatibility.
    /// </summary>
    public bool AutoDetachReadOnlyEntities { get; set; } = false;

    /// <summary>
    /// Timeout in seconds for repository operations.
    /// Default is 30 seconds. Set to 0 for no timeout.
    /// </summary>
    public int OperationTimeoutSeconds { get; set; } = 30;
}

/// <summary>
/// Additional extension methods for BizConnect service registrations.
/// Provides organized registration of core services, cached services, and background jobs.
/// </summary>    
public static partial class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers BizConnect caching services with the dependency injection container.
    /// This method configures memory cache and cache service implementations.
    /// </summary>
    /// <param name="services">The service collection to configure</param>
    /// <returns>The service collection for method chaining</returns>
    public static IServiceCollection AddBizConnectCaching(this IServiceCollection services)
    {
        // Add memory cache with size limits for Phase 3A.1 specification
        services.AddMemoryCache(options =>
        {
            options.SizeLimit = 104857600; // 100MB default size limit
            options.CompactionPercentage = 0.25; // Remove 25% when limit reached
        });

        // Add caching services as Singleton for optimal performance
        services.AddSingleton<ICacheService, MemoryCacheService>();

        return services;
    }

    /// <summary>
    /// Registers core BizConnect services (without caching decorators) with the dependency injection container.
    /// These are the inner services that will be wrapped by cached decorators.
    /// </summary>
    /// <param name="services">The service collection to configure</param>
    /// <returns>The service collection for method chaining</returns>
    public static IServiceCollection AddBizConnectCoreServices(this IServiceCollection services)
    {
        // Register inner services (these will be wrapped by cached decorators)
        services.AddScoped<UserService>();
        services.AddScoped<BranchService>();
        
        // Register other core services
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IOddRegistrationService, OddRegistrationService>();
        services.AddScoped<IValidationService, ValidationService>();
        services.AddScoped<IAnalyticsService, AnalyticsService>();
        
        // Add core security services
        services.AddScoped<ISecurityAuditService, SecurityAuditService>();
        services.AddScoped<IPasswordPolicyService, PasswordPolicyService>();
        
        // Add advanced security services - Phase 3B.1
        // Note: IDateTimeProvider is registered in AddRegistrationServices()
        services.AddScoped<IRateLimitingService, AdvancedRateLimitingService>();
        services.AddScoped<IThreatResponseService, ThreatResponseService>();
        services.AddScoped<IEnhancedSecurityAuditService, EnhancedSecurityAuditService>();

        return services;
    }

    /// <summary>
    /// Registers cached decorator services for BizConnect with the dependency injection container.
    /// These decorators wrap the core services with caching functionality.
    /// </summary>
    /// <param name="services">The service collection to configure</param>
    /// <returns>The service collection for method chaining</returns> 
    public static IServiceCollection AddBizConnectCachedServices(this IServiceCollection services)
    {
        // Register cached decorator services
        services.AddScoped<IUserService>(provider => 
            new CachedUserService(
                provider.GetRequiredService<UserService>(),
                provider.GetRequiredService<ICacheService>(),
                provider.GetRequiredService<ILogger<CachedUserService>>()));

        services.AddScoped<IBranchService>(provider =>
            new CachedBranchService(
                provider.GetRequiredService<BranchService>(),
                provider.GetRequiredService<ICacheService>(),
                provider.GetRequiredService<ILogger<CachedBranchService>>()));

        return services;
    }

    /// <summary>
    /// Registers KBank ODD integration services with the dependency injection container.
    /// This method configures HTTP clients and service implementations for KBank integration.
    /// </summary>
    /// <param name="services">The service collection to configure</param>
    /// <returns>The service collection for method chaining</returns>
    public static IServiceCollection AddKBankOddServices(this IServiceCollection services)
    {
        // Add KBank ODD services
        services.AddHttpClient<KBankOddClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("User-Agent", "BizConnect/1.0");
        });
        services.AddScoped<IKBankOddClient, KBankOddClient>();
        services.AddScoped<IKbankOddService, KbankOddService>();
        services.AddScoped<IPaymentProcessingService, PaymentProcessingService>();

        return services;
    }

    /// <summary>
    /// Registers Hangfire background job services with the dependency injection container.
    /// This method configures background job implementations with proper dependency injection.
    /// </summary>
    /// <param name="services">The service collection to configure</param>
    /// <returns>The service collection for method chaining</returns>
    public static IServiceCollection AddBizConnectBackgroundJobs(this IServiceCollection services)
    {
        // Add Hangfire background job services
        services.AddScoped<PurgeExpiredOtacCodesJob>();
        services.AddScoped<OptimizedPurgeExpiredOtacCodesJob>();
        services.AddScoped<DailyPaymentJob>();

        return services;
    }
}