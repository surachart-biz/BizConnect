-- ====================================================================
-- BizConnect Consolidated Database Schema Migration
-- Migration: 20250805-03_ConsolidatedSchema.sql
-- Date: 2025-08-05
-- Description: Consolidated schema that replaces all previous migrations with clean multi-language support
-- ====================================================================
-- 
-- IMPORTANT: This migration completely replaces the need for all previous migration files:
-- - 20250710-01_InitialSchema.sql
-- - 20250713-01_CreateKbankOddRegistration.sql  
-- - 20250713-02_AddContactColumns.sql
-- - 20250803-01_AddKbankRequiredColumns.sql
-- - 20250803-02_CreateBranchTable.sql
-- - 20250803-03_CreateHangfireTables.sql (moved to separate database)
-- - 20250803-04_CreateOtacTable.sql (merged into KbankOddRegistration)
-- - 20250803-05_UpdateKbankRegistrationV197.sql
-- - 20250803-06_AddOtacKbankRegistrationLink.sql
-- - 20250804-01_MergeOtacIntoKbankRegistration.sql
-- - 20250805-01_AddHangfireAggregatedCounter.sql (separate database)
-- - 20250805-02_ConvertTimestampToTimestamptz.sql
-- ====================================================================

-- Enable error handling and logging
\set ON_ERROR_STOP on

BEGIN;

-- ====================================================================
-- SECTION 1: COMPLETE CLEANUP - DROP ALL EXISTING TABLES FIRST
-- ====================================================================

DO $$
DECLARE
    r RECORD;
BEGIN
    RAISE NOTICE 'Starting COMPLETE schema cleanup and rebuild...';
    
    -- Drop ALL existing tables in public schema (except information_schema system tables)
    FOR r IN (SELECT tablename FROM pg_tables WHERE schemaname = 'public') LOOP
        EXECUTE 'DROP TABLE IF EXISTS public.' || quote_ident(r.tablename) || ' CASCADE';
        RAISE NOTICE 'Dropped table: %', r.tablename;
    END LOOP;
    
    -- Drop ALL existing views in public schema
    FOR r IN (SELECT viewname FROM pg_views WHERE schemaname = 'public') LOOP
        EXECUTE 'DROP VIEW IF EXISTS public.' || quote_ident(r.viewname) || ' CASCADE';
        RAISE NOTICE 'Dropped view: %', r.viewname;
    END LOOP;
    
    -- Drop custom functions only (not extension functions)
    FOR r IN (SELECT routine_name FROM information_schema.routines 
              WHERE routine_schema = 'public' 
                AND routine_type = 'FUNCTION'
                AND routine_name NOT LIKE 'uuid_%'  -- Skip UUID extension functions
                AND routine_name NOT LIKE '%operator%') LOOP  -- Skip operator functions
        BEGIN
            EXECUTE 'DROP FUNCTION IF EXISTS public.' || quote_ident(r.routine_name) || ' CASCADE';
            RAISE NOTICE 'Dropped function: %', r.routine_name;
        EXCEPTION WHEN OTHERS THEN
            RAISE NOTICE 'Could not drop function % (likely system/extension function): %', r.routine_name, SQLERRM;
        END;
    END LOOP;
    
    -- Drop hangfire schema completely from main database (should be separate)
    DROP SCHEMA IF EXISTS hangfire CASCADE;
    
    -- Drop any remaining sequences not tied to tables
    FOR r IN (SELECT sequence_name FROM information_schema.sequences WHERE sequence_schema = 'public') LOOP
        EXECUTE 'DROP SEQUENCE IF EXISTS public.' || quote_ident(r.sequence_name) || ' CASCADE';
        RAISE NOTICE 'Dropped sequence: %', r.sequence_name;
    END LOOP;
    
    RAISE NOTICE 'COMPLETE CLEANUP FINISHED - All old tables, views, functions, and sequences removed';
    RAISE NOTICE 'Ready for fresh schema creation with zero legacy artifacts';
