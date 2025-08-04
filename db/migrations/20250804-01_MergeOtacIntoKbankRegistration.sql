-- BizConnect Database Redesign: KBank ODD Registration Table Consolidation
-- Migration: 20250804-01_MergeOtacIntoKbankRegistration.sql
-- Description: Merges OtacCode and KbankOddRegistration tables into a single consolidated table
--              This reflects the business reality that OTAC generation is the start of ODD registration
--
-- Business Flow Supported:
-- Step 1: Employee generates OTAC → insert with OtacState='Generated'
-- Step 2: Guest validates OTAC → update OtacState='Validated'  
-- Step 3: Guest submits form → update OtacState='Used', Status='Pending'
-- Step 4: KBank callback → update Status='Success' or 'Fail'

BEGIN;

-- Create backup table for existing OtacCode data (if table exists)
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'OtacCode' AND table_schema = 'public') THEN
        -- Create backup table
        DROP TABLE IF EXISTS "_OtacCode_Backup_20250804";
        CREATE TABLE "_OtacCode_Backup_20250804" AS SELECT * FROM "OtacCode";
        
        RAISE NOTICE 'Created backup table _OtacCode_Backup_20250804 with % rows', 
            (SELECT COUNT(*) FROM "_OtacCode_Backup_20250804");
    END IF;
END $$;

-- Create backup table for existing KbankOddRegistration data
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'KbankOddRegistration' AND table_schema = 'public') THEN
        -- Create backup table
        DROP TABLE IF EXISTS "_KbankOddRegistration_Backup_20250804";
        CREATE TABLE "_KbankOddRegistration_Backup_20250804" AS SELECT * FROM "KbankOddRegistration";
        
        RAISE NOTICE 'Created backup table _KbankOddRegistration_Backup_20250804 with % rows', 
            (SELECT COUNT(*) FROM "_KbankOddRegistration_Backup_20250804");
    END IF;
END $$;

-- Add new OTAC-related columns to KbankOddRegistration table
DO $$
BEGIN
    -- Add OtacCode column (VARCHAR(8), UNIQUE, NOT NULL)
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'KbankOddRegistration' 
        AND column_name = 'OtacCode'
        AND table_schema = 'public'
    ) THEN
        ALTER TABLE "KbankOddRegistration" 
        ADD COLUMN "OtacCode" VARCHAR(8) NULL; -- Start as NULL, will be made NOT NULL after data migration
        
        COMMENT ON COLUMN "KbankOddRegistration"."OtacCode" IS 'The actual OTAC code (8-character alphanumeric)';
    END IF;

    -- Add OtacState column (VARCHAR(20), NOT NULL, default 'Generated')
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'KbankOddRegistration' 
        AND column_name = 'OtacState'
        AND table_schema = 'public'
    ) THEN
        ALTER TABLE "KbankOddRegistration" 
        ADD COLUMN "OtacState" VARCHAR(20) NOT NULL DEFAULT 'Generated';
        
        COMMENT ON COLUMN "KbankOddRegistration"."OtacState" IS 'OTAC state: Generated → Validated → Used';
    END IF;

    -- Add GeneratedByUserId column (INT, NOT NULL, FK to Users)
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'KbankOddRegistration' 
        AND column_name = 'GeneratedByUserId'
        AND table_schema = 'public'
    ) THEN
        ALTER TABLE "KbankOddRegistration" 
        ADD COLUMN "GeneratedByUserId" INTEGER NULL; -- Start as NULL, will be made NOT NULL after data migration
        
        COMMENT ON COLUMN "KbankOddRegistration"."GeneratedByUserId" IS 'User ID who generated this OTAC code';
    END IF;

    -- Add AttemptCount column (INT, DEFAULT 0)
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'KbankOddRegistration' 
        AND column_name = 'AttemptCount'
        AND table_schema = 'public'
    ) THEN
        ALTER TABLE "KbankOddRegistration" 
        ADD COLUMN "AttemptCount" INTEGER NOT NULL DEFAULT 0;
        
        COMMENT ON COLUMN "KbankOddRegistration"."AttemptCount" IS 'Number of OTAC validation attempts made';
    END IF;

    -- Add IsLocked column (BOOLEAN, DEFAULT FALSE)
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'KbankOddRegistration' 
        AND column_name = 'IsLocked'
        AND table_schema = 'public'
    ) THEN
        ALTER TABLE "KbankOddRegistration" 
        ADD COLUMN "IsLocked" BOOLEAN NOT NULL DEFAULT FALSE;
        
        COMMENT ON COLUMN "KbankOddRegistration"."IsLocked" IS 'TRUE if OTAC is locked due to too many failed attempts';
    END IF;

    -- Add LastAttemptAt column (TIMESTAMP, NULL)
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'KbankOddRegistration' 
        AND column_name = 'LastAttemptAt'
        AND table_schema = 'public'
    ) THEN
        ALTER TABLE "KbankOddRegistration" 
        ADD COLUMN "LastAttemptAt" TIMESTAMPTZ NULL;
        
        COMMENT ON COLUMN "KbankOddRegistration"."LastAttemptAt" IS 'Timestamp of last OTAC validation attempt';
    END IF;

    -- Add LastAttemptIp column (VARCHAR(45), NULL)
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'KbankOddRegistration' 
        AND column_name = 'LastAttemptIp'
        AND table_schema = 'public'
    ) THEN
        ALTER TABLE "KbankOddRegistration" 
        ADD COLUMN "LastAttemptIp" VARCHAR(45) NULL;
        
        COMMENT ON COLUMN "KbankOddRegistration"."LastAttemptIp" IS 'IP address of last OTAC validation attempt';
    END IF;

    -- Rename CodeExpiresAt to OtacExpiresAt for clarity (if CodeExpiresAt exists)
    IF EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'KbankOddRegistration' 
        AND column_name = 'CodeExpiresAt'
        AND table_schema = 'public'
    ) AND NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'KbankOddRegistration' 
        AND column_name = 'OtacExpiresAt'
        AND table_schema = 'public'
    ) THEN
        ALTER TABLE "KbankOddRegistration" 
        RENAME COLUMN "CodeExpiresAt" TO "OtacExpiresAt";
        
        COMMENT ON COLUMN "KbankOddRegistration"."OtacExpiresAt" IS 'When the OTAC code expires (typically 30 minutes from creation)';
    ELSIF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'KbankOddRegistration' 
        AND column_name = 'OtacExpiresAt'
        AND table_schema = 'public'
    ) THEN
        ALTER TABLE "KbankOddRegistration" 
        ADD COLUMN "OtacExpiresAt" TIMESTAMPTZ NULL;
        
        COMMENT ON COLUMN "KbankOddRegistration"."OtacExpiresAt" IS 'When the OTAC code expires (typically 30 minutes from creation)';
    END IF;

    RAISE NOTICE 'Added all OTAC-related columns to KbankOddRegistration table';
