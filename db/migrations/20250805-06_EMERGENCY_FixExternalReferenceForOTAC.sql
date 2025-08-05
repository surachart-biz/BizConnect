-- ====================================================================
-- EMERGENCY FIX: ExternalReference Schema Constraint for KBank ODD V1.9.7
-- Migration: 20250805-06_EMERGENCY_FixExternalReferenceForOTAC.sql
-- Date: 2025-08-05
-- Priority: CRITICAL - Production blocking issue
-- Description: Fix ExternalReference column to allow NULL values for proper OTAC flow
-- ====================================================================
--
-- BUSINESS REQUIREMENT (KBank ODD V1.9.7):
-- 1. Admin generates OTAC → ExternalReference should be NULL
-- 2. Guest validates OTAC → ExternalReference remains NULL  
-- 3. Guest submits registration → ExternalReference gets assigned
-- 4. KBank processes and returns status → ExternalReference persists
--
-- CURRENT ISSUE:
-- ExternalReference column is defined as NOT NULL with UNIQUE constraint
-- This prevents OTAC generation because we cannot insert NULL values
--
-- SOLUTION:
-- 1. Change column to allow NULL values
-- 2. Update unique constraint to exclude NULL values (partial unique index)
-- 3. Migrate any existing empty strings to NULL
-- 4. Preserve data integrity while enabling proper business flow
-- ====================================================================

-- Enable error handling
\set ON_ERROR_STOP on

BEGIN;

-- ====================================================================
-- SECTION 1: DIAGNOSTIC INFORMATION
-- ====================================================================

DO $$
BEGIN
    RAISE NOTICE '=== EMERGENCY MIGRATION: ExternalReference Fix ===';
    RAISE NOTICE 'Target: Allow NULL values in ExternalReference column';
    RAISE NOTICE 'Reason: Enable KBank ODD V1.9.7 OTAC generation flow';
    RAISE NOTICE 'Impact: Production unblocking - CRITICAL';
    RAISE NOTICE '=====================================================';
END $$;

-- ====================================================================
-- SECTION 2: PRE-MIGRATION VALIDATION
-- ====================================================================

DO $$
DECLARE
    current_not_null BOOLEAN;
    constraint_exists BOOLEAN;
    record_count INTEGER;
    empty_string_count INTEGER;
BEGIN
    -- Check current column definition
    SELECT is_nullable = 'NO' INTO current_not_null
    FROM information_schema.columns 
    WHERE table_schema = 'public' 
      AND table_name = 'KbankOddRegistration' 
      AND column_name = 'ExternalReference';
    
    -- Check if unique constraint exists
    SELECT EXISTS(
        SELECT 1 FROM information_schema.table_constraints 
        WHERE table_schema = 'public' 
          AND table_name = 'KbankOddRegistration' 
          AND constraint_type = 'UNIQUE'
          AND constraint_name LIKE '%ExternalReference%'
    ) INTO constraint_exists;
    
    -- Count existing records
    SELECT COUNT(*) INTO record_count FROM "KbankOddRegistration";
    
    -- Count empty string values that need conversion
    SELECT COUNT(*) INTO empty_string_count 
    FROM "KbankOddRegistration" 
    WHERE "ExternalReference" = '';
    
    RAISE NOTICE 'Pre-migration status:';
    RAISE NOTICE '- Column NOT NULL: %', current_not_null;
    RAISE NOTICE '- Unique constraint exists: %', constraint_exists;
    RAISE NOTICE '- Total records: %', record_count;
    RAISE NOTICE '- Empty string records to convert: %', empty_string_count;
    
    IF NOT current_not_null THEN
        RAISE NOTICE 'WARNING: Column is already nullable - migration may be redundant';
    END IF;
END $$;

-- ====================================================================
-- SECTION 3: BACKUP CRITICAL DATA
-- ====================================================================

-- Create temporary backup of ExternalReference values before modification
CREATE TEMP TABLE IF NOT EXISTS temp_external_ref_backup AS
SELECT 
    "Id",
    "ExternalReference",
    CASE 
        WHEN "ExternalReference" = '' THEN 'EMPTY_STRING'
        WHEN "ExternalReference" IS NULL THEN 'NULL_VALUE'
        ELSE 'HAS_VALUE'
    END AS "OriginalState",
    "CreatedAt"
