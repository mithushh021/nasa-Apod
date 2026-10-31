
-- ==============================================================
-- NASA APOD Gallery - Database Creation Script
-- Assignment: Technical Assignment 02 - NASA APOD Gallery
-- Target RDBMS: Microsoft SQL Server / SQL Express
-- ==============================================================

-- 1. Create database if it does not exist
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'NasaApodDB')
BEGIN
    CREATE DATABASE NasaApodDB;
END
GO

USE NasaApodDB;
GO

-- 2. Create Apod table to store NASA APOD response records
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = N'Apod')
BEGIN
    CREATE TABLE Apod
    (
        -- Automatically generated primary key
        Id INT IDENTITY(1,1) PRIMARY KEY,

        -- Date of NASA astronomy picture (UNIQUE constraint prevents duplicates)
        [Date] DATE NOT NULL UNIQUE,

        -- Title of the astronomy picture
        Title NVARCHAR(300) NOT NULL,

        -- Full explanation provided by NASA
        Explanation NVARCHAR(MAX) NULL,

        -- Image preview or video embed URL
        [Url] NVARCHAR(2000) NOT NULL,

        -- High definition image URL (if available)
        Hdurl NVARCHAR(2000) NULL,

        -- Media type: 'image' or 'video'
        MediaType NVARCHAR(50) NOT NULL,

        -- NASA API service version
        ServiceVersion NVARCHAR(20) NULL,

        -- Copyright / Author credits
        Copyright NVARCHAR(200) NULL,

        -- Timestamp when the record was saved to local database
        SavedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
    );

    -- Create non-clustered index on Date for fast sorting and range searches
    CREATE NONCLUSTERED INDEX IX_Apod_Date ON Apod([Date] DESC);
END
GO

-- 3. Verification query
SELECT 
    COUNT(*) AS TotalRecords,
    MIN([Date]) AS EarliestPicture,
    MAX([Date]) AS LatestPicture
FROM Apod;
GO


