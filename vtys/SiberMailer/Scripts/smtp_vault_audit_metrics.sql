-- =====================================================
-- SMTP Vault Enhancement: Audit Columns & Metrics
-- SiberMailer 2.0
-- Run this script on your PostgreSQL database
-- =====================================================

-- 1. Add Audit Columns to SmtpAccounts table
-- =====================================================
ALTER TABLE SmtpAccounts 
ADD COLUMN IF NOT EXISTS CreatedByUserId INT REFERENCES Users(UserId),
ADD COLUMN IF NOT EXISTS UpdatedByUserId INT REFERENCES Users(UserId);

-- Ensure UpdatedAt column exists (should already exist)
ALTER TABLE SmtpAccounts 
ALTER COLUMN UpdatedAt SET DEFAULT CURRENT_TIMESTAMP;

-- 2. Create indexes for audit columns
-- =====================================================
CREATE INDEX IF NOT EXISTS idx_smtpaccounts_createdby ON SmtpAccounts(CreatedByUserId);
CREATE INDEX IF NOT EXISTS idx_smtpaccounts_updatedby ON SmtpAccounts(UpdatedByUserId);

-- 3. Create View for SMTP Account Statistics
-- =====================================================
-- This view joins SmtpAccounts with MailJobs/MailJobLogs for metrics
-- and Users table for audit trail names

DROP VIEW IF EXISTS vw_SmtpAccountStats;

CREATE OR REPLACE VIEW vw_SmtpAccountStats AS
SELECT 
    sa.SmtpAccountId,
    sa.AccountName,
    sa.SmtpHost,
    sa.SmtpPort,
    sa.UseSsl,
    sa.DailyLimit,
    sa.SentToday,
    sa.LastResetDate,
    sa.OwnerUserId,
    sa.IsShared,
    sa.IsActive,
    sa.CreatedAt,
    sa.UpdatedAt,
    sa.CreatedByUserId,
    sa.UpdatedByUserId,
    
    -- Audit: Created By User Name
    COALESCE(created_user.FullName, 'System') AS CreatedByName,
    
    -- Audit: Updated By User Name
    COALESCE(updated_user.FullName, 'System') AS UpdatedByName,
    
    -- Metrics: Total Sent Count (all time)
    COALESCE(stats.TotalSentCount, 0) AS TotalSentCount,
    
    -- Metrics: Today's Sent Count
    COALESCE(stats.TodaySentCount, 0) AS TodaySentCount,
    
    -- Metrics: Last Send Date
    stats.LastSentAt
    
FROM SmtpAccounts sa

-- Join for CreatedBy user name
LEFT JOIN Users created_user ON sa.CreatedByUserId = created_user.UserId

-- Join for UpdatedBy user name  
LEFT JOIN Users updated_user ON sa.UpdatedByUserId = updated_user.UserId

-- Subquery for mail statistics
LEFT JOIN (
    SELECT 
        mj.SmtpAccountId,
        COUNT(mjl.LogId) AS TotalSentCount,
        COUNT(CASE WHEN DATE(mjl.LoggedAt) = CURRENT_DATE THEN 1 END) AS TodaySentCount,
        MAX(mjl.LoggedAt) AS LastSentAt
    FROM MailJobs mj
    INNER JOIN MailJobLogs mjl ON mj.JobId = mjl.JobId
    WHERE mjl.Status = 'SENT'
    GROUP BY mj.SmtpAccountId
) stats ON sa.SmtpAccountId = stats.SmtpAccountId;

-- 4. Grant permissions (if needed)
-- =====================================================
-- GRANT SELECT ON vw_SmtpAccountStats TO sibermailer_app;

-- 5. Add comment for documentation
-- =====================================================
COMMENT ON VIEW vw_SmtpAccountStats IS 
'SMTP Account view with audit trail (CreatedBy, UpdatedBy) and email statistics (TotalSent, SentToday)';

-- =====================================================
-- END OF SCRIPT
-- =====================================================
