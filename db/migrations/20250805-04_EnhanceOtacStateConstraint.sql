-- Migration: Enhance OTAC State Constraint for Complete Lifecycle Management
-- Date: 2025-08-05
-- Purpose: Fix constraint violation by supporting full 6-state OTAC lifecycle
-- Critical: Used state is PERMANENT - these records must NEVER be purged for daily payments

-- ==================================================================
-- CRITICAL BUSINESS RULE IMPLEMENTATION
-- ==================================================================
-- Used state records are PERMANENT and required for daily SSO payment processing with KBank
-- They must NEVER be purged or modified once in Used state

BEGIN;

-- 1. Drop the existing restrictive constraint that's causing failures
ALTER TABLE "KbankOddRegistration" 
DROP CONSTRAINT IF EXISTS "CK_KbankOddRegistration_OtacState";

-- 2. Add enhanced constraint supporting complete 6-state lifecycle
ALTER TABLE "KbankOddRegistration" 
ADD CONSTRAINT "CK_KbankOddRegistration_OtacState" 
CHECK ("OtacState" IN ('Generated', 'Validated', 'Used', 'Expired', 'Invalidated', 'Purged'));

-- 3. Add database-level documentation about Used state permanence
COMMENT ON COLUMN "KbankOddRegistration"."OtacState" IS 
'OTAC lifecycle state. CRITICAL: Used state is PERMANENT - required for daily payment processing. Valid states: Generated, Validated, Used, Expired, Invalidated, Purged. State transitions: Generated→Validated→Used (permanent), Generated/Validated→Expired→Purged (cleanup), Generated→Invalidated→Purged (failed validation)';

-- 4. Update any existing records with invalid states to valid states
-- This handles edge cases where records might have been in an inconsistent state
UPDATE "KbankOddRegistration" 
SET "OtacState" = 'Generated', "UpdatedAt" = NOW()
WHERE "OtacState" NOT IN ('Generated', 'Validated', 'Used', 'Expired', 'Invalidated', 'Purged');

-- 5. Add index for efficient state-based queries (performance optimization)
CREATE INDEX IF NOT EXISTS "IX_KbankOddRegistration_OtacState_ExpiresAt" 
ON "KbankOddRegistration" ("OtacState", "OtacExpiresAt")
WHERE "OtacState" IN ('Generated', 'Validated');

-- 6. Add index for Used records (permanent records for payment processing)
CREATE INDEX IF NOT EXISTS "IX_KbankOddRegistration_UsedRecords" 
ON "KbankOddRegistration" ("OtacState", "CreatedAt", "AccountNo")
WHERE "OtacState" = 'Used';

-- 7. Create state transition audit function (future enhancement support)
CREATE OR REPLACE FUNCTION log_otac_state_transition()
RETURNS TRIGGER AS $$
BEGIN
    -- Only log if OtacState actually changed
    IF OLD."OtacState" IS DISTINCT FROM NEW."OtacState" THEN
        -- Log critical transition warning for Used state
        IF NEW."OtacState" = 'Used' THEN
            RAISE NOTICE 'CRITICAL: OTAC ID % transitioned to PERMANENT Used state. Record must never be purged.', NEW."Id";
        END IF;
        
        -- Log attempt to modify Used state (should be prevented by application logic)
        IF OLD."OtacState" = 'Used' AND NEW."OtacState" != 'Used' THEN
            RAISE WARNING 'VIOLATION: Attempt to modify PERMANENT Used state for OTAC ID %. State change blocked.', NEW."Id";
            -- Revert the state change
            NEW."OtacState" = OLD."OtacState";
        END IF;
    END IF;
    
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- 8. Create trigger to enforce Used state permanence at database level
DROP TRIGGER IF EXISTS trg_otac_state_protection ON "KbankOddRegistration";
CREATE TRIGGER trg_otac_state_protection
    BEFORE UPDATE ON "KbankOddRegistration"
    FOR EACH ROW
    EXECUTE FUNCTION log_otac_state_transition();

-- 9. Add monitoring view for OTAC lifecycle analytics
CREATE OR REPLACE VIEW "vw_OtacLifecycleStats" AS
SELECT 
    "OtacState",
    COUNT(*) as "RecordCount",
    MIN("CreatedAt") as "OldestRecord",
    MAX("CreatedAt") as "NewestRecord",
    CASE 
        WHEN "OtacState" = 'Used' THEN 'PERMANENT - Never Purge'
        WHEN "OtacState" IN ('Generated', 'Validated') THEN 'Active - Monitor Expiration'
        WHEN "OtacState" = 'Expired' THEN 'Eligible for Purge'
        WHEN "OtacState" = 'Invalidated' THEN 'Eligible for Purge'
        WHEN "OtacState" = 'Purged' THEN 'Archived'
        ELSE 'Unknown State'
    END as "StateDescription"
