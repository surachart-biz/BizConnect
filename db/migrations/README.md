# Database Migrations

This directory contains SQL migration files for the BizConnect database.

## Naming Convention

Migration files should follow this naming pattern:
```
yyyyMMdd-##_Description.sql
```

Where:
- `yyyy` = 4-digit year
- `MM` = 2-digit month
- `dd` = 2-digit day
- `##` = 2-digit sequence number for the day (01, 02, etc.)
- `Description` = Brief description of the migration

## Examples

- `20250710-01_InitialSchema.sql` - Initial database schema
- `20250710-02_AddUserProfiles.sql` - Add user profiles table
- `20250711-01_AddIndexes.sql` - Add performance indexes

## Guidelines

1. **Idempotent**: Migrations should be safe to run multiple times
   - Use `CREATE TABLE IF NOT EXISTS`
   - Use `CREATE INDEX IF NOT EXISTS`
   - Use `INSERT ... ON CONFLICT DO NOTHING`

2. **Atomic**: Each migration should be a complete, self-contained change

3. **Backward Compatible**: Avoid breaking changes when possible
   - Add columns with defaults
   - Don't drop columns immediately
   - Use deprecation periods for schema changes

4. **Comments**: Include comments explaining the purpose of each change

## Execution Order

Migrations are executed in alphabetical order by filename. The naming convention ensures chronological execution.

## Running Migrations

Use the provided scripts to run migrations:

**Windows (PowerShell):**
```powershell
.\scripts\update-db.ps1
```

**macOS/Linux/WSL (Bash):**
```bash
bash ./scripts/update-db.sh
```

These scripts will:
1. Execute all SQL files in this directory
2. Re-scaffold Entity Framework models
3. Validate the build
