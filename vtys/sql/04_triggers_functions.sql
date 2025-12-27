-- ============================================
-- SIBERMAILER 2.0 - TRIGGERS & FUNCTIONS
-- Phase 2, Task 2.4-2.5: Advanced Programmability
-- PostgreSQL 18+
-- ============================================

-- ============================================
-- 1. FN_GET_LIST_MEMBERS (Table-Valued Function)
-- Returns all active members of a recipient list
-- ============================================
CREATE OR REPLACE FUNCTION fn_get_list_members(
    p_list_id INT,
    p_status contact_status DEFAULT NULL
)
RETURNS TABLE (
    ContactId       INT,
    Email           VARCHAR(255),
    FullName        VARCHAR(100),
    Company         VARCHAR(100),
    CustomData      JSONB,
    Status          contact_status,
    BounceCount     INT
)
LANGUAGE plpgsql
STABLE
AS $$
BEGIN
    RETURN QUERY
    SELECT 
        c.ContactId,
        c.Email,
        c.FullName,
        c.Company,
        c.CustomData,
        c.Status,
        c.BounceCount
    FROM Contacts c
    WHERE c.ListId = p_list_id
      AND (p_status IS NULL OR c.Status = p_status)
    ORDER BY c.FullName, c.Email;
END;
$$;

COMMENT ON FUNCTION fn_get_list_members IS 'Returns contacts from a list, optionally filtered by status';

-- Usage example:
-- SELECT * FROM fn_get_list_members(1);                    -- All contacts
-- SELECT * FROM fn_get_list_members(1, 'Active');          -- Only active
-- SELECT * FROM fn_get_list_members(1, 'RedListed');       -- Only red-listed

-- ============================================
-- 2. FN_GET_BRANCH_STATS (Statistics Function)
-- Returns key statistics for a branch
-- ============================================
CREATE OR REPLACE FUNCTION fn_get_branch_stats(
    p_branch_id INT
)
RETURNS TABLE (
    BranchId            INT,
    BranchCode          VARCHAR(20),
    BranchName          VARCHAR(100),
    TotalLists          BIGINT,
    TotalContacts       BIGINT,
    ActiveContacts      BIGINT,
    RedListedContacts   BIGINT,
    TotalTemplates      BIGINT,
    TotalJobs           BIGINT,
    PendingJobs         BIGINT,
    CompletedJobs       BIGINT,
    TotalEmailsSent     BIGINT,
    TotalSmtpAccounts   BIGINT
)
LANGUAGE plpgsql
STABLE
AS $$
BEGIN
    RETURN QUERY
    SELECT 
        b.BranchId,
        b.BranchCode,
        b.BranchName,
        -- List stats
        (SELECT COUNT(*) FROM RecipientLists rl WHERE rl.BranchId = b.BranchId AND rl.IsActive = TRUE)::BIGINT,
        -- Contact stats
        (SELECT COALESCE(SUM(rl.TotalContacts), 0) FROM RecipientLists rl WHERE rl.BranchId = b.BranchId)::BIGINT,
        (SELECT COALESCE(SUM(rl.ActiveContacts), 0) FROM RecipientLists rl WHERE rl.BranchId = b.BranchId)::BIGINT,
        (SELECT COUNT(*) FROM Contacts c 
            JOIN RecipientLists rl ON c.ListId = rl.ListId 
            WHERE rl.BranchId = b.BranchId AND c.Status = 'RedListed')::BIGINT,
        -- Template stats
        (SELECT COUNT(*) FROM Templates t WHERE t.BranchId = b.BranchId OR t.IsGlobal = TRUE)::BIGINT,
        -- Job stats
        (SELECT COUNT(*) FROM MailJobs mj WHERE mj.BranchId = b.BranchId)::BIGINT,
        (SELECT COUNT(*) FROM MailJobs mj WHERE mj.BranchId = b.BranchId AND mj.Status IN ('Draft', 'PendingApproval', 'Queued', 'Processing'))::BIGINT,
        (SELECT COUNT(*) FROM MailJobs mj WHERE mj.BranchId = b.BranchId AND mj.Status = 'Completed')::BIGINT,
        -- Email sent stats
        -- Email sent stats (Dynamic count from logs or rough estimate from jobs)
        (SELECT COUNT(*) FROM MailJobLogs mjl 
            JOIN MailJobs mj ON mjl.JobId = mj.JobId 
            WHERE mj.BranchId = b.BranchId AND mjl.Status = 'Sent')::BIGINT,
        -- SMTP accounts accessible
        (SELECT COUNT(*) FROM SmtpAccounts sa 
            JOIN Users u ON sa.OwnerUserId = u.UserId
            JOIN UserBranchRoles ubr ON u.UserId = ubr.UserId
            WHERE ubr.BranchId = b.BranchId AND sa.IsActive = TRUE)::BIGINT
    FROM Branches b
    WHERE b.BranchId = p_branch_id;