END $$;

-- ====================================================================
-- SECTION 2: EXTENSIONS AND UTILITY FUNCTIONS
-- ====================================================================

-- Enable UUID extension for future use
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- Create function to automatically update UpdatedAt timestamp
CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW."UpdatedAt" = NOW();
    RETURN NEW;
END;
$$ LANGUAGE 'plpgsql';

-- ====================================================================
-- SECTION 3: SCHEMA VERSION TRACKING
-- ====================================================================

-- Create schema version tracking table
CREATE TABLE "_SchemaVersion" (
    "Id" SERIAL PRIMARY KEY,
    "Filename" VARCHAR(255) NOT NULL UNIQUE,
    "AppliedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Create index on Filename for faster lookups
CREATE UNIQUE INDEX "IX_SchemaVersion_Filename" ON "_SchemaVersion" ("Filename");

-- Add comments for documentation
COMMENT ON TABLE "_SchemaVersion" IS 'Tracks applied database migration files';
COMMENT ON COLUMN "_SchemaVersion"."Filename" IS 'Name of the migration file that was applied';
COMMENT ON COLUMN "_SchemaVersion"."AppliedAt" IS 'Timestamp when the migration was applied';

-- ====================================================================
-- SECTION 4: USERS TABLE WITH AUTHENTICATION
-- ====================================================================

-- Create Users table with proper timezone support
CREATE TABLE "Users" (
    "Id" SERIAL PRIMARY KEY,
    "Username" VARCHAR(100) NOT NULL,
    "PasswordHash" VARCHAR(255) NOT NULL,
    "Role" VARCHAR(50) NOT NULL,
    "CreatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "UpdatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "IsActive" BOOLEAN NOT NULL DEFAULT TRUE
);

-- Create indexes for Users table
CREATE UNIQUE INDEX "IX_Users_Username" ON "Users" ("Username");
CREATE INDEX "IX_Users_Role" ON "Users" ("Role");
CREATE INDEX "IX_Users_IsActive" ON "Users" ("IsActive");

-- Create trigger to automatically update UpdatedAt on Users table
DROP TRIGGER IF EXISTS update_users_updated_at ON "Users";
CREATE TRIGGER update_users_updated_at
    BEFORE UPDATE ON "Users"
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

-- Insert initial admin user (password: admin123)
INSERT INTO "Users" ("Username", "PasswordHash", "Role", "CreatedAt", "UpdatedAt", "IsActive")
VALUES (
    'admin',
    '$2a$11$.cREN1iQUvZznsDi8abVhetwOBVxI3NkQLbNv1PO07mUfYYuzfldK',
    'Admin',
    NOW(),
    NOW(),
    TRUE
)
ON CONFLICT ("Username") DO NOTHING;

-- Add comments for Users table
COMMENT ON TABLE "Users" IS 'Application users with authentication and authorization data';
COMMENT ON COLUMN "Users"."Id" IS 'Primary key, auto-incrementing user identifier';
COMMENT ON COLUMN "Users"."Username" IS 'Unique username for authentication';
COMMENT ON COLUMN "Users"."PasswordHash" IS 'BCrypt hashed password';
COMMENT ON COLUMN "Users"."Role" IS 'User role: Admin or User';
COMMENT ON COLUMN "Users"."CreatedAt" IS 'Timestamp when user was created';
COMMENT ON COLUMN "Users"."UpdatedAt" IS 'Timestamp when user was last updated (auto-updated by trigger)';
COMMENT ON COLUMN "Users"."IsActive" IS 'Whether the user account is active and can log in';

-- ====================================================================
-- SECTION 5: BRANCH TABLE WITH MULTI-LANGUAGE SUPPORT
-- ====================================================================

-- Create Branch table with Thai/English support
CREATE TABLE "Branch" (
    "BranchId" SERIAL PRIMARY KEY,
    "Name" VARCHAR(100) NOT NULL,
    "NameTh" VARCHAR(100),
    "NameEn" VARCHAR(100),
    "Code" VARCHAR(10),
    "Address" TEXT,
    "AddressTh" TEXT,
    "AddressEn" TEXT,
    "IsActive" BOOLEAN NOT NULL DEFAULT TRUE,
    "CreatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "UpdatedAt" TIMESTAMPTZ
);

-- Create indexes for Branch table
CREATE UNIQUE INDEX "IX_Branch_Code" ON "Branch" ("Code");
CREATE INDEX "IX_Branch_Name" ON "Branch" ("Name");
CREATE INDEX "IX_Branch_IsActive" ON "Branch" ("IsActive");

-- Create trigger to automatically update UpdatedAt on Branch table
DROP TRIGGER IF EXISTS update_branch_updated_at ON "Branch";
CREATE TRIGGER update_branch_updated_at
    BEFORE UPDATE ON "Branch"
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

-- Insert sample branch data with multi-language support
INSERT INTO "Branch" ("Name", "NameTh", "NameEn", "Code", "Address", "AddressTh", "AddressEn", "IsActive", "CreatedAt") VALUES
('Bangkok Main Branch', 'สาขาหลักกรุงเทพฯ', 'Bangkok Main Branch', 'BMB001', '123 Silom Road, Bang Rak, Bangkok 10500', '123 ถนนสีลม บางรัก กรุงเทพฯ 10500', '123 Silom Road, Bang Rak, Bangkok 10500', TRUE, NOW()),
('Sukhumvit Branch', 'สาขาสุขุมวิท', 'Sukhumvit Branch', 'SUK002', '456 Sukhumvit Road, Watthana, Bangkok 10110', '456 ถนนสุขุมวิท วัฒนา กรุงเทพฯ 10110', '456 Sukhumvit Road, Watthana, Bangkok 10110', TRUE, NOW()),
('Chatuchak Branch', 'สาขาจตุจักร', 'Chatuchak Branch', 'CHA003', '789 Phahonyothin Road, Chatuchak, Bangkok 10900', '789 ถนนพหลโยธิน จตุจักร กรุงเทพฯ 10900', '789 Phahonyothin Road, Chatuchak, Bangkok 10900', TRUE, NOW()),
('Phuket Branch', 'สาขาภูเก็ต', 'Phuket Branch', 'PHU004', '321 Thalang Road, Mueang Phuket, Phuket 83000', '321 ถนนถลาง เมืองภูเก็ต ภูเก็ต 83000', '321 Thalang Road, Mueang Phuket, Phuket 83000', TRUE, NOW()),
('Chiang Mai Branch', 'สาขาเชียงใหม่', 'Chiang Mai Branch', 'CHM005', '654 Chang Khlan Road, Mueang Chiang Mai, Chiang Mai 50100', '654 ถนนช้างคลาน เมืองเชียงใหม่ เชียงใหม่ 50100', '654 Chang Khlan Road, Mueang Chiang Mai, Chiang Mai 50100', TRUE, NOW())
ON CONFLICT ("Code") DO NOTHING;

-- Add comments for Branch table
COMMENT ON TABLE "Branch" IS 'Bank branch information with multi-language support for ODD registration management';
COMMENT ON COLUMN "Branch"."BranchId" IS 'Primary key, auto-incrementing branch identifier';
COMMENT ON COLUMN "Branch"."Name" IS 'Default branch name (fallback)';
COMMENT ON COLUMN "Branch"."NameTh" IS 'Branch name in Thai language';
COMMENT ON COLUMN "Branch"."NameEn" IS 'Branch name in English language';
COMMENT ON COLUMN "Branch"."Code" IS 'Unique branch code for identification';
COMMENT ON COLUMN "Branch"."Address" IS 'Default physical address (fallback)';
COMMENT ON COLUMN "Branch"."AddressTh" IS 'Physical address in Thai language';
COMMENT ON COLUMN "Branch"."AddressEn" IS 'Physical address in English language';
COMMENT ON COLUMN "Branch"."IsActive" IS 'Whether the branch is currently active and accepting registrations';
COMMENT ON COLUMN "Branch"."CreatedAt" IS 'Timestamp when branch was created';
COMMENT ON COLUMN "Branch"."UpdatedAt" IS 'Timestamp when branch was last updated (auto-updated by trigger)';

-- ====================================================================
-- SECTION 6: KBANK ODD REGISTRATION TABLE (CONSOLIDATED WITH OTAC)
-- ====================================================================

-- Create consolidated KbankOddRegistration table with integrated OTAC functionality
CREATE TABLE "KbankOddRegistration" (
    "Id" SERIAL PRIMARY KEY,
    
    -- KBank ODD Registration Fields
    "ExternalReference" VARCHAR(40) UNIQUE NOT NULL,
    "RegId" VARCHAR(40),
    "EspaId" VARCHAR(40),
    "Status" VARCHAR(20) NOT NULL DEFAULT 'Pending',
    "ReturnCode" VARCHAR(10),
    
    -- Customer Information Fields (V1.9.7 compliance)
    "IdType" VARCHAR(20),
    "IdValue" VARCHAR(30),
    "FullName" VARCHAR(100),
    "MobileNo" VARCHAR(20),
    "AccountNo" VARCHAR(20),
    "BranchId" INTEGER,
    
    -- OTAC Integration Fields
    "OtacCode" VARCHAR(8) NOT NULL,
    "OtacState" VARCHAR(20) NOT NULL DEFAULT 'Generated',
    "GeneratedByUserId" INTEGER NOT NULL,
    "AttemptCount" INTEGER NOT NULL DEFAULT 0,
    "IsLocked" BOOLEAN NOT NULL DEFAULT FALSE,
    "LastAttemptAt" TIMESTAMPTZ,
    "LastAttemptIp" VARCHAR(45),
    "OtacExpiresAt" TIMESTAMPTZ,
    
    -- Audit Fields
    "CreatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "UpdatedAt" TIMESTAMPTZ,
    
    -- Foreign Key Constraints
    CONSTRAINT "FK_KbankOddRegistration_Branch" 
        FOREIGN KEY ("BranchId") REFERENCES "Branch" ("BranchId") ON DELETE SET NULL,
    CONSTRAINT "FK_KbankOddRegistration_GeneratedByUserId" 
        FOREIGN KEY ("GeneratedByUserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "UQ_KbankOddRegistration_OtacCode" 
        UNIQUE ("OtacCode"),
    CONSTRAINT "CK_KbankOddRegistration_OtacState" 
        CHECK ("OtacState" IN ('Generated', 'Validated', 'Used'))
);

-- Create comprehensive indexes for KbankOddRegistration table
CREATE UNIQUE INDEX "IX_KbankOddRegistration_ExternalReference" 
    ON "KbankOddRegistration" ("ExternalReference");
CREATE INDEX "IX_KbankOddRegistration_RegId" 
    ON "KbankOddRegistration" ("RegId");
CREATE INDEX "IX_KbankOddRegistration_Status" 
    ON "KbankOddRegistration" ("Status");
CREATE INDEX "IX_KbankOddRegistration_CreatedAt" 
    ON "KbankOddRegistration" ("CreatedAt");
CREATE INDEX "IX_KbankOddRegistration_BranchId" 
    ON "KbankOddRegistration" ("BranchId");
CREATE INDEX "IX_KbankOddRegistration_OtacCode" 
    ON "KbankOddRegistration" ("OtacCode");
CREATE INDEX "IX_KbankOddRegistration_OtacState" 
    ON "KbankOddRegistration" ("OtacState");
CREATE INDEX "IX_KbankOddRegistration_OtacExpiresAt" 
    ON "KbankOddRegistration" ("OtacExpiresAt");
CREATE INDEX "IX_KbankOddRegistration_GeneratedByUserId" 
    ON "KbankOddRegistration" ("GeneratedByUserId");
CREATE INDEX "IX_KbankOddRegistration_IdType_IdValue" 
    ON "KbankOddRegistration" ("IdType", "IdValue");

-- Composite indexes for common business queries
CREATE INDEX "IX_KbankOddRegistration_OtacCode_State_Expires" 
    ON "KbankOddRegistration" ("OtacCode", "OtacState", "OtacExpiresAt");
CREATE INDEX "IX_KbankOddRegistration_State_Status_Created" 
    ON "KbankOddRegistration" ("OtacState", "Status", "CreatedAt");

-- Create trigger to automatically update UpdatedAt on KbankOddRegistration table
DROP TRIGGER IF EXISTS update_kbankoddregistration_updated_at ON "KbankOddRegistration";
CREATE TRIGGER update_kbankoddregistration_updated_at
    BEFORE UPDATE ON "KbankOddRegistration"
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

-- Add comprehensive comments for KbankOddRegistration table
COMMENT ON TABLE "KbankOddRegistration" IS 'Consolidated table tracking KBank Online Direct Debit registration requests with integrated OTAC functionality';

-- KBank ODD Fields Comments
COMMENT ON COLUMN "KbankOddRegistration"."ExternalReference" IS 'Unique external reference generated by BizConnect (format: BIZyyyyMMddHHmmssfff)';
COMMENT ON COLUMN "KbankOddRegistration"."RegId" IS 'Registration ID returned by KBank after initialization';
COMMENT ON COLUMN "KbankOddRegistration"."EspaId" IS 'ESPA ID returned by KBank after successful registration';
COMMENT ON COLUMN "KbankOddRegistration"."Status" IS 'Registration status: Pending, Success, or Fail';
COMMENT ON COLUMN "KbankOddRegistration"."ReturnCode" IS 'Return code from KBank status update';

-- Customer Information Comments
COMMENT ON COLUMN "KbankOddRegistration"."IdType" IS 'Type of identification: National ID, Passport, Tax ID, or Company Tax ID';
COMMENT ON COLUMN "KbankOddRegistration"."IdValue" IS 'Identification document number/value corresponding to the selected ID type';
COMMENT ON COLUMN "KbankOddRegistration"."FullName" IS 'Customer full name for registration';
COMMENT ON COLUMN "KbankOddRegistration"."MobileNo" IS 'Customer mobile number in format 08xxxxxxxx or +66xxxxxxxx';
COMMENT ON COLUMN "KbankOddRegistration"."AccountNo" IS 'Bank account number for the ODD registration (10-15 digits)';
COMMENT ON COLUMN "KbankOddRegistration"."BranchId" IS 'Foreign key reference to Branch table';

-- OTAC Integration Comments
COMMENT ON COLUMN "KbankOddRegistration"."OtacCode" IS 'The actual OTAC code (8-character alphanumeric)';
COMMENT ON COLUMN "KbankOddRegistration"."OtacState" IS 'OTAC state: Generated → Validated → Used';
COMMENT ON COLUMN "KbankOddRegistration"."GeneratedByUserId" IS 'User ID who generated this OTAC code';
COMMENT ON COLUMN "KbankOddRegistration"."AttemptCount" IS 'Number of OTAC validation attempts made';
COMMENT ON COLUMN "KbankOddRegistration"."IsLocked" IS 'TRUE if OTAC is locked due to too many failed attempts';
COMMENT ON COLUMN "KbankOddRegistration"."LastAttemptAt" IS 'Timestamp of last OTAC validation attempt';
COMMENT ON COLUMN "KbankOddRegistration"."LastAttemptIp" IS 'IP address of last OTAC validation attempt';
COMMENT ON COLUMN "KbankOddRegistration"."OtacExpiresAt" IS 'When the OTAC code expires (typically 30 minutes from creation)';

-- Audit Comments
COMMENT ON COLUMN "KbankOddRegistration"."CreatedAt" IS 'Timestamp when record was created';
COMMENT ON COLUMN "KbankOddRegistration"."UpdatedAt" IS 'Timestamp when record was last updated (auto-updated by trigger)';

-- ====================================================================
-- SECTION 7: MULTI-LANGUAGE HELPER FUNCTIONS
-- ====================================================================

-- Create function to get localized branch name
CREATE OR REPLACE FUNCTION get_branch_name(branch_id INTEGER, language_code VARCHAR(2) DEFAULT 'en')
RETURNS VARCHAR(100) AS $$
DECLARE
    result VARCHAR(100);
BEGIN
    SELECT 
        CASE 
            WHEN language_code = 'th' AND "NameTh" IS NOT NULL THEN "NameTh"
            WHEN language_code = 'en' AND "NameEn" IS NOT NULL THEN "NameEn"
            ELSE COALESCE("NameEn", "NameTh", "Name")
        END
    INTO result
    FROM "Branch"
    WHERE "BranchId" = branch_id;
    
    RETURN COALESCE(result, 'Unknown Branch');
END;
$$ LANGUAGE plpgsql;

-- Create function to get localized branch address
CREATE OR REPLACE FUNCTION get_branch_address(branch_id INTEGER, language_code VARCHAR(2) DEFAULT 'en')
RETURNS TEXT AS $$
DECLARE
    result TEXT;
BEGIN
    SELECT 
        CASE 
            WHEN language_code = 'th' AND "AddressTh" IS NOT NULL THEN "AddressTh"
            WHEN language_code = 'en' AND "AddressEn" IS NOT NULL THEN "AddressEn"
            ELSE COALESCE("AddressEn", "AddressTh", "Address")
        END
    INTO result
    FROM "Branch"
    WHERE "BranchId" = branch_id;
    
    RETURN COALESCE(result, 'Unknown Address');
END;
$$ LANGUAGE plpgsql;

-- Add comments for helper functions
COMMENT ON FUNCTION get_branch_name(INTEGER, VARCHAR) IS 'Returns localized branch name based on language code (th/en)';
COMMENT ON FUNCTION get_branch_address(INTEGER, VARCHAR) IS 'Returns localized branch address based on language code (th/en)';

-- ====================================================================
-- SECTION 8: PERFORMANCE AND MAINTENANCE VIEWS
-- ====================================================================

-- Create view for active ODD registrations with branch information
CREATE OR REPLACE VIEW "ActiveOddRegistrations" AS
SELECT 
    ko."Id",
    ko."ExternalReference",
    ko."RegId",
    ko."EspaId",
    ko."Status",
    ko."IdType",
    ko."IdValue",
    ko."FullName",
    ko."MobileNo",
    ko."AccountNo",
    ko."OtacCode",
    ko."OtacState",
    ko."AttemptCount",
    ko."IsLocked",
    ko."CreatedAt",
    ko."UpdatedAt",
    b."Code" AS "BranchCode",
    b."Name" AS "BranchName",
    b."NameTh" AS "BranchNameTh",
    b."NameEn" AS "BranchNameEn",
    u."Username" AS "GeneratedByUsername"
FROM "KbankOddRegistration" ko
LEFT JOIN "Branch" b ON ko."BranchId" = b."BranchId"
LEFT JOIN "Users" u ON ko."GeneratedByUserId" = u."Id"
WHERE ko."Status" NOT IN ('Success', 'Fail')
  AND (ko."OtacExpiresAt" IS NULL OR ko."OtacExpiresAt" > NOW());

-- Create view for expired OTAC codes that need cleanup
CREATE OR REPLACE VIEW "ExpiredOtacCodes" AS
SELECT 
    "Id",
    "ExternalReference",
    "OtacCode",
    "OtacState",
    "Status",
    "OtacExpiresAt",
    "CreatedAt"
FROM "KbankOddRegistration"
WHERE "OtacExpiresAt" IS NOT NULL 
  AND "OtacExpiresAt" < NOW()
  AND "Status" NOT IN ('Success', 'Fail');

-- Add comments for views
COMMENT ON VIEW "ActiveOddRegistrations" IS 'Active ODD registrations with branch and user information for admin dashboard';
COMMENT ON VIEW "ExpiredOtacCodes" IS 'Expired OTAC codes that need cleanup by background jobs';

-- ====================================================================
-- SECTION 9: FINAL VALIDATION AND CLEANUP
-- ====================================================================

-- Record all previous migrations as applied (to prevent re-running)
INSERT INTO "_SchemaVersion" ("Filename") VALUES
    ('20250710-01_InitialSchema.sql'),
    ('20250713-01_CreateKbankOddRegistration.sql'),
    ('20250713-02_AddContactColumns.sql'),
    ('20250803-01_AddKbankRequiredColumns.sql'),
    ('20250803-02_CreateBranchTable.sql'),
    ('20250803-03_CreateHangfireTables.sql'),
    ('20250803-04_CreateOtacTable.sql'),
    ('20250803-05_UpdateKbankRegistrationV197.sql'),
    ('20250803-06_AddOtacKbankRegistrationLink.sql'),
    ('20250804-01_MergeOtacIntoKbankRegistration.sql'),
    ('20250805-01_AddHangfireAggregatedCounter.sql'),
    ('20250805-02_ConvertTimestampToTimestamptz.sql'),
    ('20250805-03_ConsolidatedSchema.sql')
ON CONFLICT ("Filename") DO NOTHING;

-- Final validation
DO $$
DECLARE
    table_count INTEGER;
    index_count INTEGER;
    constraint_count INTEGER;
BEGIN
    -- Count tables in public schema
    SELECT COUNT(*) INTO table_count
    FROM information_schema.tables 
    WHERE table_schema = 'public' AND table_type = 'BASE TABLE';
    
    -- Count indexes
    SELECT COUNT(*) INTO index_count
    FROM pg_indexes 
    WHERE schemaname = 'public';
    
    -- Count constraints
    SELECT COUNT(*) INTO constraint_count
    FROM information_schema.table_constraints 
    WHERE table_schema = 'public';
    
    RAISE NOTICE 'Schema validation complete - Tables: %, Indexes: %, Constraints: %', 
                table_count, index_count, constraint_count;
                
    IF table_count < 3 THEN
        RAISE EXCEPTION 'Schema validation failed - insufficient tables created';
    END IF;
    
    RAISE NOTICE 'Consolidated schema migration completed successfully!';
    RAISE NOTICE 'Multi-language support enabled for Thai/English UI toggle';
    RAISE NOTICE 'Database is ready for Entity Framework scaffolding';
END $$;

COMMIT;

-- ====================================================================
-- POST-MIGRATION INSTRUCTIONS
-- ====================================================================
/*
NEXT STEPS:

1. Run this migration using the update-db script:
   ./scripts/update-db

2. The script will create a clean database schema with:
   - Users table with authentication
   - Branch table with Thai/English multi-language support
   - KbankOddRegistration table with integrated OTAC functionality
   - Proper indexes and constraints for performance
   - Helper functions for language localization
   - Views for common business queries

3. Hangfire tables should be in a separate database. Update connection strings:
   - Main database: Users, Branch, KbankOddRegistration
   - Hangfire database: All Hangfire job storage tables

4. For EF Core scaffolding, use:
   dotnet ef dbcontext scaffold "ConnectionString" Npgsql.EntityFrameworkCore.PostgreSQL

5. Multi-language usage in Razor views:
   @await GetBranchNameAsync(branchId, culture)

SCHEMA SUMMARY:
- 3 main tables: _SchemaVersion, Users, Branch, KbankOddRegistration
- Multi-language columns: NameTh/NameEn, AddressTh/AddressEn
- No Hangfire tables in main database
- No backup tables
- All timestamps use TIMESTAMPTZ for proper UTC handling
- Comprehensive indexing for performance
- Business flow: Generate OTAC → Validate → Submit → KBank Callback
*/