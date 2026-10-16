/* =========================================================
   RaceDay - Database Schema and Seed Data
   Target: SQL Server / SSMS
   Revised for Part 2: Events now has DistanceKM and EventType
   ========================================================= */

IF DB_ID('RaceDayDB') IS NULL
BEGIN
    CREATE DATABASE RaceDayDB;
END
GO

USE RaceDayDB;
GO

/* Drop tables if they already exist, child-first, so the script can be re-run cleanly */
IF OBJECT_ID('dbo.Results', 'U') IS NOT NULL DROP TABLE dbo.Results;
IF OBJECT_ID('dbo.Enrolments', 'U') IS NOT NULL DROP TABLE dbo.Enrolments;
IF OBJECT_ID('dbo.Routes', 'U') IS NOT NULL DROP TABLE dbo.Routes;
IF OBJECT_ID('dbo.Categories', 'U') IS NOT NULL DROP TABLE dbo.Categories;
IF OBJECT_ID('dbo.Events', 'U') IS NOT NULL DROP TABLE dbo.Events;
IF OBJECT_ID('dbo.Users', 'U') IS NOT NULL DROP TABLE dbo.Users;
GO

/* =========================================================
   TABLE: Users
   ========================================================= */
CREATE TABLE dbo.Users (
    UserID       INT IDENTITY(1,1) PRIMARY KEY,
    FullName     NVARCHAR(100) NOT NULL,
    Email        NVARCHAR(150) NOT NULL UNIQUE,
    PasswordHash NVARCHAR(255) NOT NULL,
    Role         NVARCHAR(20)  NOT NULL DEFAULT 'Participant',
    CreatedAt    DATETIME      NOT NULL DEFAULT GETDATE(),
    CONSTRAINT CK_Users_Role CHECK (Role IN ('Organiser', 'Participant'))
);
GO

/* =========================================================
   TABLE: Events
   Revised: added DistanceKM and EventType (Run / Walk / Cycle)
   ========================================================= */
CREATE TABLE dbo.Events (
    EventID     INT IDENTITY(1,1) PRIMARY KEY,
    OrganiserID INT NOT NULL,
    EventName   NVARCHAR(150) NOT NULL,
    Description NVARCHAR(MAX) NULL,
    EventDate   DATE NOT NULL,
    Location    NVARCHAR(150) NOT NULL,
    DistanceKM  DECIMAL(6,2) NOT NULL,
    EventType   NVARCHAR(20) NOT NULL DEFAULT 'Run',
    CreatedAt   DATETIME NOT NULL DEFAULT GETDATE(),
    CONSTRAINT FK_Events_Organiser FOREIGN KEY (OrganiserID) REFERENCES dbo.Users(UserID),
    CONSTRAINT CK_Events_Distance CHECK (DistanceKM > 0),
    CONSTRAINT CK_Events_EventType CHECK (EventType IN ('Run', 'Walk', 'Cycle'))
);
GO

/* =========================================================
   TABLE: Categories
   One event has many categories (e.g. 5km, 10km, 21km, Under 20, Senior)
   ========================================================= */
CREATE TABLE dbo.Categories (
    CategoryID      INT IDENTITY(1,1) PRIMARY KEY,
    EventID         INT NOT NULL,
    CategoryName    NVARCHAR(100) NOT NULL,
    DistanceKM      DECIMAL(6,2) NOT NULL,
    MaxParticipants INT NOT NULL DEFAULT 100,
    EntryFee        DECIMAL(8,2) NOT NULL DEFAULT 0,
    CONSTRAINT FK_Categories_Event FOREIGN KEY (EventID) REFERENCES dbo.Events(EventID),
    CONSTRAINT CK_Categories_Distance CHECK (DistanceKM > 0),
    CONSTRAINT CK_Categories_MaxParticipants CHECK (MaxParticipants > 0)
);
GO

/* =========================================================
   TABLE: Routes
   One-to-one with Categories - each category has one route
   ========================================================= */
CREATE TABLE dbo.Routes (
    RouteID    INT IDENTITY(1,1) PRIMARY KEY,
    CategoryID INT NOT NULL UNIQUE,
    StartPoint NVARCHAR(150) NOT NULL,
    EndPoint   NVARCHAR(150) NOT NULL,
    MapURL     NVARCHAR(255) NULL,
    CONSTRAINT FK_Routes_Category FOREIGN KEY (CategoryID) REFERENCES dbo.Categories(CategoryID)
);
GO

/* =========================================================
   TABLE: Enrolments
   A participant enrols into one category of one event
   ========================================================= */
