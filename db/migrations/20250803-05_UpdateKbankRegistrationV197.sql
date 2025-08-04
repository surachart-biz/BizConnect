-- BizConnect KBank ODD Registration V1.9.7 Schema Updates
-- Migration: 20250803-05_UpdateKbankRegistrationV197.sql
-- Description: Updates KbankOddRegistration table for V1.9.7 specification - removes email, adds ID type fields, ensures proper structure

-- Remove Email column and update schema for V1.9.7 compliance
DO $$
BEGIN
    -- Remove Email column if it exists (V1.9.7 doesn't use email)
    IF EXISTS (SELECT 1 FROM information_schema.columns 
               WHERE table_name = 'KbankOddRegistration' AND column_name = 'Email') THEN
        ALTER TABLE "KbankOddRegistration" DROP COLUMN "Email";
    END IF;
    
    -- Add IdType column if it doesn't exist
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns 
                   WHERE table_name = 'KbankOddRegistration' AND column_name = 'IdType') THEN
        ALTER TABLE "KbankOddRegistration" ADD COLUMN "IdType" VARCHAR(20);
    END IF;
    
    -- Add IdValue column if it doesn't exist
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns 
                   WHERE table_name = 'KbankOddRegistration' AND column_name = 'IdValue') THEN
        ALTER TABLE "KbankOddRegistration" ADD COLUMN "IdValue" VARCHAR(30);
    END IF;
    
    -- Add FullName column if it doesn't exist
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns 
                   WHERE table_name = 'KbankOddRegistration' AND column_name = 'FullName') THEN
        ALTER TABLE "KbankOddRegistration" ADD COLUMN "FullName" VARCHAR(100);
    END IF;
    
    -- Ensure AccountNo column exists with proper length
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns 
                   WHERE table_name = 'KbankOddRegistration' AND column_name = 'AccountNo') THEN
        ALTER TABLE "KbankOddRegistration" ADD COLUMN "AccountNo" VARCHAR(20);
    END IF;
    
    -- Ensure BranchId column exists
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns 
                   WHERE table_name = 'KbankOddRegistration' AND column_name = 'BranchId') THEN
        ALTER TABLE "KbankOddRegistration" ADD COLUMN "BranchId" INTEGER;
    END IF;
    
    -- Ensure MobileNo column exists with proper length
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns 
                   WHERE table_name = 'KbankOddRegistration' AND column_name = 'MobileNo') THEN
        ALTER TABLE "KbankOddRegistration" ADD COLUMN "MobileNo" VARCHAR(20);
    END IF;
END $$;

-- Add foreign key constraint for BranchId if it doesn't already exist
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.table_constraints tc
        WHERE tc.constraint_type = 'FOREIGN KEY'
        AND tc.table_name = 'KbankOddRegistration'
        AND tc.constraint_name = 'FK_KbankOddRegistration_Branch_BranchId'
    ) THEN
        ALTER TABLE "KbankOddRegistration" 
        ADD CONSTRAINT "FK_KbankOddRegistration_Branch_BranchId" 
        FOREIGN KEY ("BranchId") REFERENCES "Branch"("BranchId");
    END IF;
END $$;

-- Create indexes for efficient querying
CREATE INDEX IF NOT EXISTS "IX_KbankOddRegistration_IdType_IdValue" 
ON "KbankOddRegistration" ("IdType", "IdValue");

CREATE INDEX IF NOT EXISTS "IX_KbankOddRegistration_Status_CreatedAt" 
ON "KbankOddRegistration" ("Status", "CreatedAt");

-- Add comments for documentation
COMMENT ON COLUMN "KbankOddRegistration"."IdType" IS 'Type of identification: National ID, Passport, Tax ID, or Company Tax ID';
COMMENT ON COLUMN "KbankOddRegistration"."IdValue" IS 'Identification document number/value corresponding to the selected ID type';
COMMENT ON COLUMN "KbankOddRegistration"."FullName" IS 'User full name for registration';
COMMENT ON COLUMN "KbankOddRegistration"."AccountNo" IS 'Bank account number for the ODD registration (10-15 digits)';
COMMENT ON COLUMN "KbankOddRegistration"."BranchId" IS 'Foreign key reference to Branch table';
COMMENT ON COLUMN "KbankOddRegistration"."MobileNo" IS 'User mobile number in format 08xxxxxxxx or +66xxxxxxxx';

-- Record this migration in schema version tracking
INSERT INTO "_SchemaVersion" ("Filename")
VALUES ('20250803-05_UpdateKbankRegistrationV197.sql')
ON CONFLICT ("Filename") DO NOTHING;