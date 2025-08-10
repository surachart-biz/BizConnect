-- ====================================================================
-- Migration: 20250805-05_AddMultiLanguageStatusColumns.sql
-- Purpose: Add missing multi-language columns and fix field sizes in KbankOddRegistration
-- Author: Claude Code (BizConnect Database Architect)
-- Date: 2025-08-05
-- CRITICAL: This fixes Thai/English UI toggle functionality
-- ====================================================================

BEGIN;

-- ====================================================================
-- SECTION 1: ADD MULTI-LANGUAGE STATUS AND ERROR MESSAGE COLUMNS
-- ====================================================================

-- Add multi-language status message columns
ALTER TABLE "KbankOddRegistration" 
ADD COLUMN "StatusMessageTh" TEXT,
ADD COLUMN "StatusMessageEn" TEXT,
ADD COLUMN "ErrorMessageTh" TEXT,
ADD COLUMN "ErrorMessageEn" TEXT;

-- Add column comments for multi-language fields
COMMENT ON COLUMN "KbankOddRegistration"."StatusMessageTh" IS 'Status message in Thai language for UI display';
COMMENT ON COLUMN "KbankOddRegistration"."StatusMessageEn" IS 'Status message in English language for UI display';
COMMENT ON COLUMN "KbankOddRegistration"."ErrorMessageTh" IS 'Error message in Thai language for UI display';
COMMENT ON COLUMN "KbankOddRegistration"."ErrorMessageEn" IS 'Error message in English language for UI display';

-- ====================================================================
-- SECTION 2: DROP DEPENDENT VIEWS BEFORE COLUMN MODIFICATIONS
-- ====================================================================

-- Drop dependent views temporarily to allow column type changes
DROP VIEW IF EXISTS "ActiveOddRegistrations";
DROP VIEW IF EXISTS "ExpiredOtacCodes";

-- ====================================================================
-- SECTION 3: FIX FIELD SIZE ISSUES FOR KBANK INTEGRATION
-- ====================================================================

-- Increase ExternalReference field size (currently VARCHAR(40) -> VARCHAR(50))
ALTER TABLE "KbankOddRegistration" 
ALTER COLUMN "ExternalReference" TYPE VARCHAR(50);

-- Fix field name inconsistency: IdValue should be larger for international formats
-- Current: IdValue VARCHAR(30) -> Increase to VARCHAR(20) for National IDs but we need proper field
-- Note: Current schema has IdValue which should be mapped to NationalId
-- We need to add proper NationalId column and rename mobile field

-- Add NationalId column if not exists (for clarity and international formats)
DO $$
BEGIN
    -- Check if NationalId column exists, if not add it
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'KbankOddRegistration' 
        AND column_name = 'NationalId'
    ) THEN
        ALTER TABLE "KbankOddRegistration" ADD COLUMN "NationalId" VARCHAR(20);
        COMMENT ON COLUMN "KbankOddRegistration"."NationalId" IS 'National identification number (separate from IdValue for clarity)';
    END IF;
END $$;

-- Increase MobileNo field size for international formats (currently VARCHAR(20) is OK, but let's ensure it's VARCHAR(20))
-- Also add MobileNumber column for consistency if needed
DO $$
BEGIN
    -- Check if MobileNumber column exists, if not add it
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'KbankOddRegistration' 
        AND column_name = 'MobileNumber'
    ) THEN
        ALTER TABLE "KbankOddRegistration" ADD COLUMN "MobileNumber" VARCHAR(20);
        COMMENT ON COLUMN "KbankOddRegistration"."MobileNumber" IS 'Mobile phone number in international format (alternative to MobileNo)';
    END IF;
END $$;

-- ====================================================================
-- SECTION 4: FIX STATUS COLUMN DEFAULT AND NULL CONSTRAINTS
-- ====================================================================

-- Change Status default from 'Pending' to NULL (should be set by business logic)
ALTER TABLE "KbankOddRegistration" 
ALTER COLUMN "Status" DROP DEFAULT;

-- Allow NULL values for Status until KBank processes the registration
ALTER TABLE "KbankOddRegistration" 
ALTER COLUMN "Status" DROP NOT NULL;

-- Update column comment to reflect new nullable status
COMMENT ON COLUMN "KbankOddRegistration"."Status" IS 'Registration status: NULL (unprocessed), Pending, Success, or Fail - set by KBank integration';

-- ====================================================================
-- SECTION 5: CREATE INDEXES FOR NEW COLUMNS
-- ====================================================================