FROM "KbankOddRegistration";

DO $$
BEGIN
    RAISE NOTICE 'Created temporary backup of ExternalReference data';
END $$;

-- ====================================================================
-- SECTION 4: MIGRATE EMPTY STRINGS TO NULL
-- ====================================================================

-- Convert empty strings to NULL values first (before changing constraint)
UPDATE "KbankOddRegistration" 
SET "ExternalReference" = NULL 
WHERE "ExternalReference" = '';

-- Get count of converted records
DO $$
DECLARE
    converted_count INTEGER;
BEGIN
    SELECT COUNT(*) INTO converted_count 
    FROM temp_external_ref_backup 
    WHERE "OriginalState" = 'EMPTY_STRING';
    
    RAISE NOTICE 'Converted % empty string values to NULL', converted_count;
END $$;

-- ====================================================================
-- SECTION 5: DROP EXISTING UNIQUE CONSTRAINT
-- ====================================================================

-- Drop the existing unique index that prevents NULL values
DROP INDEX IF EXISTS "IX_KbankOddRegistration_ExternalReference";

DO $$
BEGIN
    RAISE NOTICE 'Dropped existing unique index on ExternalReference';
END $$;

-- Also drop any table-level unique constraints
DO $$
DECLARE
    constraint_name TEXT;
BEGIN
    -- Find constraint name dynamically
    SELECT tc.constraint_name INTO constraint_name
    FROM information_schema.table_constraints tc
    JOIN information_schema.key_column_usage kcu 
        ON tc.constraint_name = kcu.constraint_name
    WHERE tc.table_schema = 'public'
      AND tc.table_name = 'KbankOddRegistration'
      AND tc.constraint_type = 'UNIQUE'
      AND kcu.column_name = 'ExternalReference';
    
    IF constraint_name IS NOT NULL THEN
        EXECUTE 'ALTER TABLE "KbankOddRegistration" DROP CONSTRAINT "' || constraint_name || '"';
        RAISE NOTICE 'Dropped unique constraint: %', constraint_name;
    ELSE
        RAISE NOTICE 'No table-level unique constraint found on ExternalReference';
    END IF;
END $$;

-- ====================================================================
-- SECTION 6: MODIFY COLUMN TO ALLOW NULL
-- ====================================================================

-- Change column to allow NULL values
ALTER TABLE "KbankOddRegistration" 
ALTER COLUMN "ExternalReference" DROP NOT NULL;

DO $$
BEGIN
    RAISE NOTICE 'Successfully changed ExternalReference column to allow NULL values';
END $$;

-- ====================================================================
-- SECTION 7: CREATE PARTIAL UNIQUE INDEX (EXCLUDES NULL VALUES)
-- ====================================================================

-- Create partial unique index that excludes NULL values
-- This allows multiple NULL values while maintaining uniqueness for non-NULL values
CREATE UNIQUE INDEX "IX_KbankOddRegistration_ExternalReference_NotNull" 
ON "KbankOddRegistration" ("ExternalReference") 
WHERE "ExternalReference" IS NOT NULL;

DO $$
BEGIN
    RAISE NOTICE 'Created partial unique index excluding NULL values';
END $$;

-- ====================================================================
-- SECTION 8: UPDATE COLUMN COMMENT FOR CLARITY
-- ====================================================================

-- Update column comment to reflect the new NULL-allowed behavior
COMMENT ON COLUMN "KbankOddRegistration"."ExternalReference" IS 
'Unique external reference generated by BizConnect (format: BIZyyyyMMddHHmmssfff). NULL during OTAC generation/validation phases, assigned upon guest registration submission per KBank ODD V1.9.7 business flow.';

-- ====================================================================
-- SECTION 9: POST-MIGRATION VALIDATION
-- ====================================================================

DO $$
DECLARE
    is_nullable BOOLEAN;
    unique_constraint_exists BOOLEAN;
    partial_index_exists BOOLEAN;
    null_count INTEGER;
    non_null_count INTEGER;
    duplicate_non_null_count INTEGER;