FROM "KbankOddRegistration"
GROUP BY "OtacState"
ORDER BY 
    CASE "OtacState"
        WHEN 'Generated' THEN 1
        WHEN 'Validated' THEN 2  
        WHEN 'Used' THEN 3
        WHEN 'Expired' THEN 4
        WHEN 'Invalidated' THEN 5
        WHEN 'Purged' THEN 6
        ELSE 99
    END;

-- 10. Add function to safely check purgeable records (excludes Used state)
CREATE OR REPLACE FUNCTION get_purgeable_otac_count()
RETURNS INTEGER AS $$
BEGIN
    RETURN (
        SELECT COUNT(*)
        FROM "KbankOddRegistration"
        WHERE "OtacState" IN ('Expired', 'Invalidated')
        AND "OtacState" != 'Used'  -- Extra safety check
    );
END;
$$ LANGUAGE plpgsql;

-- 11. Create comprehensive state validation check
CREATE OR REPLACE FUNCTION validate_otac_state_integrity()
RETURNS TABLE(
    check_name TEXT,
    status TEXT,
    record_count INTEGER,
    details TEXT
) AS $$
BEGIN
    -- Check 1: Verify no invalid states exist
    RETURN QUERY
    SELECT 
        'Invalid States'::TEXT,
        CASE WHEN COUNT(*) = 0 THEN 'PASS' ELSE 'FAIL' END::TEXT,
        COUNT(*)::INTEGER,
        'Records with invalid OtacState values'::TEXT
    FROM "KbankOddRegistration"
    WHERE "OtacState" NOT IN ('Generated', 'Validated', 'Used', 'Expired', 'Invalidated', 'Purged');
    
    -- Check 2: Count Used records (these are permanent)
    RETURN QUERY
    SELECT 
        'Used Records (Permanent)'::TEXT,
        'INFO'::TEXT,
        COUNT(*)::INTEGER,
        'These records must NEVER be purged - required for daily payments'::TEXT
    FROM "KbankOddRegistration"
    WHERE "OtacState" = 'Used';
    
    -- Check 3: Count purgeable records
    RETURN QUERY
    SELECT 
        'Purgeable Records'::TEXT,
        'INFO'::TEXT,
        COUNT(*)::INTEGER,
        'Records in Expired/Invalidated state eligible for purging'::TEXT
    FROM "KbankOddRegistration"
    WHERE "OtacState" IN ('Expired', 'Invalidated');
    
    -- Check 4: Active records needing monitoring
    RETURN QUERY
    SELECT 
        'Active Records'::TEXT,
        'INFO'::TEXT,
        COUNT(*)::INTEGER,
        'Records in Generated/Validated state - monitor for expiration'::TEXT
    FROM "KbankOddRegistration"
    WHERE "OtacState" IN ('Generated', 'Validated');
    
END;
$$ LANGUAGE plpgsql;

COMMIT;

-- ==================================================================
-- POST-MIGRATION VERIFICATION
-- ==================================================================
-- Run these queries to verify the migration was successful:

-- 1. Check constraint exists and allows all 6 states
-- SELECT conname, consrc FROM pg_constraint WHERE conname = 'CK_KbankOddRegistration_OtacState';

-- 2. View lifecycle statistics
-- SELECT * FROM "vw_OtacLifecycleStats";

-- 3. Run integrity validation
-- SELECT * FROM validate_otac_state_integrity();

-- 4. Test constraint (should succeed)
-- INSERT INTO "KbankOddRegistration" (... "OtacState") VALUES (... 'Expired');

-- ==================================================================
-- ROLLBACK SCRIPT (if needed)
-- ==================================================================
-- To rollback this migration (CAUTION: will cause job failures):
-- 
-- ALTER TABLE "KbankOddRegistration" 
-- DROP CONSTRAINT "CK_KbankOddRegistration_OtacState";
-- 
-- ALTER TABLE "KbankOddRegistration" 
-- ADD CONSTRAINT "CK_KbankOddRegistration_OtacState" 
-- CHECK ("OtacState" IN ('Generated', 'Validated', 'Used'));