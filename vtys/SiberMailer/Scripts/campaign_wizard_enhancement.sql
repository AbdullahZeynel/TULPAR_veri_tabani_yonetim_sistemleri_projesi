-- =====================================================
-- Campaign Wizard Enhancement: Email Subject, Admin Notes, Attachments
-- SiberMailer 2.0
-- Run this script on your PostgreSQL database
-- =====================================================

-- 1. Add EmailSubject column to MailJobs table
-- Allows user to override the default template subject
-- =====================================================
ALTER TABLE MailJobs 
ADD COLUMN IF NOT EXISTS EmailSubject VARCHAR(500);

COMMENT ON COLUMN MailJobs.EmailSubject IS 'Custom email subject line (overrides template default)';

-- 2. Add AdminNotes column to MailJobs table
-- Description/notes for the approver to review
-- =====================================================
ALTER TABLE MailJobs 
ADD COLUMN IF NOT EXISTS AdminNotes TEXT;

COMMENT ON COLUMN MailJobs.AdminNotes IS 'Notes/description for admin review during approval process';

-- 3. Add AttachmentPaths column to MailJobs table
-- JSONB array of file paths for email attachments
-- =====================================================
ALTER TABLE MailJobs 
ADD COLUMN IF NOT EXISTS AttachmentPaths JSONB DEFAULT '[]'::jsonb;

COMMENT ON COLUMN MailJobs.AttachmentPaths IS 'JSON array of attachment file paths (e.g. ["C:\\files\\doc.pdf"])';

-- 4. Add index for faster querying of jobs with attachments
-- =====================================================
CREATE INDEX IF NOT EXISTS idx_mailjobs_has_attachments 
ON MailJobs ((AttachmentPaths != '[]'::jsonb));

-- =====================================================
-- END OF SCRIPT
-- =====================================================
