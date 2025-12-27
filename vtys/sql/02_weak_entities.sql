-- ============================================
-- SIBERMAILER 2.0 - DATABASE SCHEMA
-- Weak Entities & Relationships (Phase 2, Task 2.2)
-- PostgreSQL 18+
-- ============================================

-- ============================================
-- 1. USER_BRANCH_ROLES (Junction Table - M:N)
-- Links Users to Branches with specific roles
-- ============================================
CREATE TABLE IF NOT EXISTS UserBranchRoles (
    UserId              INT         NOT NULL,
    BranchId            INT         NOT NULL,
    AssignedRole        user_role   NOT NULL DEFAULT 'Member',
    AssignedAt          TIMESTAMP   NOT NULL DEFAULT CURRENT_TIMESTAMP,
    AssignedBy          INT,
    IsActive            BOOLEAN     NOT NULL DEFAULT TRUE,
    
    CONSTRAINT pk_user_branch PRIMARY KEY (UserId, BranchId),

    CONSTRAINT fk_ubr_user FOREIGN KEY (UserId) 
        REFERENCES Users(UserId) ON DELETE CASCADE,
    CONSTRAINT fk_ubr_branch FOREIGN KEY (BranchId) 
        REFERENCES Branches(BranchId) ON DELETE CASCADE,
    CONSTRAINT fk_ubr_assignedby FOREIGN KEY (AssignedBy) 
        REFERENCES Users(UserId) ON DELETE SET NULL
);

CREATE INDEX IF NOT EXISTS idx_ubr_user ON UserBranchRoles(UserId);
CREATE INDEX IF NOT EXISTS idx_ubr_branch ON UserBranchRoles(BranchId);

COMMENT ON TABLE UserBranchRoles IS 'Many-to-Many relationship between Users and Branches with role context';

-- ============================================
-- 2. RECIPIENT_LISTS (Weak Entity)
-- Mailing lists owned by branches
-- ============================================
CREATE TABLE IF NOT EXISTS RecipientLists (
    ListId          SERIAL          PRIMARY KEY,
    BranchId        INT             NOT NULL,
    ListName        VARCHAR(100)    NOT NULL,
    Description     TEXT,
    TotalContacts   INT             NOT NULL DEFAULT 0,
    ActiveContacts  INT             NOT NULL DEFAULT 0,
    IsActive        BOOLEAN         NOT NULL DEFAULT TRUE,
    CreatedBy       INT,
    CreatedAt       TIMESTAMP       NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt       TIMESTAMP       NOT NULL DEFAULT CURRENT_TIMESTAMP,
    
    CONSTRAINT fk_list_branch FOREIGN KEY (BranchId) 
        REFERENCES Branches(BranchId) ON DELETE CASCADE,
    CONSTRAINT fk_list_creator FOREIGN KEY (CreatedBy) 
        REFERENCES Users(UserId) ON DELETE SET NULL,
    
    -- List names must be unique within a branch
    CONSTRAINT uq_branch_listname UNIQUE (BranchId, ListName)
);

CREATE INDEX IF NOT EXISTS idx_list_branch ON RecipientLists(BranchId);
CREATE INDEX IF NOT EXISTS idx_list_active ON RecipientLists(IsActive);

COMMENT ON TABLE RecipientLists IS 'Recipient mailing lists belonging to branches';

-- ============================================
-- 3. CONTACTS (Weak Entity with JSONB)
-- Individual contacts within lists
-- ============================================
DO $$ BEGIN
    CREATE TYPE contact_status AS ENUM ('Active', 'Unsubscribed', 'Bounced', 'RedListed', 'Pending');
EXCEPTION
    WHEN duplicate_object THEN null;
END $$;

CREATE TABLE IF NOT EXISTS Contacts (
    ContactId       SERIAL          PRIMARY KEY,
    ListId          INT             NOT NULL,
    Email           VARCHAR(255)    NOT NULL,
    FullName        VARCHAR(100),
    Company         VARCHAR(100),
    
    -- JSONB for flexible custom fields
    CustomData      JSONB           DEFAULT '{}',
    
    Status          contact_status  NOT NULL DEFAULT 'Active',
    BounceCount     INT             NOT NULL DEFAULT 0,
    LastBouncedAt   TIMESTAMP,
    UnsubscribedAt  TIMESTAMP,
    
    CreatedAt       TIMESTAMP       NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt       TIMESTAMP       NOT NULL DEFAULT CURRENT_TIMESTAMP,
    
    CONSTRAINT fk_contact_list FOREIGN KEY (ListId) 
        REFERENCES RecipientLists(ListId) ON DELETE CASCADE,
    
    -- Email must be unique within each list
    CONSTRAINT uq_list_email UNIQUE (ListId, Email)
);