END;
$$;

COMMENT ON FUNCTION fn_get_branch_stats IS 'Returns comprehensive statistics for a branch dashboard';

-- Usage: SELECT * FROM fn_get_branch_stats(1);

-- ============================================
-- 3. FN_GET_ALL_BRANCHES_STATS (Admin Overview)
-- Returns stats for all branches
-- ============================================
CREATE OR REPLACE FUNCTION fn_get_all_branches_stats()
RETURNS TABLE (
    BranchId            INT,
    BranchCode          VARCHAR(20),
    BranchName          VARCHAR(100),
    TotalLists          BIGINT,
    TotalContacts       BIGINT,
    TotalJobs           BIGINT,
    TotalEmailsSent     BIGINT
)
LANGUAGE plpgsql
STABLE
AS $$
BEGIN
    RETURN QUERY
    SELECT 
        b.BranchId,
        b.BranchCode,
        b.BranchName,
        (SELECT COUNT(*) FROM RecipientLists rl WHERE rl.BranchId = b.BranchId)::BIGINT,
        (SELECT COALESCE(SUM(rl.TotalContacts), 0) FROM RecipientLists rl WHERE rl.BranchId = b.BranchId)::BIGINT,
        (SELECT COUNT(*) FROM MailJobs mj WHERE mj.BranchId = b.BranchId)::BIGINT,
        (SELECT COUNT(*) FROM MailJobLogs mjl 
            JOIN MailJobs mj ON mjl.JobId = mj.JobId 
            WHERE mj.BranchId = b.BranchId AND mjl.Status = 'Sent')::BIGINT
    FROM Branches b
    WHERE b.IsActive = TRUE
    ORDER BY b.BranchName;
END;
$$;

COMMENT ON FUNCTION fn_get_all_branches_stats IS 'Returns summary statistics for all branches (Admin dashboard)';

-- ============================================
-- 4. TRG_CHECK_BRANCH_ACCESS (Security Trigger)
-- Validates user has access to branch before job creation
-- ============================================
CREATE OR REPLACE FUNCTION fn_check_branch_access()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
DECLARE
    v_has_access BOOLEAN;
    v_user_role user_role;
BEGIN
    -- Check if user has ANY role assignment to this branch
    SELECT EXISTS (
        SELECT 1 FROM UserBranchRoles ubr
        WHERE ubr.UserId = NEW.CreatedBy 
          AND ubr.BranchId = NEW.BranchId
          AND ubr.IsActive = TRUE
    ) INTO v_has_access;
    
    -- Get user's global role
    SELECT Role INTO v_user_role
    FROM Users
    WHERE UserId = NEW.CreatedBy;
    
    -- Admins have access to all branches
    IF v_user_role = 'Admin' THEN
        v_has_access := TRUE;
    END IF;
    
    IF NOT v_has_access THEN
        RAISE EXCEPTION 'Access Denied: User % does not have permission to create jobs in Branch %', 
            NEW.CreatedBy, NEW.BranchId;
    END IF;
    
    -- If 'Member' role, force approval requirement
    IF v_user_role = 'Member' THEN
        NEW.RequiresApproval := TRUE;
        NEW.Status := 'PendingApproval';
        RAISE NOTICE 'Job requires approval: Created by Member role user';
    END IF;
    
    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_check_branch_access ON MailJobs;
CREATE TRIGGER trg_check_branch_access
    BEFORE INSERT ON MailJobs
    FOR EACH ROW
    EXECUTE FUNCTION fn_check_branch_access();

COMMENT ON FUNCTION fn_check_branch_access IS 'Security: Validates branch access and enforces approval for Members';

-- ============================================
-- 5. TRG_AUDIT_JOB_STATUS (Audit Trail Trigger)
-- Logs all status changes to a JSONB audit field
-- ============================================

