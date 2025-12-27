-- ============================================
-- SIBERMAILER 2.0 - DATABASE RESET SCRIPT
-- ============================================
-- PURPOSE: Truncate all tables except Users
-- PRESERVES: Only admin user (admin/admin)
-- WARNING: This will DELETE ALL DATA except the admin user!
-- ============================================

BEGIN;

-- ============================================
-- STEP 1: Disable Foreign Key Checks Temporarily
-- ============================================
SET session_replication_role = 'replica';

-- ============================================
-- STEP 2: Truncate All Tables (Except Users)
-- ============================================
-- Order matters for dependencies

-- Mail operation data (most dependent)
TRUNCATE TABLE MailJobLogs CASCADE;
TRUNCATE TABLE MailJobs CASCADE;

-- Contact data
TRUNCATE TABLE Contacts CASCADE;
TRUNCATE TABLE RecipientLists CASCADE;

-- Template data
TRUNCATE TABLE Templates CASCADE;

-- SMTP Accounts
TRUNCATE TABLE SmtpAccounts CASCADE;

-- User-Branch relationships
TRUNCATE TABLE UserBranchRoles CASCADE;

-- Branches
TRUNCATE TABLE Branches CASCADE;

-- ============================================
-- STEP 3: Clean Users Table (Keep Only Admin)
-- ============================================
-- Delete all non-admin users first
DELETE FROM Users WHERE Username != 'admin';

-- ============================================
-- STEP 4: Re-enable Foreign Key Checks
-- ============================================
SET session_replication_role = 'origin';

-- ============================================
-- STEP 5: Reset All Sequences to 1
-- ============================================

ALTER SEQUENCE IF EXISTS branches_branchid_seq RESTART WITH 1;
ALTER SEQUENCE IF EXISTS smtpaccounts_smtpaccountid_seq RESTART WITH 1;
ALTER SEQUENCE IF EXISTS recipientlists_listid_seq RESTART WITH 1;
ALTER SEQUENCE IF EXISTS contacts_contactid_seq RESTART WITH 1;
ALTER SEQUENCE IF EXISTS templates_templateid_seq RESTART WITH 1;
ALTER SEQUENCE IF EXISTS mailjobs_jobid_seq RESTART WITH 1;
ALTER SEQUENCE IF EXISTS mailjoblogs_logid_seq RESTART WITH 1;

-- Don't reset users_userid_seq to preserve admin ID

-- ============================================
-- STEP 6: Ensure Admin User Exists
-- ============================================
DO $$
DECLARE
    v_admin_exists BOOLEAN;
BEGIN
    -- Check if admin user exists
    SELECT EXISTS (SELECT 1 FROM Users WHERE Username = 'admin') INTO v_admin_exists;
    
    IF NOT v_admin_exists THEN
        -- Create admin user if doesn't exist
        -- Password: "admin" (SHA-256 hash)
        INSERT INTO Users (
            Username, 
            Email, 
            PasswordHash, 
            FullName, 
            Role, 
            IsActive,
            CreatedAt,
            UpdatedAt
        ) VALUES (
            'admin',
            'admin@sibermailer.com',
            '8c6976e5b5410415bde908bd4dee15dfb167a9c873fc4bb8a81f6f2ab448a918', -- SHA-256 hash of "admin"
            'System Administrator',
            'Admin',
            TRUE,
            CURRENT_TIMESTAMP,
            CURRENT_TIMESTAMP
        );
        
        RAISE NOTICE 'Admin user created: admin / admin';
    ELSE
        -- Update existing admin user to ensure password is "admin"
        UPDATE Users
        SET 
            PasswordHash = '8c6976e5b5410415bde908bd4dee15dfb167a9c873fc4bb8a81f6f2ab448a918',
            Email = 'admin@sibermailer.com',
            FullName = 'System Administrator',
            Role = 'Admin',
            IsActive = TRUE,
            UpdatedAt = CURRENT_TIMESTAMP
        WHERE Username = 'admin';
        
        RAISE NOTICE 'Admin user updated: admin / admin';
    END IF;
END $$;

-- ============================================
-- STEP 7: Verification Report
-- ============================================
DO $$
DECLARE
    v_users INT;
    v_branches INT;
    v_contacts INT;
    v_lists INT;
    v_jobs INT;
    v_templates INT;
    v_smtp INT;
BEGIN
    SELECT COUNT(*) INTO v_users FROM Users;
    SELECT COUNT(*) INTO v_branches FROM Branches;
    SELECT COUNT(*) INTO v_contacts FROM Contacts;
    SELECT COUNT(*) INTO v_lists FROM RecipientLists;
    SELECT COUNT(*) INTO v_jobs FROM MailJobs;
    SELECT COUNT(*) INTO v_templates FROM Templates;
    SELECT COUNT(*) INTO v_smtp FROM SmtpAccounts;
    
    RAISE NOTICE '';
    RAISE NOTICE '========================================';
    RAISE NOTICE '     DATABASE RESET VERIFICATION       ';
    RAISE NOTICE '========================================';
    RAISE NOTICE 'Users:          % (should be 1)', v_users;
    RAISE NOTICE 'Branches:       % (should be 0)', v_branches;
    RAISE NOTICE 'Contacts:       % (should be 0)', v_contacts;
    RAISE NOTICE 'RecipientLists: % (should be 0)', v_lists;
    RAISE NOTICE 'MailJobs:       % (should be 0)', v_jobs;
    RAISE NOTICE 'Templates:      % (should be 0)', v_templates;
    RAISE NOTICE 'SmtpAccounts:   % (should be 0)', v_smtp;
    RAISE NOTICE '========================================';
    
    IF v_users = 1 AND v_branches = 0 AND v_contacts = 0 
       AND v_lists = 0 AND v_jobs = 0 AND v_templates = 0 
       AND v_smtp = 0 THEN
        RAISE NOTICE '✅ DATABASE RESET SUCCESSFUL!';
        RAISE NOTICE '   Login with: admin / admin';
    ELSE
        RAISE WARNING '⚠️  Unexpected data counts!';
    END IF;
    RAISE NOTICE '========================================';
END $$;

COMMIT;

-- ============================================
-- FINAL CHECK: Display Admin User
-- ============================================
SELECT 
    UserId,
    Username,
    Email,
    FullName,
    Role,
    IsActive,
    CreatedAt
FROM Users
WHERE Username = 'admin';