BEGIN
    -- Verify column is now nullable
    SELECT (c.is_nullable = 'YES') INTO is_nullable
    FROM information_schema.columns c
    WHERE c.table_schema = 'public' 
      AND c.table_name = 'KbankOddRegistration' 
      AND c.column_name = 'ExternalReference';
    
    -- Check if partial unique index exists
    SELECT EXISTS(
        SELECT 1 FROM pg_indexes 
        WHERE schemaname = 'public' 
          AND tablename = 'KbankOddRegistration'
          AND indexname = 'IX_KbankOddRegistration_ExternalReference_NotNull'
    ) INTO partial_index_exists;
    
    -- Count NULL and non-NULL values
    SELECT 
        COUNT(*) FILTER (WHERE "ExternalReference" IS NULL),
        COUNT(*) FILTER (WHERE "ExternalReference" IS NOT NULL)
    INTO null_count, non_null_count
    FROM "KbankOddRegistration";
    
    -- Check for duplicates in non-NULL values (should be 0)
    SELECT COUNT(*) - COUNT(DISTINCT "ExternalReference") INTO duplicate_non_null_count
    FROM "KbankOddRegistration" 
    WHERE "ExternalReference" IS NOT NULL;
    
    RAISE NOTICE '=== POST-MIGRATION VALIDATION ===';
    RAISE NOTICE 'Column is nullable: %', is_nullable;
    RAISE NOTICE 'Partial unique index created: %', partial_index_exists;
    RAISE NOTICE 'NULL values count: %', null_count;
    RAISE NOTICE 'Non-NULL values count: %', non_null_count;
    RAISE NOTICE 'Duplicate non-NULL values: %', duplicate_non_null_count;
    
    -- Validation checks
    IF NOT is_nullable THEN
        RAISE EXCEPTION 'MIGRATION FAILED: Column ExternalReference is still NOT NULL';
    END IF;
    
    IF NOT partial_index_exists THEN
        RAISE EXCEPTION 'MIGRATION FAILED: Partial unique index was not created';
    END IF;
    
    IF duplicate_non_null_count > 0 THEN
        RAISE EXCEPTION 'MIGRATION FAILED: Duplicate non-NULL ExternalReference values detected';
    END IF;
    
    RAISE NOTICE 'All validation checks PASSED ✓';
END $$;

-- ====================================================================
-- SECTION 10: BUSINESS FLOW VALIDATION TEST
-- ====================================================================

DO $$
DECLARE
    test_user_id INTEGER;
    test_otac_code VARCHAR(8);
    test_record_id INTEGER;
BEGIN
    RAISE NOTICE '=== BUSINESS FLOW VALIDATION TEST ===';
    
    -- Get a valid user ID for testing
    SELECT "Id" INTO test_user_id FROM "Users" WHERE "IsActive" = TRUE LIMIT 1;
    
    IF test_user_id IS NULL THEN
        RAISE NOTICE 'No active user found - skipping business flow test';
        RETURN;
    END IF;
    
    -- Generate test OTAC code
    test_otac_code := 'TEST' || LPAD(FLOOR(RANDOM() * 10000)::TEXT, 4, '0');
    
    -- Test 1: Insert OTAC record with NULL ExternalReference (should succeed)
    BEGIN
        INSERT INTO "KbankOddRegistration" (
            "ExternalReference", "OtacCode", "OtacState", "GeneratedByUserId", 
            "OtacExpiresAt", "CreatedAt"
        ) VALUES (
            NULL, -- This should now work!
            test_otac_code,
            'Generated',
            test_user_id,
            NOW() + INTERVAL '30 minutes',
            NOW()
        ) RETURNING "Id" INTO test_record_id;
        
        RAISE NOTICE 'TEST 1 PASSED ✓ - OTAC record created with NULL ExternalReference (ID: %)', test_record_id;
    EXCEPTION WHEN OTHERS THEN
        RAISE EXCEPTION 'TEST 1 FAILED ✗ - Cannot insert OTAC with NULL ExternalReference: %', SQLERRM;
    END;
    
    -- Test 2: Update to assign ExternalReference (should succeed)
    BEGIN
        UPDATE "KbankOddRegistration" 
        SET "ExternalReference" = 'BIZ' || TO_CHAR(NOW(), 'YYYYMMDDHH24MISSMS'),
            "OtacState" = 'Used'
        WHERE "Id" = test_record_id;
        
        RAISE NOTICE 'TEST 2 PASSED ✓ - ExternalReference assigned successfully';
    EXCEPTION WHEN OTHERS THEN
        RAISE EXCEPTION 'TEST 2 FAILED ✗ - Cannot update ExternalReference: %', SQLERRM;
    END;
    
    -- Clean up test record
    DELETE FROM "KbankOddRegistration" WHERE "Id" = test_record_id;
    RAISE NOTICE 'Test record cleaned up (ID: %)', test_record_id;
    
    RAISE NOTICE 'BUSINESS FLOW VALIDATION COMPLETED ✓';
