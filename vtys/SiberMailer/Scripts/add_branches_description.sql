-- Add missing timestamp columns to Branches table
-- These columns track when branches are created and updated

ALTER TABLE Branches 
ADD COLUMN IF NOT EXISTS CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP;

ALTER TABLE Branches 
ADD COLUMN IF NOT EXISTS UpdatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP;

-- Add Description column if not already added
ALTER TABLE Branches 
ADD COLUMN IF NOT EXISTS Description TEXT;
