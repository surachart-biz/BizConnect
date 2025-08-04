-- Migration: Create OtacCode table for One-Time Access Code functionality
-- Created: 2025-08-03
-- Description: Adds OTAC (One-Time Access Code) table with security controls and audit tracking

-- Create OtacCode table
CREATE TABLE IF NOT EXISTS "OtacCode" (
    "Id" SERIAL PRIMARY KEY,
    "Code" VARCHAR(8) NOT NULL,
    "Purpose" VARCHAR(100) NOT NULL,
    "IssuedTo" VARCHAR(256) NOT NULL,
    "AttemptCount" INTEGER NOT NULL DEFAULT 0,
    "IsLocked" BOOLEAN NOT NULL DEFAULT FALSE,
    "IsUsed" BOOLEAN NOT NULL DEFAULT FALSE,
    "CreatedAt" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    "ExpiresAt" TIMESTAMP WITH TIME ZONE NOT NULL,
    "UsedAt" TIMESTAMP WITH TIME ZONE NULL,
    "ValidatedFromIp" VARCHAR(45) NULL,
    "GeneratedByUserId" INTEGER NULL
);

-- Add indexes for performance
CREATE INDEX IF NOT EXISTS "IX_OtacCode_Code" ON "OtacCode" ("Code");
CREATE INDEX IF NOT EXISTS "IX_OtacCode_Purpose" ON "OtacCode" ("Purpose");
CREATE INDEX IF NOT EXISTS "IX_OtacCode_ExpiresAt" ON "OtacCode" ("ExpiresAt");
CREATE INDEX IF NOT EXISTS "IX_OtacCode_IssuedTo" ON "OtacCode" ("IssuedTo");
CREATE INDEX IF NOT EXISTS "IX_OtacCode_GeneratedByUserId" ON "OtacCode" ("GeneratedByUserId");

-- Add foreign key constraint to Users table
ALTER TABLE "OtacCode" 
ADD CONSTRAINT "FK_OtacCode_User" 
FOREIGN KEY ("GeneratedByUserId") 
REFERENCES "Users" ("Id") 
ON DELETE SET NULL;

-- Add comments for documentation
COMMENT ON TABLE "OtacCode" IS 'One-Time Access Codes for secure access to protected resources';
COMMENT ON COLUMN "OtacCode"."Id" IS 'Primary key, auto-incrementing identifier';
COMMENT ON COLUMN "OtacCode"."Code" IS '8-character alphanumeric code (uppercase letters and numbers)';
COMMENT ON COLUMN "OtacCode"."Purpose" IS 'Purpose or resource this code grants access to';
COMMENT ON COLUMN "OtacCode"."IssuedTo" IS 'Email address or identifier the code was issued to';
COMMENT ON COLUMN "OtacCode"."AttemptCount" IS 'Number of validation attempts made (max 5)';
COMMENT ON COLUMN "OtacCode"."IsLocked" IS 'Whether the code is locked due to excessive attempts';
COMMENT ON COLUMN "OtacCode"."IsUsed" IS 'Whether the code has been successfully used';
COMMENT ON COLUMN "OtacCode"."CreatedAt" IS 'Timestamp when the code was created';
COMMENT ON COLUMN "OtacCode"."ExpiresAt" IS 'Timestamp when the code expires (10 minutes after creation)';
COMMENT ON COLUMN "OtacCode"."UsedAt" IS 'Timestamp when the code was used (if applicable)';
COMMENT ON COLUMN "OtacCode"."ValidatedFromIp" IS 'IP address from which the code was successfully validated';
COMMENT ON COLUMN "OtacCode"."GeneratedByUserId" IS 'User ID of the admin/employee who generated this code';

-- Update schema version tracking
INSERT INTO "_SchemaVersion" ("Filename", "AppliedAt") 
VALUES ('20250803_CreateOtacCodeTable.sql', NOW())
ON CONFLICT ("Filename") DO NOTHING;