CREATE TABLE dbo.Enrolments (
    EnrolmentID   INT IDENTITY(1,1) PRIMARY KEY,
    ParticipantID INT NOT NULL,
    CategoryID    INT NOT NULL,
    EnrolmentDate DATE NOT NULL DEFAULT CAST(GETDATE() AS DATE),
    Status        NVARCHAR(20) NOT NULL DEFAULT 'Registered',
    CONSTRAINT FK_Enrolments_Participant FOREIGN KEY (ParticipantID) REFERENCES dbo.Users(UserID),
    CONSTRAINT FK_Enrolments_Category    FOREIGN KEY (CategoryID)    REFERENCES dbo.Categories(CategoryID),
    CONSTRAINT CK_Enrolments_Status CHECK (Status IN ('Registered', 'Cancelled', 'Completed')),
    CONSTRAINT UQ_Enrolments_Participant_Category UNIQUE (ParticipantID, CategoryID)
);
GO

/* =========================================================
   TABLE: Results
   One-to-one with Enrolments - each enrolment produces one result
   ========================================================= */
CREATE TABLE dbo.Results (
    ResultID    INT IDENTITY(1,1) PRIMARY KEY,
    EnrolmentID INT NOT NULL UNIQUE,
    FinishTime  TIME NULL,
    Position    INT NULL,
    Status      NVARCHAR(20) NOT NULL DEFAULT 'Pending',
    CONSTRAINT FK_Results_Enrolment FOREIGN KEY (EnrolmentID) REFERENCES dbo.Enrolments(EnrolmentID),
    CONSTRAINT CK_Results_Status CHECK (Status IN ('Pending', 'Finished', 'DNF', 'DSQ'))
);
GO

/* =========================================================
   SEED DATA
   ========================================================= */

-- Organisers
INSERT INTO dbo.Users (FullName, Email, PasswordHash, Role) VALUES
('Thabo Nkosi',         'thabo.nkosi@raceday.co.za', 'HASHED_PASSWORD_1', 'Organiser'),
('Sarah van der Merwe', 'sarah.vdm@raceday.co.za',   'HASHED_PASSWORD_2', 'Organiser');

-- Participants
INSERT INTO dbo.Users (FullName, Email, PasswordHash, Role) VALUES
('Lindiwe Dlamini', 'lindiwe.d@example.com',   'HASHED_PASSWORD_3', 'Participant'),
('James Botha',     'james.botha@example.com', 'HASHED_PASSWORD_4', 'Participant');

-- Events (now with DistanceKM and EventType)
INSERT INTO dbo.Events (OrganiserID, EventName, Description, EventDate, Location, DistanceKM, EventType) VALUES
(1, 'Johannesburg City Marathon', 'Annual road marathon through the Johannesburg CBD', '2026-11-08', 'Johannesburg, Gauteng',  42.20, 'Run'),
(1, 'Soweto Fun Run',             'Community charity walk and fun run',                 '2026-10-04', 'Soweto, Gauteng',         10.00, 'Walk'),
(2, 'Cape Town Cycle Challenge',  'Scenic coastal cycling event',                       '2026-11-22', 'Cape Town, Western Cape', 50.00, 'Cycle');

-- Categories
INSERT INTO dbo.Categories (EventID, CategoryName, DistanceKM, MaxParticipants, EntryFee) VALUES
(1, '10km Run',              10.0, 500,  150.00),
(1, 'Half Marathon (21km)',  21.1, 500,  250.00),
(1, 'Full Marathon (42km)',  42.2, 300,  350.00),
(2, '5km Fun Walk',           5.0, 1000,  50.00),
(2, '10km Run',              10.0, 500,  100.00),
(3, '50km Cycle Race',       50.0, 400,  300.00);

-- Routes (one per category)
INSERT INTO dbo.Routes (CategoryID, StartPoint, EndPoint, MapURL) VALUES
(1, 'Sandton City',   'FNB Stadium',          'https://maps.example.com/route1'),
(2, 'Sandton City',   'FNB Stadium',          'https://maps.example.com/route2'),
(3, 'Sandton City',   'FNB Stadium',          'https://maps.example.com/route3'),
(4, 'Soweto Theatre', 'Walter Sisulu Square', 'https://maps.example.com/route4'),
(5, 'Soweto Theatre', 'Walter Sisulu Square', 'https://maps.example.com/route5'),
(6, 'V&A Waterfront', 'Camps Bay',            'https://maps.example.com/route6');

-- Enrolments
INSERT INTO dbo.Enrolments (ParticipantID, CategoryID, EnrolmentDate, Status) VALUES
(3, 2, '2026-09-01', 'Registered'),
(3, 4, '2026-09-01', 'Registered'),
(4, 1, '2026-09-02', 'Registered'),
(4, 6, '2026-09-02', 'Registered');

-- Results (only for enrolments where the event has already taken place, for demonstration)
INSERT INTO dbo.Results (EnrolmentID, FinishTime, Position, Status) VALUES
(1, '01:52:34', 45, 'Finished'),
(3, '00:48:12', 12, 'Finished');
GO