END $$;

-- Migrate existing data from OtacCode to KbankOddRegistration (if OtacCode table exists)
DO $$
DECLARE
    otac_record RECORD;
    kbank_id INTEGER;
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'OtacCode' AND table_schema = 'public') THEN
        -- Process each OTAC code that has a linked KbankOddRegistration
        FOR otac_record IN 
            SELECT * FROM "OtacCode" 
            WHERE "KbankOddRegistrationId" IS NOT NULL
        LOOP
            -- Update the corresponding KbankOddRegistration record with OTAC data
            UPDATE "KbankOddRegistration" 
            SET 
                "OtacCode" = otac_record."Code",
                "OtacState" = CASE 
                    WHEN otac_record."IsUsed" = TRUE THEN 'Used'
                    WHEN otac_record."AttemptCount" > 0 THEN 'Validated'
                    ELSE 'Generated'
                END,
                "GeneratedByUserId" = otac_record."GeneratedByUserId",
                "AttemptCount" = otac_record."AttemptCount",
                "IsLocked" = otac_record."IsLocked",
                "LastAttemptAt" = otac_record."LastAttemptAt",
                "LastAttemptIp" = otac_record."LastAttemptIp",
                "OtacExpiresAt" = otac_record."ExpiresAt"
            WHERE "Id" = otac_record."KbankOddRegistrationId";
        END LOOP;

        -- Process orphaned OTAC codes (not linked to KbankOddRegistration)
        -- Create new KbankOddRegistration records for them
        FOR otac_record IN 
            SELECT * FROM "OtacCode" 
            WHERE "KbankOddRegistrationId" IS NULL
        LOOP
            -- Create new KbankOddRegistration record
            INSERT INTO "KbankOddRegistration" (
                "ExternalReference", 
                "RegId", 
                "Status", 
                "CreatedAt",
                "OtacCode",
                "OtacState",
                "GeneratedByUserId",
                "AttemptCount",
                "IsLocked",
                "LastAttemptAt",
                "LastAttemptIp",
                "OtacExpiresAt"
            ) VALUES (
                'MIGRATED_' || otac_record."Id", -- Temporary external reference
                'MIGRATED_' || otac_record."Id", -- Temporary reg ID
                CASE WHEN otac_record."IsUsed" THEN 'Used' ELSE 'Generated' END,
                otac_record."CreatedAt",
                otac_record."Code",
                CASE 
                    WHEN otac_record."IsUsed" = TRUE THEN 'Used'
                    WHEN otac_record."AttemptCount" > 0 THEN 'Validated'
                    ELSE 'Generated'
                END,
                otac_record."GeneratedByUserId",
                otac_record."AttemptCount",
                otac_record."IsLocked",
                otac_record."LastAttemptAt",
                otac_record."LastAttemptIp",
                otac_record."ExpiresAt"
            );
        END LOOP;

        RAISE NOTICE 'Migrated data from OtacCode to KbankOddRegistration';
    END IF;