CREATE INDEX IF NOT EXISTS idx_contact_list ON Contacts(ListId);
CREATE INDEX IF NOT EXISTS idx_contact_email ON Contacts(Email);
CREATE INDEX IF NOT EXISTS idx_contact_status ON Contacts(Status);
-- GIN index for JSONB queries
CREATE INDEX IF NOT EXISTS idx_contact_customdata ON Contacts USING GIN (CustomData);

COMMENT ON TABLE Contacts IS 'Individual email contacts with flexible JSONB custom fields';
COMMENT ON COLUMN Contacts.CustomData IS 'Flexible JSON storage for custom fields (e.g., {"Year": 2024, "Department": "IT"})';

-- ============================================
-- 4. TEMPLATES (Weak Entity)
-- Email HTML templates
-- ============================================
CREATE TABLE IF NOT EXISTS Templates (
    TemplateId      SERIAL          PRIMARY KEY,
    BranchId        INT,
    TemplateName    VARCHAR(100)    NOT NULL,
    Subject         VARCHAR(255)    NOT NULL,
    HtmlBody        TEXT            NOT NULL,
    PlainTextBody   TEXT,
    
    -- Placeholders expected (for validation)
    Placeholders    TEXT[]          DEFAULT ARRAY[]::TEXT[],
    
    IsGlobal        BOOLEAN         NOT NULL DEFAULT FALSE,
    IsActive        BOOLEAN         NOT NULL DEFAULT TRUE,
    CreatedBy       INT,
    CreatedAt       TIMESTAMP       NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt       TIMESTAMP       NOT NULL DEFAULT CURRENT_TIMESTAMP,
    
    CONSTRAINT fk_template_branch FOREIGN KEY (BranchId) 
        REFERENCES Branches(BranchId) ON DELETE SET NULL,
    CONSTRAINT fk_template_creator FOREIGN KEY (CreatedBy) 
        REFERENCES Users(UserId) ON DELETE SET NULL
);

CREATE INDEX IF NOT EXISTS idx_template_branch ON Templates(BranchId);
CREATE INDEX IF NOT EXISTS idx_template_global ON Templates(IsGlobal) WHERE IsGlobal = TRUE;

COMMENT ON TABLE Templates IS 'Email HTML templates with placeholder support';
COMMENT ON COLUMN Templates.Placeholders IS 'Array of expected placeholders (e.g., {FullName}, {Company})';

-- ============================================
-- 5. MAIL_JOBS (Weak Entity with Status Enum)
-- Email campaign/job tracking
-- ============================================
DO $$ BEGIN
    CREATE TYPE job_status AS ENUM (
        'Draft', 
        'PendingApproval', 
        'Approved', 
        'Queued', 
        'Processing', 
        'Completed', 
        'Failed', 
        'Cancelled'
    );
EXCEPTION
    WHEN duplicate_object THEN null;
END $$;

CREATE TABLE IF NOT EXISTS MailJobs (
    JobId               SERIAL          PRIMARY KEY,
    JobName             VARCHAR(100)    NOT NULL,
    BranchId            INT             NOT NULL,
    ListId              INT             NOT NULL,
    TemplateId          INT             NOT NULL,
    SmtpAccountId       INT             NOT NULL,
    
    Status              job_status      NOT NULL DEFAULT 'Draft',
    
    -- Progress tracking
    TotalRecipients     INT             NOT NULL DEFAULT 0,
    
    -- Scheduling
    StartedAt           TIMESTAMP,
    CompletedAt         TIMESTAMP,
    
    -- Approval workflow
    RequiresApproval    BOOLEAN         NOT NULL DEFAULT FALSE,
    ApprovedBy          INT,
    ApprovedAt          TIMESTAMP,
    RejectionReason     TEXT,
    
    CreatedBy           INT             NOT NULL,
    CreatedAt           TIMESTAMP       NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt           TIMESTAMP       NOT NULL DEFAULT CURRENT_TIMESTAMP,
    
    CONSTRAINT fk_job_branch FOREIGN KEY (BranchId) 
        REFERENCES Branches(BranchId) ON DELETE RESTRICT,
    CONSTRAINT fk_job_list FOREIGN KEY (ListId) 
        REFERENCES RecipientLists(ListId) ON DELETE RESTRICT,
    CONSTRAINT fk_job_template FOREIGN KEY (TemplateId) 
        REFERENCES Templates(TemplateId) ON DELETE RESTRICT,
    CONSTRAINT fk_job_smtp FOREIGN KEY (SmtpAccountId) 
        REFERENCES SmtpAccounts(SmtpAccountId) ON DELETE RESTRICT,
    CONSTRAINT fk_job_creator FOREIGN KEY (CreatedBy) 
        REFERENCES Users(UserId) ON DELETE RESTRICT,
    CONSTRAINT fk_job_approver FOREIGN KEY (ApprovedBy) 
        REFERENCES Users(UserId) ON DELETE SET NULL
);

