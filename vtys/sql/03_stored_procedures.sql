-- ============================================
-- SIBERMAILER 2.0 - STORED PROCEDURES
-- Phase 2, Task 2.3: Critical Business Logic
-- PostgreSQL 18+
-- ============================================

-- ============================================
-- 1. SP_IMPORT_CONTACTS_BULK
-- Bulk import contacts from JSONB array
-- Handles duplicates with ON CONFLICT
-- ============================================
CREATE OR REPLACE PROCEDURE sp_import_contacts_bulk(
    p_list_id       INT,
    p_contacts_json JSONB,
    OUT p_inserted  INT,
    OUT p_updated   INT,
    OUT p_skipped   INT
)
LANGUAGE plpgsql
AS $$
DECLARE
    v_contact       JSONB;
    v_email         VARCHAR(255);
    v_fullname      VARCHAR(100);
    v_company       VARCHAR(100);
    v_custom_data   JSONB;
    v_exists        BOOLEAN;
BEGIN
    -- Initialize counters
    p_inserted := 0;
    p_updated := 0;
    p_skipped := 0;
    
    -- Validate list exists
    IF NOT EXISTS (SELECT 1 FROM RecipientLists WHERE ListId = p_list_id AND IsActive = TRUE) THEN
        RAISE EXCEPTION 'List ID % does not exist or is inactive', p_list_id;
    END IF;
    
    -- Loop through JSONB array
    FOR v_contact IN SELECT * FROM jsonb_array_elements(p_contacts_json)
    LOOP
        -- Extract fields with null safety
        v_email := LOWER(TRIM(v_contact->>'Email'));
        v_fullname := TRIM(v_contact->>'FullName');
        v_company := TRIM(v_contact->>'Company');
        v_custom_data := COALESCE(v_contact->'CustomData', '{}'::JSONB);
        
        -- Skip if email is null or empty
        IF v_email IS NULL OR v_email = '' THEN
            p_skipped := p_skipped + 1;
            CONTINUE;
        END IF;
        
        -- Skip if email format is invalid (basic check)
        IF v_email NOT LIKE '%@%.%' THEN
            p_skipped := p_skipped + 1;
            CONTINUE;
        END IF;
        
        -- Check if exists
        SELECT EXISTS (
            SELECT 1 FROM Contacts WHERE ListId = p_list_id AND Email = v_email
        ) INTO v_exists;
        
        IF v_exists THEN
            -- Update existing contact
            UPDATE Contacts
            SET 
                FullName = COALESCE(v_fullname, FullName),
                Company = COALESCE(v_company, Company),
                CustomData = CustomData || v_custom_data,
                UpdatedAt = CURRENT_TIMESTAMP
            WHERE ListId = p_list_id AND Email = v_email;
            
            p_updated := p_updated + 1;
        ELSE
            -- Insert new contact
            INSERT INTO Contacts (ListId, Email, FullName, Company, CustomData, Status)
            VALUES (p_list_id, v_email, v_fullname, v_company, v_custom_data, 'Active');
            
            p_inserted := p_inserted + 1;
        END IF;
    END LOOP;
    
    -- Update list statistics
    UPDATE RecipientLists
    SET 
        TotalContacts = (SELECT COUNT(*) FROM Contacts WHERE ListId = p_list_id),
        ActiveContacts = (SELECT COUNT(*) FROM Contacts WHERE ListId = p_list_id AND Status = 'Active'),
        UpdatedAt = CURRENT_TIMESTAMP
    WHERE ListId = p_list_id;
    
    -- Log the import operation
    RAISE NOTICE 'Bulk Import Complete: % inserted, % updated, % skipped', p_inserted, p_updated, p_skipped;
END;
$$;

COMMENT ON PROCEDURE sp_import_contacts_bulk IS 'Bulk import contacts from JSONB array with duplicate handling';

-- ============================================
-- 2. SP_PROCESS_BOUNCE
-- Handle bounced emails and Red List logic
-- ============================================
CREATE OR REPLACE PROCEDURE sp_process_bounce(
    p_contact_id    INT,
    OUT p_new_status contact_status,
    p_bounce_reason TEXT DEFAULT NULL
)
LANGUAGE plpgsql
AS $$
DECLARE
    v_current_bounce_count INT;
    v_max_bounces CONSTANT INT := 3;
    v_list_id INT;
BEGIN
    -- Get current bounce count and list
    SELECT BounceCount, ListId 
    INTO v_current_bounce_count, v_list_id
    FROM Contacts 
    WHERE ContactId = p_contact_id;
    
    IF NOT FOUND THEN
        RAISE EXCEPTION 'Contact ID % not found', p_contact_id;
    END IF;
    
    -- Increment bounce count
    v_current_bounce_count := v_current_bounce_count + 1;
    
    -- Determine new status based on bounce count
    IF v_current_bounce_count >= v_max_bounces THEN
        -- Red List the contact (too many bounces)
        p_new_status := 'RedListed';
        
        RAISE NOTICE 'Contact % has been RED LISTED after % bounces', p_contact_id, v_current_bounce_count;
    ELSE
        -- Mark as bounced but not red listed yet
        p_new_status := 'Bounced';
        
        RAISE NOTICE 'Contact % marked as Bounced (% of % max)', p_contact_id, v_current_bounce_count, v_max_bounces;
    END IF;
    
    -- Update the contact
    UPDATE Contacts
    SET 
        Status = p_new_status,
        BounceCount = v_current_bounce_count,
        LastBouncedAt = CURRENT_TIMESTAMP,
        CustomData = CustomData || jsonb_build_object(
            'last_bounce_reason', COALESCE(p_bounce_reason, 'Unknown'),
            'last_bounce_at', CURRENT_TIMESTAMP::TEXT
        ),
        UpdatedAt = CURRENT_TIMESTAMP
    WHERE ContactId = p_contact_id;
    
    -- Update list active count
    UPDATE RecipientLists
    SET 
        ActiveContacts = (
            SELECT COUNT(*) FROM Contacts 
            WHERE ListId = v_list_id AND Status = 'Active'
        ),
        UpdatedAt = CURRENT_TIMESTAMP
    WHERE ListId = v_list_id;
    
