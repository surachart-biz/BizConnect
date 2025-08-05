using BizConnect.Dal;
using BizConnect.Dal.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace BizConnect.Tests.Integration
{
    /// <summary>
    /// Comprehensive integration tests for database connectivity and TIMESTAMPTZ functionality
    /// </summary>
    public class DatabaseTimestamptzIntegrationTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly ServiceProvider _serviceProvider;
        private readonly BizConnectContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<DatabaseTimestamptzIntegrationTests> _logger;

        public DatabaseTimestamptzIntegrationTests(ITestOutputHelper output)
        {
            _output = output;

            // Setup configuration
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false)
                .AddEnvironmentVariables();

            _configuration = builder.Build();

            // Setup services
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddXUnit(output));
            
            // Add database context with connection pooling
            var connectionString = _configuration.GetConnectionString("DefaultConnection");
            services.AddDbContext<BizConnectContext>(options =>
            {
                options.UseNpgsql(connectionString, npgsqlOptions =>
                {
                    npgsqlOptions.EnableRetryOnFailure(maxRetryCount: 3);
                });
                options.EnableSensitiveDataLogging();
                options.EnableDetailedErrors();
            });

            _serviceProvider = services.BuildServiceProvider();
            _context = _serviceProvider.GetRequiredService<BizConnectContext>();
            _logger = _serviceProvider.GetRequiredService<ILogger<DatabaseTimestamptzIntegrationTests>>();
        }

        [Fact]
        public async Task Test_DatabaseConnectivity_DefaultConnection_ShouldConnect()
        {
            _output.WriteLine("=== TESTING DATABASE CONNECTIVITY - DefaultConnection ===");
            
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                // Test basic connectivity
                var canConnect = await _context.Database.CanConnectAsync();
                Assert.True(canConnect, "Should be able to connect to the database");
                
                stopwatch.Stop();
                _output.WriteLine($"✅ Database connection successful in {stopwatch.ElapsedMilliseconds}ms");
                
                // Test schema validation
                var tableNames = await _context.Database.SqlQueryRaw<string>(
                    "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public' AND table_type = 'BASE TABLE'"
                ).ToListAsync();
                
                _output.WriteLine($"📊 Found {tableNames.Count} tables in public schema:");
                foreach (var table in tableNames.OrderBy(t => t))
                {
                    _output.WriteLine($"   - {table}");
                }
                
                // Verify critical tables exist
                var criticalTables = new[] { "KbankOddRegistration", "Branch", "Users", "_SchemaVersion" };
                foreach (var table in criticalTables)
                {
                    Assert.Contains(table, tableNames, StringComparer.OrdinalIgnoreCase);
                    _output.WriteLine($"✅ Critical table '{table}' exists");
                }
            }
            catch (Exception ex)
            {
                _output.WriteLine($"❌ Database connectivity test failed: {ex.Message}");
                _logger.LogError(ex, "Database connectivity test failed");
                throw;
            }
        }

        [Fact]
        public async Task Test_HangfireConnection_ShouldConnect()
        {
            _output.WriteLine("=== TESTING HANGFIRE DATABASE CONNECTIVITY ===");
            
            var hangfireConnectionString = _configuration.GetConnectionString("HangfireConnection");
            Assert.NotNull(hangfireConnectionString);
            
            var optionsBuilder = new DbContextOptionsBuilder<BizConnectContext>();
            optionsBuilder.UseNpgsql(hangfireConnectionString);
            
            using var hangfireContext = new BizConnectContext(optionsBuilder.Options);
            
            var stopwatch = Stopwatch.StartNew();
            var canConnect = await hangfireContext.Database.CanConnectAsync();
            stopwatch.Stop();
            
            Assert.True(canConnect, "Should be able to connect to Hangfire database");
            _output.WriteLine($"✅ Hangfire database connection successful in {stopwatch.ElapsedMilliseconds}ms");
            
            // Test Hangfire schema
            var hangfireTables = await hangfireContext.Database.SqlQueryRaw<string>(
                "SELECT table_name FROM information_schema.tables WHERE table_schema = 'hangfire'"
            ).ToListAsync();
            
            _output.WriteLine($"📊 Found {hangfireTables.Count} Hangfire tables:");
            foreach (var table in hangfireTables.OrderBy(t => t))
            {
                _output.WriteLine($"   - hangfire.{table}");
            }
            
            var expectedHangfireTables = new[] { "job", "jobstate", "jobqueue", "server", "hash", "counter" };
            foreach (var table in expectedHangfireTables)
            {
                Assert.Contains(table, hangfireTables, StringComparer.OrdinalIgnoreCase);
                _output.WriteLine($"✅ Hangfire table '{table}' exists");
            }
        }

        [Fact]
        public async Task Test_TimestamptzColumns_StoreAndRetrieve_ShouldMaintainUtcAccuracy()
        {
            _output.WriteLine("=== TESTING TIMESTAMPTZ STORAGE AND RETRIEVAL ===");
            
            // Test with various DateTime scenarios
            var testCases = new[]
            {
                new { Name = "Current UTC", Value = DateTime.UtcNow },
                new { Name = "Specific UTC", Value = new DateTime(2025, 8, 5, 14, 30, 45, DateTimeKind.Utc) },
                new { Name = "Future UTC", Value = DateTime.UtcNow.AddDays(30) },
                new { Name = "Past UTC", Value = DateTime.UtcNow.AddDays(-30) }
            };
            
            foreach (var testCase in testCases)
            {
                _output.WriteLine($"\n📅 Testing {testCase.Name}: {testCase.Value:yyyy-MM-dd HH:mm:ss.fff} UTC");
                
                // Create test registration
                var registration = new KbankOddRegistration
                {
                    ExternalReference = $"BIZ{DateTime.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}",
                    RegId = $"REG_{Guid.NewGuid():N}",
                    Status = "Pending",
                    CreatedAt = testCase.Value,
                    OtacExpiresAt = testCase.Value.AddMinutes(30),
                    LastAttemptAt = testCase.Value.AddMinutes(5),
                    CodeExpiresAt = testCase.Value.AddHours(24),
                    OtacCode = Guid.NewGuid().ToString("N")[..8].ToUpper(),
                    OtacState = "Generated",
                    GeneratedByUserId = 1, // Assuming admin user exists
                    AttemptCount = 0,
                    IsLocked = false
                };
                
                try
                {
                    // Store
                    _context.KbankOddRegistrations.Add(registration);
                    await _context.SaveChangesAsync();
                    _output.WriteLine($"✅ Stored registration with ID: {registration.Id}");
                    
                    // Retrieve fresh from database
                    var retrieved = await _context.KbankOddRegistrations
                        .AsNoTracking()
                        .FirstOrDefaultAsync(r => r.Id == registration.Id);
                    
                    Assert.NotNull(retrieved);
                    
                    // Verify TIMESTAMPTZ accuracy
                    var timeDifference = Math.Abs((retrieved.CreatedAt - testCase.Value).TotalMilliseconds);
                    Assert.True(timeDifference < 1000, $"CreatedAt time difference should be less than 1 second, was {timeDifference}ms");
                    
                    _output.WriteLine($"   📊 CreatedAt - Original: {testCase.Value:yyyy-MM-dd HH:mm:ss.fff}");
                    _output.WriteLine($"   📊 CreatedAt - Retrieved: {retrieved.CreatedAt:yyyy-MM-dd HH:mm:ss.fff}");
                    _output.WriteLine($"   📊 Time difference: {timeDifference:F2}ms");
                    
                    // Test nullable TIMESTAMPTZ fields
                    if (retrieved.OtacExpiresAt.HasValue)
                    {
                        var otacTimeDiff = Math.Abs((retrieved.OtacExpiresAt.Value - registration.OtacExpiresAt!.Value).TotalMilliseconds);
                        Assert.True(otacTimeDiff < 1000, $"OtacExpiresAt time difference should be less than 1 second, was {otacTimeDiff}ms");
                        _output.WriteLine($"   📊 OtacExpiresAt accuracy: {otacTimeDiff:F2}ms difference");
                    }
                    
                    // Cleanup
                    _context.KbankOddRegistrations.Remove(retrieved);
                    await _context.SaveChangesAsync();
                    _output.WriteLine($"✅ Cleaned up test registration");
                }
                catch (Exception ex)
                {
                    _output.WriteLine($"❌ Failed to test {testCase.Name}: {ex.Message}");
                    _logger.LogError(ex, "TIMESTAMPTZ test failed for {TestCase}", testCase.Name);
                    throw;
                }
            }
        }

        [Fact]
        public async Task Test_TimezoneConversion_ShouldHandleUtcCorrectly()
        {
            _output.WriteLine("=== TESTING TIMEZONE CONVERSION ACCURACY ===");
            
            var utcNow = DateTime.UtcNow;
            _output.WriteLine($"📅 Test UTC time: {utcNow:yyyy-MM-dd HH:mm:ss.fff} UTC");
            
            // Create test data
            var registration = new KbankOddRegistration
            {
                ExternalReference = $"BIZ{utcNow:yyyyMMddHHmmssfff}",
                RegId = $"TZ_TEST_{Guid.NewGuid():N}",
                Status = "Pending",
                CreatedAt = utcNow,
                OtacCode = "TZTEST01",
                OtacState = "Generated",
                GeneratedByUserId = 1,
                AttemptCount = 0,
                IsLocked = false
            };
            
            try
            {
                _context.KbankOddRegistrations.Add(registration);
                await _context.SaveChangesAsync();
                
                // Query using raw SQL to verify TIMESTAMPTZ behavior
                var sqlQuery = @"
                    SELECT 
                        ""CreatedAt"",
                        EXTRACT(TIMEZONE FROM ""CreatedAt"") as timezone_offset_seconds,
                        ""CreatedAt"" AT TIME ZONE 'UTC' as utc_time,
                        ""CreatedAt"" AT TIME ZONE 'Asia/Bangkok' as bangkok_time
                    FROM ""KbankOddRegistration""
                    WHERE ""Id"" = {0}";
                
                var results = await _context.Database.SqlQueryRaw<TimezoneTestResult>(
                    sqlQuery, registration.Id
                ).ToListAsync();
                
                var result = results.FirstOrDefault();
                Assert.NotNull(result);
                
                _output.WriteLine($"📊 Database CreatedAt: {result.CreatedAt:yyyy-MM-dd HH:mm:ss.fff}");
                _output.WriteLine($"📊 Timezone offset: {result.timezone_offset_seconds} seconds");
                _output.WriteLine($"📊 UTC conversion: {result.utc_time:yyyy-MM-dd HH:mm:ss.fff}");
                _output.WriteLine($"📊 Bangkok time: {result.bangkok_time:yyyy-MM-dd HH:mm:ss.fff}");
                
                // Verify UTC storage
                var timeDiff = Math.Abs((result.CreatedAt - utcNow).TotalSeconds);
                Assert.True(timeDiff < 1, $"Time difference should be less than 1 second, was {timeDiff}s");
                
                // Verify timezone handling
                var bangkokOffset = (result.bangkok_time - result.utc_time).TotalHours;
                Assert.True(Math.Abs(bangkokOffset - 7) < 1, $"Bangkok should be UTC+7, got offset of {bangkokOffset} hours");
                
                _output.WriteLine($"✅ Timezone conversion test passed");
                
                // Cleanup
                _context.KbankOddRegistrations.Remove(registration);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _output.WriteLine($"❌ Timezone conversion test failed: {ex.Message}");
                _logger.LogError(ex, "Timezone conversion test failed");
                throw;
            }
        }

        [Fact]
        public async Task Test_QueryPerformance_WithTimestamptzIndexes()
        {
            _output.WriteLine("=== TESTING QUERY PERFORMANCE WITH TIMESTAMPTZ INDEXES ===");
            
            var stopwatch = new Stopwatch();
            
            // Test indexed query performance
            var queries = new[]
            {
                new { Name = "CreatedAt Range Query", 
                      Query = () => _context.KbankOddRegistrations
                        .Where(r => r.CreatedAt >= DateTime.UtcNow.AddDays(-7))
                        .CountAsync() },
                
                new { Name = "OtacExpiresAt Filter", 
                      Query = () => _context.KbankOddRegistrations
                        .Where(r => r.OtacExpiresAt < DateTime.UtcNow)
                        .CountAsync() },
                
                new { Name = "Status and CreatedAt Combined", 
                      Query = () => _context.KbankOddRegistrations
                        .Where(r => r.Status == "Pending" && r.CreatedAt >= DateTime.UtcNow.AddHours(-1))
                        .CountAsync() },
                
                new { Name = "CodeExpiresAt Purge Query", 
                      Query = () => _context.KbankOddRegistrations
                        .Where(r => r.CodeExpiresAt < DateTime.UtcNow)
                        .CountAsync() }
            };
            
            foreach (var queryTest in queries)
            {
                stopwatch.Restart();
                var count = await queryTest.Query();
                stopwatch.Stop();
                
                _output.WriteLine($"📊 {queryTest.Name}: {count} records, {stopwatch.ElapsedMilliseconds}ms");
                
                // Performance assertion - queries should be reasonably fast
                Assert.True(stopwatch.ElapsedMilliseconds < 5000, 
                    $"{queryTest.Name} took too long: {stopwatch.ElapsedMilliseconds}ms");
            }
        }

        [Fact]
        public async Task Test_ForeignKeyConstraints_ShouldBeEnforced()
        {
            _output.WriteLine("=== TESTING FOREIGN KEY CONSTRAINTS ===");
            
            try
            {
                // Test Branch foreign key constraint
                var invalidRegistration = new KbankOddRegistration
                {
                    ExternalReference = $"BIZ{DateTime.UtcNow:yyyyMMddHHmmssfff}",
                    RegId = "FK_TEST",
                    Status = "Pending",
                    BranchId = 99999, // Non-existent branch
                    OtacCode = "FKTEST01",
                    OtacState = "Generated",
                    GeneratedByUserId = 1,
                    AttemptCount = 0,
                    IsLocked = false
                };
                
                _context.KbankOddRegistrations.Add(invalidRegistration);
                
                // This should fail due to foreign key constraint
                var exception = await Assert.ThrowsAsync<DbUpdateException>(
                    () => _context.SaveChangesAsync()
                );
                
                _output.WriteLine($"✅ Foreign key constraint properly enforced: {exception.InnerException?.Message}");
                
                // Test User foreign key constraint
                var invalidUserRegistration = new KbankOddRegistration
                {
                    ExternalReference = $"BIZ{DateTime.UtcNow:yyyyMMddHHmmssfff}",
                    RegId = "FK_USER_TEST",
                    Status = "Pending",
                    GeneratedByUserId = 99999, // Non-existent user
                    OtacCode = "FKTEST02",
                    OtacState = "Generated",
                    AttemptCount = 0,
                    IsLocked = false
                };
                
                _context.Entry(invalidRegistration).State = EntityState.Detached; // Clear previous entity
                _context.KbankOddRegistrations.Add(invalidUserRegistration);
                
                var userException = await Assert.ThrowsAsync<DbUpdateException>(
                    () => _context.SaveChangesAsync()
                );
                
                _output.WriteLine($"✅ User foreign key constraint properly enforced: {userException.InnerException?.Message}");
            }
            catch (Exception ex) when (!(ex is DbUpdateException))
            {
                _output.WriteLine($"❌ Foreign key constraint test failed unexpectedly: {ex.Message}");
                throw;
            }
        }

        [Fact]
        public async Task Test_SchemaVersioning_ShouldTrackMigrations()
        {
            _output.WriteLine("=== TESTING SCHEMA VERSIONING ===");
            
            var appliedMigrations = await _context._SchemaVersions
                .OrderBy(sv => sv.AppliedAt)
                .ToListAsync();
            
            _output.WriteLine($"📊 Found {appliedMigrations.Count} applied migrations:");
            
            foreach (var migration in appliedMigrations)
            {
                _output.WriteLine($"   📅 {migration.AppliedAt:yyyy-MM-dd HH:mm:ss} - {migration.Filename}");
            }
            
            // Verify recent TIMESTAMPTZ migration is present
            var timestampMigration = appliedMigrations
                .FirstOrDefault(m => m.Filename.Contains("ConvertTimestampToTimestamptz"));
            
            Assert.NotNull(timestampMigration);
            _output.WriteLine($"✅ TIMESTAMPTZ migration found: {timestampMigration.Filename}");
            
            // Verify migration timestamps are in TIMESTAMPTZ format
            foreach (var migration in appliedMigrations.Take(3))
            {
                Assert.Equal(DateTimeKind.Utc, migration.AppliedAt.Kind);
                _output.WriteLine($"✅ Migration timestamp is UTC: {migration.Filename}");
            }
        }

        [Fact]
        public async Task Test_ConnectionPooling_ShouldHandleConcurrentRequests()
        {
            _output.WriteLine("=== TESTING CONNECTION POOLING AND CONCURRENCY ===");
            
            var concurrentTasks = new List<Task<int>>();
            var taskCount = 10;
            
            for (int i = 0; i < taskCount; i++)
            {
                var taskId = i;
                concurrentTasks.Add(Task.Run(async () =>
                {
                    using var scope = _serviceProvider.CreateScope();
                    var context = scope.ServiceProvider.GetRequiredService<BizConnectContext>();
                    
                    var stopwatch = Stopwatch.StartNew();
                    var count = await context.KbankOddRegistrations.CountAsync();
                    stopwatch.Stop();
                    
                    _output.WriteLine($"📊 Task {taskId}: {count} records, {stopwatch.ElapsedMilliseconds}ms");
                    return count;
                }));
            }
            
            var results = await Task.WhenAll(concurrentTasks);
            
            // All tasks should return the same count
            var firstResult = results[0];
            Assert.All(results, result => Assert.Equal(firstResult, result));
            
            _output.WriteLine($"✅ All {taskCount} concurrent tasks completed successfully");
            _output.WriteLine($"📊 Consistent result count: {firstResult}");
        }

        public void Dispose()
        {
            _context?.Dispose();
            _serviceProvider?.Dispose();
        }

        // Helper class for timezone testing
        public class TimezoneTestResult
        {
            public DateTime CreatedAt { get; set; }
            public double timezone_offset_seconds { get; set; }
            public DateTime utc_time { get; set; }
            public DateTime bangkok_time { get; set; }
        }
    }
}