-- Create indexes for multi-language message columns (for searching/filtering)
CREATE INDEX "IX_KbankOddRegistration_StatusMessageTh" 
    ON "KbankOddRegistration" ("StatusMessageTh")
    WHERE "StatusMessageTh" IS NOT NULL;

CREATE INDEX "IX_KbankOddRegistration_StatusMessageEn" 
    ON "KbankOddRegistration" ("StatusMessageEn")
    WHERE "StatusMessageEn" IS NOT NULL;

-- Create indexes for new identification fields
CREATE INDEX "IX_KbankOddRegistration_NationalId" 
    ON "KbankOddRegistration" ("NationalId")
    WHERE "NationalId" IS NOT NULL;

CREATE INDEX "IX_KbankOddRegistration_MobileNumber" 
    ON "KbankOddRegistration" ("MobileNumber")
    WHERE "MobileNumber" IS NOT NULL;

-- ====================================================================
-- SECTION 6: CREATE HELPER FUNCTIONS FOR MULTI-LANGUAGE STATUS MESSAGES
-- ====================================================================

-- Create function to set localized status messages
CREATE OR REPLACE FUNCTION set_status_messages(
    registration_id INTEGER,
    status_code VARCHAR(20),
    error_code VARCHAR(10) DEFAULT NULL
) RETURNS VOID AS $$
BEGIN
    UPDATE "KbankOddRegistration" 
    SET 
        "StatusMessageTh" = CASE status_code
            WHEN 'Pending' THEN 'รอดำเนินการ'
            WHEN 'Success' THEN 'สำเร็จ'
            WHEN 'Fail' THEN 'ล้มเหลว'
            WHEN 'Cancelled' THEN 'ยกเลิก'
            WHEN 'Expired' THEN 'หมดอายุ'
            ELSE status_code
        END,
        "StatusMessageEn" = CASE status_code
            WHEN 'Pending' THEN 'Pending'
            WHEN 'Success' THEN 'Success'
            WHEN 'Fail' THEN 'Failed'
            WHEN 'Cancelled' THEN 'Cancelled'
            WHEN 'Expired' THEN 'Expired'
            ELSE status_code
        END,
        "ErrorMessageTh" = CASE error_code
            WHEN '001' THEN 'ข้อมูลไม่ถูกต้อง'
            WHEN '002' THEN 'บัญชีไม่พบ'
            WHEN '003' THEN 'บัญชีถูกปิด'
            WHEN '004' THEN 'เอกสารไม่ถูกต้อง'
            WHEN '005' THEN 'มีการลงทะเบียนแล้ว'
            ELSE CASE WHEN error_code IS NOT NULL THEN 'เกิดข้อผิดพลาด: ' || error_code ELSE NULL END
        END,
        "ErrorMessageEn" = CASE error_code
            WHEN '001' THEN 'Invalid data'
            WHEN '002' THEN 'Account not found'
            WHEN '003' THEN 'Account closed'
            WHEN '004' THEN 'Invalid document'
            WHEN '005' THEN 'Already registered'
            ELSE CASE WHEN error_code IS NOT NULL THEN 'Error: ' || error_code ELSE NULL END
        END
    WHERE "Id" = registration_id;
END;
$$ LANGUAGE plpgsql;

-- Create function to get localized status message
CREATE OR REPLACE FUNCTION get_registration_status_message(
    registration_id INTEGER, 
    language_code VARCHAR(2) DEFAULT 'en'
) RETURNS TEXT AS $$
DECLARE
    result TEXT;
BEGIN
    SELECT 
        CASE 
            WHEN language_code = 'th' AND "StatusMessageTh" IS NOT NULL THEN "StatusMessageTh"
            WHEN language_code = 'en' AND "StatusMessageEn" IS NOT NULL THEN "StatusMessageEn"
            ELSE COALESCE("StatusMessageEn", "StatusMessageTh", "Status", 'Unknown')
        END
    INTO result
    FROM "KbankOddRegistration"
    WHERE "Id" = registration_id;
    
    RETURN COALESCE(result, 'Record not found');
END;
$$ LANGUAGE plpgsql;

-- Create function to get localized error message
CREATE OR REPLACE FUNCTION get_registration_error_message(
    registration_id INTEGER, 
    language_code VARCHAR(2) DEFAULT 'en'
) RETURNS TEXT AS $$
DECLARE
    result TEXT;
BEGIN
    SELECT 
        CASE 
            WHEN language_code = 'th' AND "ErrorMessageTh" IS NOT NULL THEN "ErrorMessageTh"
            WHEN language_code = 'en' AND "ErrorMessageEn" IS NOT NULL THEN "ErrorMessageEn"
            ELSE NULL
        END
    INTO result
    FROM "KbankOddRegistration"
    WHERE "Id" = registration_id;
    
    RETURN result;