END;
$$;

COMMENT ON PROCEDURE sp_process_bounce IS 'Process email bounce and apply Red List logic after 3 bounces';

-- ============================================
-- 3. SP_APPROVE_MAIL_JOB
-- Approval workflow for mail jobs
-- ============================================
CREATE OR REPLACE PROCEDURE sp_approve_mail_job(
    p_job_id        INT,
    p_approver_id   INT,
    p_approved      BOOLEAN,
    OUT p_result    TEXT,
    p_reason        TEXT DEFAULT NULL
)
LANGUAGE plpgsql
AS $$
DECLARE
    v_current_status    job_status;
    v_job_name          VARCHAR(100);
    v_approver_role     user_role;
BEGIN
    -- Get current job status
    SELECT Status, JobName 
    INTO v_current_status, v_job_name
    FROM MailJobs 
    WHERE JobId = p_job_id;
    
    IF NOT FOUND THEN
        p_result := 'ERROR: Job ID not found';
        RETURN;
    END IF;
    
    -- Check if job is in pending approval status
    IF v_current_status != 'PendingApproval' THEN
        p_result := 'ERROR: Job is not pending approval. Current status: ' || v_current_status::TEXT;
        RETURN;
    END IF;
    
    -- Verify approver has Admin or Manager role
    SELECT Role INTO v_approver_role
    FROM Users
    WHERE UserId = p_approver_id AND IsActive = TRUE;
    
    IF NOT FOUND THEN
        p_result := 'ERROR: Approver not found or inactive';
        RETURN;
    END IF;
    
    IF v_approver_role = 'Member' THEN
        p_result := 'ERROR: Members cannot approve jobs. Admin or Manager role required.';
        RETURN;
    END IF;
    
    IF p_approved THEN
        -- Approve the job
        UPDATE MailJobs
        SET 
            Status = 'Approved',
            ApprovedBy = p_approver_id,
            ApprovedAt = CURRENT_TIMESTAMP,
            RejectionReason = NULL,
            UpdatedAt = CURRENT_TIMESTAMP
        WHERE JobId = p_job_id;
        
        p_result := 'SUCCESS: Job "' || v_job_name || '" approved and ready for processing';
        
        RAISE NOTICE 'Job % approved by user %', p_job_id, p_approver_id;
    ELSE
        -- Reject the job
        UPDATE MailJobs
        SET 
            Status = 'Draft',
            ApprovedBy = NULL,
            ApprovedAt = NULL,
            RejectionReason = COALESCE(p_reason, 'No reason provided'),
            UpdatedAt = CURRENT_TIMESTAMP
        WHERE JobId = p_job_id;
        
        p_result := 'REJECTED: Job "' || v_job_name || '" sent back to draft. Reason: ' || COALESCE(p_reason, 'No reason provided');
        
        RAISE NOTICE 'Job % rejected by user %. Reason: %', p_job_id, p_approver_id, COALESCE(p_reason, 'None');
    END IF;
    
END;
$$;

COMMENT ON PROCEDURE sp_approve_mail_job IS 'Approve or reject a pending mail job with role validation';

-- ============================================
-- HELPER PROCEDURE: Create Mail Job
-- ============================================
CREATE OR REPLACE PROCEDURE sp_create_mail_job(
    p_job_name          VARCHAR(100),
    p_branch_id         INT,
    p_list_id           INT,
    p_template_id       INT,
    p_smtp_account_id   INT,
    p_created_by        INT,
    OUT p_job_id        INT,
    p_requires_approval BOOLEAN DEFAULT FALSE
)
LANGUAGE plpgsql
AS $$
DECLARE
    v_total_recipients INT;
    v_initial_status job_status;
BEGIN
    -- Count recipients
    SELECT COUNT(*) INTO v_total_recipients
    FROM Contacts
    WHERE ListId = p_list_id AND Status = 'Active';
    
    IF v_total_recipients = 0 THEN
        RAISE EXCEPTION 'Cannot create job: No active contacts in list %', p_list_id;
    END IF;
    
    -- Determine initial status
    IF p_requires_approval THEN
        v_initial_status := 'PendingApproval';
    ELSE
        v_initial_status := 'Queued';
    END IF;
    
    -- Create the job
    INSERT INTO MailJobs (
        JobName, BranchId, ListId, TemplateId, SmtpAccountId,
        Status, TotalRecipients, RequiresApproval, CreatedBy
    )
    VALUES (
        p_job_name, p_branch_id, p_list_id, p_template_id, p_smtp_account_id,
        v_initial_status, v_total_recipients, p_requires_approval, p_created_by
    )
    RETURNING JobId INTO p_job_id;
    
    RAISE NOTICE 'Mail Job % created with status %', p_job_id, v_initial_status;
END;
$$;

COMMENT ON PROCEDURE sp_create_mail_job IS 'Create a new mail job with automatic recipient counting';

-- ============================================
-- SUCCESS MESSAGE
-- ============================================
DO $$ BEGIN
    RAISE NOTICE 'SiberMailer 2.0 - Stored Procedures created successfully!';
END $$;
