-- ============================================
-- SiberMailer 2.0 - Templates Table Update
-- Adds IsActive column for template status
-- ============================================

-- Add IsActive column to Templates table if it doesn't exist
ALTER TABLE Templates 
ADD COLUMN IF NOT EXISTS IsActive BOOLEAN DEFAULT TRUE;

-- Update any existing records to be active (if column was just added)
UPDATE Templates SET IsActive = TRUE WHERE IsActive IS NULL;

-- Create index for faster filtering by status
CREATE INDEX IF NOT EXISTS idx_templates_is_active ON Templates(IsActive);

-- Verify the column was added
SELECT column_name, data_type, column_default 
FROM information_schema.columns 
WHERE table_name = 'templates' AND column_name = 'isactive';