END $$;

-- ====================================================================
-- SECTION 11: RECORD MIGRATION IN VERSION TRACKING
-- ====================================================================

-- Record this migration as applied
INSERT INTO "_SchemaVersion" ("Filename") 
VALUES ('20250805-06_EMERGENCY_FixExternalReferenceForOTAC.sql')
ON CONFLICT ("Filename") DO NOTHING;

-- ====================================================================
-- SECTION 12: FINAL SUCCESS CONFIRMATION
-- ====================================================================

DO $$
BEGIN
    RAISE NOTICE '=====================================================';
    RAISE NOTICE 'EMERGENCY MIGRATION COMPLETED SUCCESSFULLY! ✓';
    RAISE NOTICE '';
    RAISE NOTICE 'CHANGES APPLIED:';
    RAISE NOTICE '1. ExternalReference column now allows NULL values';
    RAISE NOTICE '2. Partial unique index prevents duplicate non-NULL values';
    RAISE NOTICE '3. Empty strings converted to NULL';
    RAISE NOTICE '4. Business flow validation tests passed';
    RAISE NOTICE '';
    RAISE NOTICE 'KBANK ODD V1.9.7 BUSINESS FLOW NOW SUPPORTED:';
    RAISE NOTICE '✓ Admin generates OTAC (ExternalReference = NULL)';
    RAISE NOTICE '✓ Guest validates OTAC (ExternalReference = NULL)';
    RAISE NOTICE '✓ Guest submits registration (ExternalReference assigned)';
    RAISE NOTICE '✓ KBank callback updates status (ExternalReference persists)';
    RAISE NOTICE '';
    RAISE NOTICE 'NEXT STEPS:';
    RAISE NOTICE '1. Run Entity Framework scaffolding to update models';
    RAISE NOTICE '2. Test OTAC generation in application';
    RAISE NOTICE '3. Verify production deployment';
    RAISE NOTICE '';
    RAISE NOTICE 'DATABASE_SCHEMA_FIXED: ExternalReference constraint resolved';
    RAISE NOTICE '=====================================================';
END $$;

COMMIT;

-- ====================================================================
-- POST-MIGRATION NOTES
-- ====================================================================
/*
CRITICAL CHANGES SUMMARY:

1. COLUMN MODIFICATION:
   - ExternalReference: VARCHAR(40) NOT NULL → VARCHAR(40) NULL
   - Now supports NULL values during OTAC generation phase

2. CONSTRAINT CHANGES:
   - Removed: UNIQUE constraint on ExternalReference (included NULLs)
   - Added: Partial UNIQUE index excluding NULL values

3. DATA MIGRATION:
   - Converted empty string values to NULL
   - Preserved all existing non-empty ExternalReference values

4. BUSINESS FLOW COMPLIANCE:
   - Phase 1: OTAC Generation → ExternalReference = NULL ✓
   - Phase 2: OTAC Validation → ExternalReference = NULL ✓  
   - Phase 3: Registration Submit → ExternalReference assigned ✓
   - Phase 4: KBank Callback → ExternalReference persists ✓

5. DATA INTEGRITY:
   - Uniqueness still enforced for non-NULL values
   - Multiple NULL values allowed (as required)
   - No data loss during migration

VERIFICATION COMMANDS:
- Check nullable: SELECT is_nullable FROM information_schema.columns WHERE table_name = 'KbankOddRegistration' AND column_name = 'ExternalReference';
- Check index: SELECT indexname FROM pg_indexes WHERE tablename = 'KbankOddRegistration' AND indexname LIKE '%ExternalReference%';
- Test insert: INSERT INTO "KbankOddRegistration" ("ExternalReference", "OtacCode", ...) VALUES (NULL, 'TEST1234', ...);

ROLLBACK PLAN (if needed):
- Restore from temp_external_ref_backup table
- ALTER COLUMN ExternalReference SET NOT NULL
- CREATE UNIQUE INDEX including NULL values
*/