CREATE INDEX IF NOT EXISTS idx_job_branch ON MailJobs(BranchId);
CREATE INDEX IF NOT EXISTS idx_job_status ON MailJobs(Status);
CREATE INDEX IF NOT EXISTS idx_job_pending ON MailJobs(Status) WHERE Status = 'PendingApproval';

COMMENT ON TABLE MailJobs IS 'Email campaign jobs with status tracking and approval workflow';

-- ============================================
-- 6. MAIL_JOB_LOGS (Weak Entity with JSONB)
-- Detailed logging for each email sent
-- ============================================
DO $$ BEGIN
    CREATE TYPE log_status AS ENUM ('Sent', 'Failed', 'Bounced', 'Opened', 'Clicked');
EXCEPTION
    WHEN duplicate_object THEN null;
END $$;

CREATE TABLE IF NOT EXISTS MailJobLogs (
    LogId           SERIAL          PRIMARY KEY,
    JobId           INT             NOT NULL,
    ContactId       INT             NOT NULL,
    
    Status          log_status      NOT NULL DEFAULT 'Sent',
    
    -- JSONB for flexible log details
    LogDetails      JSONB           NOT NULL DEFAULT '{}',
    
    SentAt          TIMESTAMP,
    ErrorMessage    TEXT,
    
    CreatedAt       TIMESTAMP       NOT NULL DEFAULT CURRENT_TIMESTAMP,
    
    CONSTRAINT fk_log_job FOREIGN KEY (JobId) 
        REFERENCES MailJobs(JobId) ON DELETE CASCADE,
    CONSTRAINT fk_log_contact FOREIGN KEY (ContactId) 
        REFERENCES Contacts(ContactId) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS idx_log_job ON MailJobLogs(JobId);
CREATE INDEX IF NOT EXISTS idx_log_contact ON MailJobLogs(ContactId);
CREATE INDEX IF NOT EXISTS idx_log_status ON MailJobLogs(Status);
-- GIN index for JSONB log queries
CREATE INDEX IF NOT EXISTS idx_log_details ON MailJobLogs USING GIN (LogDetails);

COMMENT ON TABLE MailJobLogs IS 'Per-email send logs with JSONB details for analytics';
COMMENT ON COLUMN MailJobLogs.LogDetails IS 'Flexible JSON (e.g., {"smtp_response": "250 OK", "delivery_time_ms": 234})';

-- ============================================
-- APPLY TIMESTAMP TRIGGERS TO NEW TABLES
-- ============================================
DROP TRIGGER IF EXISTS trg_recipientlists_timestamp ON RecipientLists;
CREATE TRIGGER trg_recipientlists_timestamp
    BEFORE UPDATE ON RecipientLists
    FOR EACH ROW EXECUTE FUNCTION fn_update_timestamp();

DROP TRIGGER IF EXISTS trg_contacts_timestamp ON Contacts;
CREATE TRIGGER trg_contacts_timestamp
    BEFORE UPDATE ON Contacts
    FOR EACH ROW EXECUTE FUNCTION fn_update_timestamp();

DROP TRIGGER IF EXISTS trg_templates_timestamp ON Templates;
CREATE TRIGGER trg_templates_timestamp
    BEFORE UPDATE ON Templates
    FOR EACH ROW EXECUTE FUNCTION fn_update_timestamp();

DROP TRIGGER IF EXISTS trg_mailjobs_timestamp ON MailJobs;
CREATE TRIGGER trg_mailjobs_timestamp
    BEFORE UPDATE ON MailJobs
    FOR EACH ROW EXECUTE FUNCTION fn_update_timestamp();

-- ============================================
-- SUCCESS MESSAGE
-- ============================================
DO $$ BEGIN
    RAISE NOTICE 'SiberMailer 2.0 - Weak Entities created successfully!';
END $$;