-- First, add audit column to MailJobs if not exists
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'mailjobs' AND column_name = 'statushistory'
    ) THEN
        ALTER TABLE MailJobs ADD COLUMN StatusHistory JSONB DEFAULT '[]'::JSONB;
    END IF;
END $$;

CREATE OR REPLACE FUNCTION fn_audit_job_status()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
DECLARE
    v_audit_entry JSONB;
BEGIN
    -- Only log if status actually changed
    IF OLD.Status IS DISTINCT FROM NEW.Status THEN
        v_audit_entry := jsonb_build_object(
            'from_status', OLD.Status::TEXT,
            'to_status', NEW.Status::TEXT,
            'changed_at', CURRENT_TIMESTAMP,
            'changed_by', COALESCE(
                CASE 
                    WHEN NEW.Status = 'Approved' THEN NEW.ApprovedBy
                    ELSE NULL
                END,
                0
            )
        );
        
        -- Append to status history array
        NEW.StatusHistory := COALESCE(NEW.StatusHistory, '[]'::JSONB) || v_audit_entry;
        
        RAISE NOTICE 'Job % status changed: % -> %', NEW.JobId, OLD.Status, NEW.Status;
    END IF;
    
    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_audit_job_status ON MailJobs;
CREATE TRIGGER trg_audit_job_status
    BEFORE UPDATE ON MailJobs
    FOR EACH ROW
    EXECUTE FUNCTION fn_audit_job_status();

COMMENT ON FUNCTION fn_audit_job_status IS 'Audit: Tracks all job status changes in StatusHistory JSONB array';

-- ============================================
-- 6. TRG_RESET_DAILY_SMTP_LIMIT (Auto Reset)
-- Resets daily send count at midnight
-- ============================================
CREATE OR REPLACE FUNCTION fn_reset_daily_smtp_limit()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
BEGIN
    -- If the date has changed, reset the counter
    IF NEW.LastResetDate < CURRENT_DATE THEN
        NEW.SentToday := 0;
        NEW.LastResetDate := CURRENT_DATE;
        RAISE NOTICE 'SMTP Account % daily limit reset', NEW.SmtpAccountId;
    END IF;
    
    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_reset_daily_smtp_limit ON SmtpAccounts;
CREATE TRIGGER trg_reset_daily_smtp_limit
    BEFORE UPDATE ON SmtpAccounts
    FOR EACH ROW
    EXECUTE FUNCTION fn_reset_daily_smtp_limit();

COMMENT ON FUNCTION fn_reset_daily_smtp_limit IS 'Auto-resets daily SMTP send counter when date changes';

-- ============================================
-- 7. TRG_UPDATE_LIST_COUNTS (Contact Counter)
-- Auto-updates list contact counts on insert/delete
-- ============================================
CREATE OR REPLACE FUNCTION fn_update_list_counts()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
DECLARE
    v_list_id INT;
BEGIN
    -- Determine which list to update
    IF TG_OP = 'DELETE' THEN
        v_list_id := OLD.ListId;
    ELSE
        v_list_id := NEW.ListId;
    END IF;
    
    -- Update the counts
    UPDATE RecipientLists
    SET 
        TotalContacts = (SELECT COUNT(*) FROM Contacts WHERE ListId = v_list_id),
        ActiveContacts = (SELECT COUNT(*) FROM Contacts WHERE ListId = v_list_id AND Status = 'Active'),
        UpdatedAt = CURRENT_TIMESTAMP
    WHERE ListId = v_list_id;
    
    IF TG_OP = 'DELETE' THEN
        RETURN OLD;
    ELSE
        RETURN NEW;
    END IF;
END;
$$;

DROP TRIGGER IF EXISTS trg_update_list_counts ON Contacts;
CREATE TRIGGER trg_update_list_counts
    AFTER INSERT OR DELETE ON Contacts
    FOR EACH ROW
    EXECUTE FUNCTION fn_update_list_counts();

COMMENT ON FUNCTION fn_update_list_counts IS 'Auto-updates RecipientLists contact counts on insert/delete';

-- ============================================
-- SUCCESS MESSAGE
-- ============================================
DO $$ BEGIN
    RAISE NOTICE 'SiberMailer 2.0 - Triggers & Functions created successfully!';
END $$;