END $$;

-- Make OtacCode and GeneratedByUserId NOT NULL after data migration
DO $$
DECLARE
    counter INTEGER := 1;
BEGIN
    -- Update any remaining NULL values with unique default values
    UPDATE "KbankOddRegistration" 
    SET "OtacCode" = 'MIG' || LPAD("Id"::TEXT, 5, '0') 
    WHERE "OtacCode" IS NULL;
    
    UPDATE "KbankOddRegistration" 
    SET "GeneratedByUserId" = 1 
    WHERE "GeneratedByUserId" IS NULL 
    AND EXISTS (SELECT 1 FROM "Users" WHERE "Id" = 1);

    -- Make columns NOT NULL
    ALTER TABLE "KbankOddRegistration" 
    ALTER COLUMN "OtacCode" SET NOT NULL;
    
    ALTER TABLE "KbankOddRegistration" 
    ALTER COLUMN "GeneratedByUserId" SET NOT NULL;
END $$;

-- Add constraints and foreign keys
DO $$
BEGIN
    -- Add unique constraint on OtacCode
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.table_constraints 
        WHERE constraint_name = 'UQ_KbankOddRegistration_OtacCode'
        AND table_name = 'KbankOddRegistration'
        AND table_schema = 'public'
    ) THEN
        ALTER TABLE "KbankOddRegistration"
        ADD CONSTRAINT "UQ_KbankOddRegistration_OtacCode" 
        UNIQUE ("OtacCode");
    END IF;

    -- Add foreign key constraint to Users table
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.table_constraints 
        WHERE constraint_name = 'FK_KbankOddRegistration_GeneratedByUserId'
        AND table_name = 'KbankOddRegistration'
        AND table_schema = 'public'
    ) THEN
        ALTER TABLE "KbankOddRegistration"
        ADD CONSTRAINT "FK_KbankOddRegistration_GeneratedByUserId" 
        FOREIGN KEY ("GeneratedByUserId") 
        REFERENCES "Users" ("Id") 
        ON DELETE RESTRICT ON UPDATE CASCADE;
    END IF;

    -- Add check constraint for OtacState values
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.table_constraints 
        WHERE constraint_name = 'CK_KbankOddRegistration_OtacState'
        AND table_name = 'KbankOddRegistration'
        AND table_schema = 'public'
    ) THEN
        ALTER TABLE "KbankOddRegistration"
        ADD CONSTRAINT "CK_KbankOddRegistration_OtacState" 
        CHECK ("OtacState" IN ('Generated', 'Validated', 'Used'));
    END IF;

    RAISE NOTICE 'Added constraints and foreign keys';
END $$;

-- Create performance indexes
DO $$
BEGIN
    -- Index on OtacCode for fast lookup
    IF NOT EXISTS (
        SELECT 1 FROM pg_indexes 
        WHERE indexname = 'IX_KbankOddRegistration_OtacCode'
        AND tablename = 'KbankOddRegistration'
        AND schemaname = 'public'
    ) THEN
        CREATE INDEX "IX_KbankOddRegistration_OtacCode" 
        ON "KbankOddRegistration" ("OtacCode");
    END IF;

    -- Index on OtacState for state-based queries
    IF NOT EXISTS (
        SELECT 1 FROM pg_indexes 
        WHERE indexname = 'IX_KbankOddRegistration_OtacState'
        AND tablename = 'KbankOddRegistration'
        AND schemaname = 'public'
    ) THEN
        CREATE INDEX "IX_KbankOddRegistration_OtacState" 
        ON "KbankOddRegistration" ("OtacState");
    END IF;

    -- Index on Status for registration status queries
    IF NOT EXISTS (
        SELECT 1 FROM pg_indexes 
        WHERE indexname = 'IX_KbankOddRegistration_Status'
        AND tablename = 'KbankOddRegistration'
        AND schemaname = 'public'
    ) THEN
        CREATE INDEX "IX_KbankOddRegistration_Status" 
        ON "KbankOddRegistration" ("Status");
    END IF;

    -- Index on OtacExpiresAt for cleanup jobs
    IF NOT EXISTS (
        SELECT 1 FROM pg_indexes 
        WHERE indexname = 'IX_KbankOddRegistration_OtacExpiresAt'
        AND tablename = 'KbankOddRegistration'
        AND schemaname = 'public'
    ) THEN
        CREATE INDEX "IX_KbankOddRegistration_OtacExpiresAt" 
        ON "KbankOddRegistration" ("OtacExpiresAt");
    END IF;

    -- Index on GeneratedByUserId for user-based queries
    IF NOT EXISTS (
        SELECT 1 FROM pg_indexes 
        WHERE indexname = 'IX_KbankOddRegistration_GeneratedByUserId'
        AND tablename = 'KbankOddRegistration'
        AND schemaname = 'public'
    ) THEN
        CREATE INDEX "IX_KbankOddRegistration_GeneratedByUserId" 
        ON "KbankOddRegistration" ("GeneratedByUserId");
    END IF;

    -- Composite index for OTAC validation queries
    IF NOT EXISTS (
        SELECT 1 FROM pg_indexes 
        WHERE indexname = 'IX_KbankOddRegistration_OtacCode_State_Expires'
        AND tablename = 'KbankOddRegistration'
        AND schemaname = 'public'
    ) THEN
        CREATE INDEX "IX_KbankOddRegistration_OtacCode_State_Expires" 
        ON "KbankOddRegistration" ("OtacCode", "OtacState", "OtacExpiresAt");
    END IF;

    -- Composite index for business flow queries
    IF NOT EXISTS (
        SELECT 1 FROM pg_indexes 
        WHERE indexname = 'IX_KbankOddRegistration_State_Status_Created'
        AND tablename = 'KbankOddRegistration'
        AND schemaname = 'public'
    ) THEN
        CREATE INDEX "IX_KbankOddRegistration_State_Status_Created" 
        ON "KbankOddRegistration" ("OtacState", "Status", "CreatedAt");
    END IF;

    RAISE NOTICE 'Created performance indexes';
