# BizConnect Database Migrations

## Current Status (2025-08-05)

The database has been consolidated into a clean, multi-language schema that replaces all previous scattered migrations.

## Migration Order

**IMPORTANT**: Run these migrations in this exact order:

1. **20250805-02_CleanupDatabase.sql** - Removes backup tables, Hangfire tables, and temporary objects
2. **20250805-03_ConsolidatedSchema.sql** - Creates clean consolidated schema with multi-language support

## Schema Overview

### Core Tables

1. **`_SchemaVersion`** - Migration tracking
2. **`Users`** - Authentication and authorization
3. **`Branch`** - Bank branches with Thai/English multi-language support
4. **`KbankOddRegistration`** - KBank ODD registration with integrated OTAC functionality

### Multi-Language Support

The schema includes Thai/English language support for the UI toggle:

- **Branch table**: `NameTh`/`NameEn`, `AddressTh`/`AddressEn`
- **Helper functions**: `get_branch_name()`, `get_branch_address()`
- **Language codes**: 'th' for Thai, 'en' for English

### Business Flow

The consolidated `KbankOddRegistration` table supports the complete business flow:

1. **Employee generates OTAC** → INSERT with `OtacState='Generated'`
2. **Guest validates OTAC** → UPDATE `OtacState='Validated'`  
3. **Guest submits form** → UPDATE `OtacState='Used'`, `Status='Pending'`
4. **KBank callback** → UPDATE `Status='Success'` or `Status='Fail'`

## Database Setup

### Prerequisites

1. PostgreSQL 12+ installed
2. Database created: `bizconnect_local` (or your environment name)
3. Proper connection string in `appsettings.Local.json`

### Running Migrations

Use the cross-platform update script:

```bash
# Windows PowerShell
.\scripts\update-db.ps1

# macOS/Linux/WSL/Git Bash  
bash ./scripts/update-db.sh

# Cross-platform (detects OS automatically)
./scripts/update-db
```

These scripts will:
1. Execute SQL migrations from `/db/migrations/`
2. Re-scaffold Entity Framework models
3. Validate the build

### Hangfire Database Separation

**IMPORTANT**: Hangfire tables have been removed from the main database. Create a separate Hangfire database:

```sql
CREATE DATABASE bizconnect_hangfire;
```

Update your connection strings:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=bizconnect_local;Username=postgres;Password=your_password",
    "HangfireConnection": "Host=localhost;Database=bizconnect_hangfire;Username=postgres;Password=your_password"
  }
}
```

## Entity Framework Scaffolding

After running the consolidated migrations, scaffold the models:

```bash
cd BizConnect.Dal

# Scaffold from database
dotnet ef dbcontext scaffold "Host=localhost;Database=bizconnect_local;Username=postgres;Password=your_password" Npgsql.EntityFrameworkCore.PostgreSQL --output-dir Models --context-dir . --context BizConnectContext --force

# Build to verify
dotnet build
```

### Expected Models

The scaffolding should generate these models:

- `SchemaVersion.cs`
- `User.cs` 
- `Branch.cs`
- `KbankOddRegistration.cs`

## Multi-Language Usage

### In Controllers

```csharp
public async Task<IActionResult> BranchList(string culture = "en")
{
    var branches = await _context.Branch
        .Select(b => new BranchViewModel 
        {
            BranchId = b.BranchId,
            Name = culture == "th" ? b.NameTh ?? b.Name : b.NameEn ?? b.Name,
            Address = culture == "th" ? b.AddressTh ?? b.Address : b.AddressEn ?? b.Address
        })
        .ToListAsync();
    
    return View(branches);
}
```

### In Razor Views

```html
@{
    var culture = Context.Request.Cookies["culture"] ?? "en";
}

<h1>@(culture == "th" ? "รายการสาขา" : "Branch List")</h1>

@foreach(var branch in Model)
{
    <div class="branch-item">
        <h3>@(culture == "th" ? branch.NameTh : branch.NameEn)</h3>
        <p>@(culture == "th" ? branch.AddressTh : branch.AddressEn)</p>
    </div>
}
```

### Using Helper Functions (SQL)

```sql
-- Get Thai branch name
SELECT get_branch_name(1, 'th') AS BranchNameTh;