END;
$$ LANGUAGE plpgsql;

-- Add comments for new functions
COMMENT ON FUNCTION set_status_messages(INTEGER, VARCHAR, VARCHAR) IS 'Sets localized status and error messages for a registration record';
COMMENT ON FUNCTION get_registration_status_message(INTEGER, VARCHAR) IS 'Returns localized status message for UI display';
COMMENT ON FUNCTION get_registration_error_message(INTEGER, VARCHAR) IS 'Returns localized error message for UI display';

-- ====================================================================
-- SECTION 7: RECREATE VIEWS WITH MULTI-LANGUAGE COLUMNS
-- ====================================================================

-- Update ActiveOddRegistrations view to include multi-language status messages
DROP VIEW IF EXISTS "ActiveOddRegistrations";
CREATE OR REPLACE VIEW "ActiveOddRegistrations" AS
SELECT 
    ko."Id",
    ko."ExternalReference",
    ko."RegId",
    ko."EspaId",
    ko."Status",
    ko."StatusMessageTh",
    ko."StatusMessageEn",
    ko."ErrorMessageTh",
    ko."ErrorMessageEn",
    ko."IdType",
    ko."IdValue",
    ko."NationalId",
    ko."FullName",
    ko."MobileNo",
    ko."MobileNumber",
    ko."AccountNo",
    ko."OtacCode",
    ko."OtacState",
    ko."AttemptCount",
    ko."IsLocked",
    ko."CreatedAt",
    ko."UpdatedAt",
    ko."BranchId",
    b."Code" AS "BranchCode",
    b."Name" AS "BranchName",
    b."NameTh" AS "BranchNameTh",
    b."NameEn" AS "BranchNameEn",
    u."Username" AS "GeneratedByUsername"
FROM "KbankOddRegistration" ko
LEFT JOIN "Branch" b ON ko."BranchId" = b."BranchId"
LEFT JOIN "Users" u ON ko."GeneratedByUserId" = u."Id"
WHERE ko."Status" IS NULL OR ko."Status" NOT IN ('Success', 'Fail')
  AND (ko."OtacExpiresAt" IS NULL OR ko."OtacExpiresAt" > NOW());

-- Update ExpiredOtacCodes view to include multi-language messages
DROP VIEW IF EXISTS "ExpiredOtacCodes";
CREATE OR REPLACE VIEW "ExpiredOtacCodes" AS
SELECT 
    ko."Id",
    ko."ExternalReference",
    ko."OtacCode",
    ko."OtacState",
    ko."Status",
    ko."StatusMessageTh",
    ko."StatusMessageEn",
    ko."ErrorMessageTh",
    ko."ErrorMessageEn",
    ko."OtacExpiresAt",
    ko."CreatedAt",
    ko."UpdatedAt",
    ko."BranchId",
    -- Multi-language branch information
    b."Code" AS "BranchCode",
    b."Name" AS "BranchName",
    b."NameTh" AS "BranchNameTh",
    b."NameEn" AS "BranchNameEn",
    b."Address" AS "BranchAddress",
    b."AddressTh" AS "BranchAddressTh",
    b."AddressEn" AS "BranchAddressEn",
    -- User information
    u."Username" AS "GeneratedByUsername",
    -- Additional helpful fields
    EXTRACT(EPOCH FROM (NOW() - ko."OtacExpiresAt"))/60 AS "MinutesExpired",
    CASE 
        WHEN ko."OtacExpiresAt" < NOW() - INTERVAL '1 day' THEN 'STALE'
        WHEN ko."OtacExpiresAt" < NOW() - INTERVAL '1 hour' THEN 'OLD'
        ELSE 'RECENT'
    END AS "ExpiryCategory"
FROM "KbankOddRegistration" ko
LEFT JOIN "Branch" b ON ko."BranchId" = b."BranchId"
LEFT JOIN "Users" u ON ko."GeneratedByUserId" = u."Id"
WHERE ko."OtacExpiresAt" IS NOT NULL 
  AND ko."OtacExpiresAt" < NOW()
  AND (ko."Status" IS NULL OR ko."Status" NOT IN ('Success', 'Fail'));

-- Update view comments
COMMENT ON VIEW "ActiveOddRegistrations" IS 'Active ODD registrations with complete multi-language status/error messages and branch information';
COMMENT ON VIEW "ExpiredOtacCodes" IS 'Expired OTAC codes with multi-language messages and branch information for cleanup jobs';

-- ====================================================================
-- SECTION 8: DATA MIGRATION - SET STATUS MESSAGES FOR EXISTING RECORDS
-- ====================================================================

