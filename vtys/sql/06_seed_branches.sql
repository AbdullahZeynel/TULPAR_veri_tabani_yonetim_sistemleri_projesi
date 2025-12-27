-- ============================================
-- SIBERMAILER - Branch Seeding Script
-- Run once to ensure required branches exist
-- ============================================

-- Insert 'Sponsorluk Birimi' if not exists
INSERT INTO Branches (BranchCode, BranchName, Description, IsActive)
SELECT 'SPONSOR', 'Sponsorluk Birimi', 'Sponsorship and partnerships department', TRUE
WHERE NOT EXISTS (
    SELECT 1 FROM Branches WHERE BranchCode = 'SPONSOR'
);

-- Insert 'Tanıtım Birimi' if not exists  
INSERT INTO Branches (BranchCode, BranchName, Description, IsActive)
SELECT 'TANITIM', 'Tanıtım Birimi', 'Promotion and marketing department', TRUE
WHERE NOT EXISTS (
    SELECT 1 FROM Branches WHERE BranchCode = 'TANITIM'
);

-- Verify insertion
SELECT BranchId, BranchCode, BranchName, IsActive 
FROM Branches 
ORDER BY BranchId;