-- Get English branch address  
SELECT get_branch_address(1, 'en') AS BranchAddressEn;
```

## Performance Features

### Indexes

The schema includes comprehensive indexing for:
- OTAC code lookups (`IX_KbankOddRegistration_OtacCode`)
- Status filtering (`IX_KbankOddRegistration_Status`)
- Expiration cleanup (`IX_KbankOddRegistration_OtacExpiresAt`)
- Branch lookups (`IX_Branch_Code`, `IX_Branch_Name`)
- User authentication (`IX_Users_Username`)

### Views

- **`ActiveOddRegistrations`** - Active registrations with branch info for admin dashboard
- **`ExpiredOtacCodes`** - Expired codes for cleanup background jobs

### Background Job Optimization

For Hangfire background jobs, query the views for better performance:

```csharp
// PurgeExpiredCodesJob (runs every 5 minutes)
var expiredCodes = await _context.ExpiredOtacCodes
    .Where(x => x.OtacExpiresAt < DateTime.UtcNow.AddHours(-1)) // Grace period
    .ToListAsync();

// DailyPaymentJob (runs at 2:00 AM)
var activeRegistrations = await _context.ActiveOddRegistrations
    .Where(x => x.Status == "Pending")
    .ToListAsync();
```

## Schema Validation

After migration, verify the schema:

```sql
-- Check table count (should be 4: _SchemaVersion, Users, Branch, KbankOddRegistration)
SELECT COUNT(*) as TableCount 
FROM information_schema.tables 
WHERE table_schema = 'public' AND table_type = 'BASE TABLE';

-- Check for any remaining Hangfire tables (should be 0)
SELECT table_name 
FROM information_schema.tables 
WHERE table_schema = 'public' 
AND table_name LIKE '%job%' OR table_name LIKE '%hangfire%';

-- Check multi-language columns exist
SELECT column_name 
FROM information_schema.columns 
WHERE table_name = 'Branch' 
AND column_name IN ('NameTh', 'NameEn', 'AddressTh', 'AddressEn');

-- Verify TIMESTAMPTZ usage (should be all timestamp with time zone)
SELECT table_name, column_name, data_type
FROM information_schema.columns 
WHERE table_schema = 'public' 
AND data_type LIKE 'timestamp%'
ORDER BY table_name, column_name;
```

## Troubleshooting

### Common Issues

1. **EF Scaffolding Fails**
   - Ensure PostgreSQL connection is working
   - Check that all migrations have been applied
   - Verify no Hangfire tables exist in main database

2. **Multi-language Not Working**
   - Check that NameTh/NameEn columns exist in Branch table
   - Verify helper functions are created
   - Ensure proper culture handling in controllers

3. **Performance Issues**
   - Check that all indexes are created
   - Use the provided views for common queries
   - Consider query optimization for large datasets

### Reset Database (Development Only)

```sql
-- WARNING: This deletes all data
DROP DATABASE IF EXISTS bizconnect_local;
CREATE DATABASE bizconnect_local;

-- Then re-run migrations
./scripts/update-db
```

## Architecture Benefits

This consolidated schema provides:

1. **Single Source of Truth** - One migration file instead of 12 scattered files
2. **Multi-language Ready** - Thai/English support built-in
3. **Performance Optimized** - Comprehensive indexing and views
4. **EF Compatible** - Clean scaffolding without Hangfire conflicts
5. **Maintainable** - Clear separation between main data and background jobs
6. **Scalable** - Proper foreign keys and constraints for data integrity

## Migration History

| Date | Migration | Purpose |
|------|-----------|---------|
| 2025-08-05 | 20250805-02_CleanupDatabase.sql | Remove backup/Hangfire tables |
| 2025-08-05 | 20250805-03_ConsolidatedSchema.sql | Consolidated multi-language schema |

**Previous migrations (replaced):**
- 20250710-01_InitialSchema.sql
- 20250713-01_CreateKbankOddRegistration.sql  
- 20250713-02_AddContactColumns.sql
- 20250803-01_AddKbankRequiredColumns.sql
- 20250803-02_CreateBranchTable.sql
- 20250803-03_CreateHangfireTables.sql
- 20250803-04_CreateOtacTable.sql
- 20250803-05_UpdateKbankRegistrationV197.sql
- 20250803-06_AddOtacKbankRegistrationLink.sql
- 20250804-01_MergeOtacIntoKbankRegistration.sql
- 20250805-01_AddHangfireAggregatedCounter.sql
- 20250805-02_ConvertTimestampToTimestamptz.sql

## Guidelines for Future Migrations

1. **Idempotent**: Migrations should be safe to run multiple times
   - Use `CREATE TABLE IF NOT EXISTS`
   - Use `CREATE INDEX IF NOT EXISTS`
   - Use `INSERT ... ON CONFLICT DO NOTHING`

2. **Atomic**: Each migration should be a complete, self-contained change

3. **Multi-language**: Always consider Thai/English support for user-facing data

4. **Performance**: Include appropriate indexes for new columns

5. **Documentation**: Include comprehensive comments and update this README
