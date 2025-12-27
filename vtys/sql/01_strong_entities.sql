-- ============================================
-- SIBERMAILER 2.0 - DATABASE SCHEMA
-- Strong Entities (Phase 2, Task 2.1)
-- PostgreSQL 18+
-- ============================================

-- Enable UUID extension if not already enabled
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- ============================================
-- 1. BRANCHES (Strong Entity)
-- Represents organizational units/departments
-- ============================================
CREATE TABLE IF NOT EXISTS Branches (
    BranchId        SERIAL          PRIMARY KEY,
    BranchCode      VARCHAR(20)     NOT NULL UNIQUE,
    BranchName      VARCHAR(100)    NOT NULL,
    Description     TEXT,
    IsActive        BOOLEAN         NOT NULL DEFAULT TRUE,
    CreatedAt       TIMESTAMP       NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt       TIMESTAMP       NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- Index for frequently queried columns
CREATE INDEX IF NOT EXISTS idx_branches_code ON Branches(BranchCode);
CREATE INDEX IF NOT EXISTS idx_branches_active ON Branches(IsActive);

COMMENT ON TABLE Branches IS 'Organizational branches/departments for multi-tenant mail operations';
COMMENT ON COLUMN Branches.BranchCode IS 'Unique alphanumeric code (e.g., IST-001, ANK-002)';

-- ============================================
-- 2. USERS (Strong Entity)
-- System users with role-based access
-- ============================================
DO $$ BEGIN
    CREATE TYPE user_role AS ENUM ('Admin', 'Manager', 'Member');
EXCEPTION
    WHEN duplicate_object THEN null;
END $$;

CREATE TABLE IF NOT EXISTS Users (
    UserId          SERIAL          PRIMARY KEY,
    Username        VARCHAR(50)     NOT NULL UNIQUE,
    Email           VARCHAR(255)    NOT NULL UNIQUE,
    PasswordHash    VARCHAR(256)    NOT NULL,
    FullName        VARCHAR(100)    NOT NULL,
    Role            user_role       NOT NULL DEFAULT 'Member',
    IsActive        BOOLEAN         NOT NULL DEFAULT TRUE,
    LastLoginAt     TIMESTAMP,
    CreatedAt       TIMESTAMP       NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt       TIMESTAMP       NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- Indexes for authentication queries
CREATE INDEX IF NOT EXISTS idx_users_username ON Users(Username);
CREATE INDEX IF NOT EXISTS idx_users_email ON Users(Email);
CREATE INDEX IF NOT EXISTS idx_users_active ON Users(IsActive);

COMMENT ON TABLE Users IS 'System users with authentication and role information';
COMMENT ON COLUMN Users.PasswordHash IS 'Hashed password (SHA-256 or BCrypt)';
COMMENT ON COLUMN Users.Role IS 'User role: Admin (full access), Manager (branch-level), Member (limited)';

-- ============================================
-- 3. SMTP_ACCOUNTS (Strong Entity - The Vault)
-- Encrypted SMTP credentials storage
-- ============================================
CREATE TABLE IF NOT EXISTS SmtpAccounts (
    SmtpAccountId   SERIAL          PRIMARY KEY,
    AccountName     VARCHAR(100)    NOT NULL UNIQUE,
    SmtpHost        VARCHAR(255)    NOT NULL,
    SmtpPort        INT             NOT NULL DEFAULT 587,
    UseSsl          BOOLEAN         NOT NULL DEFAULT TRUE,
    
    -- Email and Password (Email is plain text, Password is AES-256 encrypted)
    Email               VARCHAR(255) NOT NULL,
    EncryptedPassword   TEXT        NOT NULL,
    EncryptionIV        VARCHAR(44) NOT NULL,
    
    -- Rate Limiting
    DailyLimit          INT         NOT NULL DEFAULT 500,
    SentToday           INT         NOT NULL DEFAULT 0,
    LastResetDate       DATE        NOT NULL DEFAULT CURRENT_DATE,
    
    -- Ownership (Branch-based)
    OwnerBranchId       INT         NOT NULL,
    IsShared            BOOLEAN     NOT NULL DEFAULT FALSE,
    IsActive            BOOLEAN     NOT NULL DEFAULT TRUE,
    
    CreatedAt           TIMESTAMP   NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt           TIMESTAMP   NOT NULL DEFAULT CURRENT_TIMESTAMP,
    
    CONSTRAINT fk_smtp_branch FOREIGN KEY (OwnerBranchId) 
        REFERENCES Branches(BranchId) ON DELETE RESTRICT
);

-- Index for ownership queries
CREATE INDEX IF NOT EXISTS idx_smtp_branch ON SmtpAccounts(OwnerBranchId);
CREATE INDEX IF NOT EXISTS idx_smtp_active ON SmtpAccounts(IsActive);
CREATE INDEX IF NOT EXISTS idx_smtp_shared ON SmtpAccounts(IsShared) WHERE IsShared = TRUE;

COMMENT ON TABLE SmtpAccounts IS 'The Vault: SMTP account credentials with encrypted password';
COMMENT ON COLUMN SmtpAccounts.Email IS 'SMTP email address (plain text, not encrypted)';
COMMENT ON COLUMN SmtpAccounts.EncryptedPassword IS 'AES-256 encrypted password (Base64)';
COMMENT ON COLUMN SmtpAccounts.EncryptionIV IS 'Initialization Vector for AES decryption';
COMMENT ON COLUMN SmtpAccounts.DailyLimit IS 'Max emails per day to prevent blacklisting';
COMMENT ON COLUMN SmtpAccounts.OwnerBranchId IS 'Branch that owns this SMTP account';

-- ============================================
-- HELPER: Auto-update timestamp trigger
-- ============================================
CREATE OR REPLACE FUNCTION fn_update_timestamp()
RETURNS TRIGGER AS $$
BEGIN
    NEW.UpdatedAt = CURRENT_TIMESTAMP;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Apply to all Strong Entity tables
DROP TRIGGER IF EXISTS trg_branches_timestamp ON Branches;
CREATE TRIGGER trg_branches_timestamp
    BEFORE UPDATE ON Branches
    FOR EACH ROW EXECUTE FUNCTION fn_update_timestamp();

DROP TRIGGER IF EXISTS trg_users_timestamp ON Users;
CREATE TRIGGER trg_users_timestamp
    BEFORE UPDATE ON Users
    FOR EACH ROW EXECUTE FUNCTION fn_update_timestamp();

DROP TRIGGER IF EXISTS trg_smtp_timestamp ON SmtpAccounts;
CREATE TRIGGER trg_smtp_timestamp
    BEFORE UPDATE ON SmtpAccounts
    FOR EACH ROW EXECUTE FUNCTION fn_update_timestamp();

-- ============================================
-- SUCCESS MESSAGE
-- ============================================
DO $$ BEGIN
    RAISE NOTICE 'SiberMailer 2.0 - Strong Entities created successfully!';
END $$;