-- Update existing records to have proper status messages
UPDATE "KbankOddRegistration" 
SET 
    "StatusMessageTh" = CASE "Status"
        WHEN 'Pending' THEN 'รอดำเนินการ'
        WHEN 'Success' THEN 'สำเร็จ'
        WHEN 'Fail' THEN 'ล้มเหลว'
        WHEN 'Cancelled' THEN 'ยกเลิก'
        WHEN 'Expired' THEN 'หมดอายุ'
        ELSE "Status"
    END,
    "StatusMessageEn" = CASE "Status"
        WHEN 'Pending' THEN 'Pending'
        WHEN 'Success' THEN 'Success'
        WHEN 'Fail' THEN 'Failed'
        WHEN 'Cancelled' THEN 'Cancelled'
        WHEN 'Expired' THEN 'Expired'
        ELSE "Status"
    END
WHERE "Status" IS NOT NULL;

-- ====================================================================
-- SECTION 9: FINAL VALIDATION AND COMPLETION
-- ====================================================================

-- Validate that all new columns were added successfully
DO $$
DECLARE
    column_count INTEGER;
    function_count INTEGER;
    view_count INTEGER;
BEGIN
    -- Count new multi-language columns
    SELECT COUNT(*) INTO column_count
    FROM information_schema.columns
    WHERE table_name = 'KbankOddRegistration' 
    AND column_name IN ('StatusMessageTh', 'StatusMessageEn', 'ErrorMessageTh', 'ErrorMessageEn');
    
    -- Count new helper functions
    SELECT COUNT(*) INTO function_count
    FROM pg_proc p
    JOIN pg_namespace n ON p.pronamespace = n.oid
    WHERE n.nspname = 'public' 
    AND p.proname IN ('set_status_messages', 'get_registration_status_message', 'get_registration_error_message');
    
    -- Count updated views
    SELECT COUNT(*) INTO view_count
    FROM pg_views
    WHERE schemaname = 'public'
    AND viewname IN ('ActiveOddRegistrations', 'ExpiredOtacCodes');
    
    RAISE NOTICE 'Multi-language status columns validation:';
    RAISE NOTICE '- Multi-language columns added: %', column_count;
    RAISE NOTICE '- Helper functions created: %', function_count;
    RAISE NOTICE '- Views updated with multi-language support: %', view_count;
    
    IF column_count < 4 THEN
        RAISE EXCEPTION 'Multi-language columns addition failed - missing status/error message columns';
    END IF;
    
    IF function_count < 3 THEN
        RAISE EXCEPTION 'Multi-language functions creation failed - missing helper functions';
    END IF;
    
    IF view_count < 2 THEN
        RAISE EXCEPTION 'View updates failed - missing updated views';
    END IF;
    
    RAISE NOTICE 'Multi-language status columns migration completed successfully!';
    RAISE NOTICE 'Thai/English UI toggle functionality is now fully supported';
    RAISE NOTICE 'Status and error messages will display in proper language';
    RAISE NOTICE 'Field sizes have been adjusted for KBank integration requirements';
END $$;

COMMIT;

-- ====================================================================
-- POST-MIGRATION USAGE EXAMPLES
-- ====================================================================
/*
USAGE EXAMPLES FOR MULTI-LANGUAGE STATUS FUNCTIONALITY:

1. Set status messages when updating registration status:
   SELECT set_status_messages(123, 'Success', NULL);

2. Set status with error messages:
   SELECT set_status_messages(124, 'Fail', '002');

3. Get localized status message in Thai:
   SELECT get_registration_status_message(123, 'th');

4. Get localized error message in English:
   SELECT get_registration_error_message(124, 'en');

5. Query active registrations with multi-language status:
   SELECT "ExternalReference", "StatusMessageTh", "StatusMessageEn", 
          "ErrorMessageTh", "BranchNameTh"
   FROM "ActiveOddRegistrations"
   WHERE "Status" IS NULL OR "Status" = 'Pending';

6. View expired codes with localized messages:
   SELECT "ExternalReference", "StatusMessageTh", "ErrorMessageTh",
          "ExpiryCategory", "MinutesExpired"
   FROM "ExpiredOtacCodes"
   WHERE "ExpiryCategory" = 'RECENT';

INTEGRATION NOTES:
- In your KBank integration service, call set_status_messages() after receiving callback
- In Razor views, use get_registration_status_message() with current culture
- Views now include all multi-language columns for direct querying
- Status column can now be NULL for unprocessed registrations
- Field sizes have been increased to accommodate KBank requirements
*/