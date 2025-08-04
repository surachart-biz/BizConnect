-- BizConnect Branch Management Table
-- Migration: 20250803-02_CreateBranchTable.sql
-- Description: Creates the Branch table for managing bank branch information

-- Create Branch table with PascalCase column names to match EF Core conventions
CREATE TABLE IF NOT EXISTS "Branch" (
    "BranchId" SERIAL PRIMARY KEY,
    "Name" VARCHAR(100) NOT NULL,
    "Code" VARCHAR(10),
    "Address" TEXT,
    "IsActive" BOOLEAN NOT NULL DEFAULT TRUE,
    "CreatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "UpdatedAt" TIMESTAMPTZ
);

-- Create unique index on branch code for faster lookups
CREATE UNIQUE INDEX IF NOT EXISTS "IX_Branch_Code" ON "Branch" ("Code");

-- Create index on Name for searching branches by name
CREATE INDEX IF NOT EXISTS "IX_Branch_Name" ON "Branch" ("Name");

-- Create index on IsActive for filtering active branches
CREATE INDEX IF NOT EXISTS "IX_Branch_IsActive" ON "Branch" ("IsActive");

-- Create trigger to automatically update UpdatedAt on Branch table
DROP TRIGGER IF EXISTS update_branch_updated_at ON "Branch";
CREATE TRIGGER update_branch_updated_at
    BEFORE UPDATE ON "Branch"
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

-- Add foreign key constraint from KbankOddRegistration to Branch (only if it doesn't exist)
-- Note: We use ON DELETE SET NULL to allow branch deletion without affecting existing registrations
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.constraint_column_usage 
                   WHERE constraint_name = 'FK_KbankOddRegistration_Branch') THEN
        ALTER TABLE "KbankOddRegistration"
            ADD CONSTRAINT "FK_KbankOddRegistration_Branch"
            FOREIGN KEY ("BranchId") 
            REFERENCES "Branch" ("BranchId") 
            ON DELETE SET NULL;
    END IF;
END $$;

-- Insert some sample branch data
INSERT INTO "Branch" ("Name", "Code", "Address", "IsActive", "CreatedAt") VALUES
('Bangkok Main Branch', 'BMB001', '123 Silom Road, Bang Rak, Bangkok 10500', TRUE, NOW()),
('Sukhumvit Branch', 'SUK002', '456 Sukhumvit Road, Watthana, Bangkok 10110', TRUE, NOW()),
('Chatuchak Branch', 'CHA003', '789 Phahonyothin Road, Chatuchak, Bangkok 10900', TRUE, NOW()),
('Phuket Branch', 'PHU004', '321 Thalang Road, Mueang Phuket, Phuket 83000', TRUE, NOW()),
('Chiang Mai Branch', 'CHM005', '654 Chang Khlan Road, Mueang Chiang Mai, Chiang Mai 50100', TRUE, NOW())
ON CONFLICT ("Code") DO NOTHING;

-- Add comments for documentation
COMMENT ON TABLE "Branch" IS 'Bank branch information for ODD registration management';
COMMENT ON COLUMN "Branch"."BranchId" IS 'Primary key, auto-incrementing branch identifier';
COMMENT ON COLUMN "Branch"."Name" IS 'Human-readable branch name';
COMMENT ON COLUMN "Branch"."Code" IS 'Unique branch code for identification';
COMMENT ON COLUMN "Branch"."Address" IS 'Physical address of the branch';
COMMENT ON COLUMN "Branch"."IsActive" IS 'Whether the branch is currently active and accepting registrations';
COMMENT ON COLUMN "Branch"."CreatedAt" IS 'Timestamp when branch was created';
COMMENT ON COLUMN "Branch"."UpdatedAt" IS 'Timestamp when branch was last updated (auto-updated by trigger)';

-- Record this migration in schema version tracking
INSERT INTO "_SchemaVersion" ("Filename")
VALUES ('20250803-02_CreateBranchTable.sql')
ON CONFLICT ("Filename") DO NOTHING;