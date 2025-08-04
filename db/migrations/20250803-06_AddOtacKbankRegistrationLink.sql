-- BizConnect OTAC-KBank Registration Link
-- Migration: 20250803-06_AddOtacKbankRegistrationLink.sql
-- Description: Adds foreign key relationship between OtacCode and KbankOddRegistration tables
--              to support the complete business flow: OTAC generation → Registration → KBank callback

BEGIN;

-- Check if the column already exists to make this migration idempotent
DO $$
BEGIN
    -- Add KbankOddRegistrationId column to OtacCode table if it doesn't exist
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'OtacCode' 
        AND column_name = 'KbankOddRegistrationId'
        AND table_schema = 'public'
    ) THEN
        ALTER TABLE "OtacCode" 
        ADD COLUMN "KbankOddRegistrationId" INTEGER NULL;
        
        -- Add comment for the new column
        COMMENT ON COLUMN "OtacCode"."KbankOddRegistrationId" IS 'Optional foreign key to KbankOddRegistration for linking OTAC to ODD registration process';
    END IF;
END $$;

-- Check if the foreign key constraint already exists
DO $$
BEGIN
    -- Add foreign key constraint if it doesn't exist
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.table_constraints 
        WHERE constraint_name = 'FK_OtacCode_KbankOddRegistrationId'
        AND table_name = 'OtacCode'
        AND table_schema = 'public'
    ) THEN
        ALTER TABLE "OtacCode"
        ADD CONSTRAINT "FK_OtacCode_KbankOddRegistrationId" 
        FOREIGN KEY ("KbankOddRegistrationId") 
        REFERENCES "KbankOddRegistration" ("Id") 
        ON DELETE SET NULL ON UPDATE CASCADE;
    END IF;
END $$;

-- Check if the performance index already exists
DO $$
BEGIN
    -- Add performance index on KbankOddRegistrationId if it doesn't exist
    IF NOT EXISTS (
        SELECT 1 FROM pg_indexes 
        WHERE indexname = 'IX_OtacCode_KbankOddRegistrationId'
        AND tablename = 'OtacCode'
        AND schemaname = 'public'
    ) THEN
        CREATE INDEX "IX_OtacCode_KbankOddRegistrationId" 
        ON "OtacCode" ("KbankOddRegistrationId");
    END IF;
END $$;

-- Add composite index for common business flow queries (OTAC lookup with registration status)
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_indexes 
        WHERE indexname = 'IX_OtacCode_KbankRegistration_Purpose_IsUsed'
        AND tablename = 'OtacCode'
        AND schemaname = 'public'
    ) THEN
        CREATE INDEX "IX_OtacCode_KbankRegistration_Purpose_IsUsed" 
        ON "OtacCode" ("KbankOddRegistrationId", "Purpose", "IsUsed");
    END IF;
END $$;

COMMIT;

-- Record this migration in schema version tracking
INSERT INTO "_SchemaVersion" ("Filename")
VALUES ('20250803-06_AddOtacKbankRegistrationLink.sql')
ON CONFLICT ("Filename") DO NOTHING;

/*
ROLLBACK INSTRUCTIONS (for future reference):
To rollback this migration, execute:

BEGIN;
-- Remove foreign key constraint
ALTER TABLE "OtacCode" DROP CONSTRAINT IF EXISTS "FK_OtacCode_KbankOddRegistrationId";
-- Remove indexes
DROP INDEX IF EXISTS "IX_OtacCode_KbankOddRegistrationId";
DROP INDEX IF EXISTS "IX_OtacCode_KbankRegistration_Purpose_IsUsed";
-- Remove column
ALTER TABLE "OtacCode" DROP COLUMN IF EXISTS "KbankOddRegistrationId";
-- Remove migration record
DELETE FROM "_SchemaVersion" WHERE "Filename" = '20250803-06_AddOtacKbankRegistrationLink.sql';
COMMIT;

BUSINESS FLOW SUPPORTED:
1. Employee clicks "เพิ่มข้อมูล" → Create KbankOddRegistration (Status="CodeIssued") + Create linked OtacCode
2. Guest validates OTAC → Update same KbankOddRegistration (Status="Pending", fill form data)
3. KBank callback → Update KbankOddRegistration (Status="Success/Fail")

The nullable KbankOddRegistrationId allows OTAC codes to exist independently for other purposes
while supporting the ODD registration flow when linked.
*/