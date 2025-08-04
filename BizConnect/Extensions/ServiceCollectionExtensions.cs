using BizConnect.Dal.Repositories;
using BizConnect.Dal.UnitOfWork;
using BizConnect.Services;
using BizConnect.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace BizConnect.Extensions;

/// <summary>
/// Extension methods for IServiceCollection to register Repository and Unit of Work patterns.
/// Provides clean, fluent configuration of data access layer dependencies.
/// </summary>
public static class ServiceCollectionExtensions
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