END $$;

-- Drop the OtacCode table and its dependencies
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'OtacCode' AND table_schema = 'public') THEN
        -- Drop foreign key constraints first
        ALTER TABLE "OtacCode" DROP CONSTRAINT IF EXISTS "FK_OtacCode_KbankOddRegistrationId";
        ALTER TABLE "OtacCode" DROP CONSTRAINT IF EXISTS "FK_OtacCode_GeneratedByUserId";
        
        -- Drop all indexes
        DROP INDEX IF EXISTS "IX_OtacCode_Code";
        DROP INDEX IF EXISTS "IX_OtacCode_Purpose";
        DROP INDEX IF EXISTS "IX_OtacCode_IssuedTo";
        DROP INDEX IF EXISTS "IX_OtacCode_ExpiresAt";
        DROP INDEX IF EXISTS "IX_OtacCode_IsUsed";
        DROP INDEX IF EXISTS "IX_OtacCode_IsLocked";
        DROP INDEX IF EXISTS "IX_OtacCode_CreatedAt";
        DROP INDEX IF EXISTS "IX_OtacCode_GeneratedByUserId";
        DROP INDEX IF EXISTS "IX_OtacCode_Code_IsUsed_ExpiresAt";
        DROP INDEX IF EXISTS "IX_OtacCode_Purpose_IssuedTo_IsUsed";
        DROP INDEX IF EXISTS "IX_OtacCode_KbankOddRegistrationId";
        DROP INDEX IF EXISTS "IX_OtacCode_KbankRegistration_Purpose_IsUsed";
        
        -- Drop the table
        DROP TABLE "OtacCode";
        
        RAISE NOTICE 'Dropped OtacCode table and all its dependencies';
    END IF;
END $$;

-- Update table comment
COMMENT ON TABLE "KbankOddRegistration" IS 'Consolidated table tracking KBank Online Direct Debit registration requests with integrated OTAC functionality';

COMMIT;

-- Record this migration in schema version tracking
INSERT INTO "_SchemaVersion" ("Filename")
VALUES ('20250804-01_MergeOtacIntoKbankRegistration.sql')
ON CONFLICT ("Filename") DO NOTHING;

/*
ROLLBACK INSTRUCTIONS (for emergency use only):
This migration cannot be easily rolled back due to the destructive nature of dropping the OtacCode table.
The backup tables _OtacCode_Backup_20250804 and _KbankOddRegistration_Backup_20250804 contain the original data.

To partially rollback (NOT RECOMMENDED in production):
1. Restore from backup tables
2. Recreate OtacCode table from backup
3. Remove added columns from KbankOddRegistration

BUSINESS FLOW AFTER MIGRATION:
1. Employee generates OTAC → INSERT with OtacState='Generated'
2. Guest validates OTAC → UPDATE OtacState='Validated'
3. Guest submits form → UPDATE OtacState='Used', Status='Pending'
4. KBank callback → UPDATE Status='Success' or 'Fail'

The table now supports both OTAC lifecycle and ODD registration status in a single entity,
reflecting the business reality that they are the same process.
*/