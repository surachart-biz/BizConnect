-- BizConnect KBank ODD Registration Additional Required Columns
-- Migration: 20250803-01_AddKbankRequiredColumns.sql
-- Description: Adds AccountNo, BranchId, and CodeExpiresAt columns to KbankOddRegistration table

-- Add required columns to KbankOddRegistration table (only if they don't already exist)
DO $$
BEGIN
    -- Add AccountNo column if it doesn't exist
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns 
                   WHERE table_name = 'KbankOddRegistration' AND column_name = 'AccountNo') THEN
        ALTER TABLE "KbankOddRegistration" ADD COLUMN "AccountNo" VARCHAR(20);
    END IF;
    
    -- Add BranchId column if it doesn't exist
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns 
                   WHERE table_name = 'KbankOddRegistration' AND column_name = 'BranchId') THEN
        ALTER TABLE "KbankOddRegistration" ADD COLUMN "BranchId" INTEGER;
    END IF;
    
    -- Add CodeExpiresAt column if it doesn't exist
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns 
                   WHERE table_name = 'KbankOddRegistration' AND column_name = 'CodeExpiresAt') THEN
        ALTER TABLE "KbankOddRegistration" ADD COLUMN "CodeExpiresAt" TIMESTAMPTZ;
    END IF;
END $$;

-- Create index on BranchId for efficient joins with Branch table
CREATE INDEX IF NOT EXISTS "IX_KbankOddRegistration_BranchId" 
ON "KbankOddRegistration" ("BranchId");

-- Create index on CodeExpiresAt for efficient purging of expired codes
CREATE INDEX IF NOT EXISTS "IX_KbankOddRegistration_CodeExpiresAt" 
ON "KbankOddRegistration" ("CodeExpiresAt");

-- Add comments for documentation
COMMENT ON COLUMN "KbankOddRegistration"."AccountNo" IS 'Bank account number for the ODD registration';
COMMENT ON COLUMN "KbankOddRegistration"."BranchId" IS 'Foreign key reference to Branch table';
COMMENT ON COLUMN "KbankOddRegistration"."CodeExpiresAt" IS 'Timestamp when the registration code expires for purging purposes';

-- Record this migration in schema version tracking
INSERT INTO "_SchemaVersion" ("Filename")
VALUES ('20250803-01_AddKbankRequiredColumns.sql')
ON CONFLICT ("Filename") DO NOTHING;