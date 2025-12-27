-- ============================================================
-- FIX: Template Column Name Mismatch
-- ============================================================
-- This fixes the error: null value in column "htmlbody" violates not-null constraint
-- 
-- The database has column "htmlbody" but the C# code expects "HtmlContent"
-- This script renames the column to match the code expectations.
-- ============================================================

-- Rename htmlbody to HtmlContent
ALTER TABLE Templates RENAME COLUMN htmlbody TO HtmlContent;

-- Verification: Check the column was renamed successfully
SELECT column_name, data_type, is_nullable
FROM information_schema.columns 
WHERE table_name = 'templates' 
  AND column_name = 'htmlcontent';

-- Done! The Templates table now uses HtmlContent instead of htmlbody.
