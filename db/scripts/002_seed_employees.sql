/*
    002_seed_employees.sql
    ----------------------------------------------------------------------------
    Seeds mock employees (Hebrew names and addresses) and their phone numbers
    (E.164) for manual / DBA provisioning. Run after 001_create_schema.sql.

        sqlcmd -S <server> -d SmsManagement -f 65001 -i db/scripts/002_seed_employees.sql

    This file is UTF-8 (with BOM). Always pass -f 65001 to sqlcmd, or Hebrew text
    may be read with the client's ANSI code page and corrupted. All literals use
    the N'' prefix so they are sent as Unicode.

    The application performs the same seeding automatically on startup
    (SmsManagement.Infrastructure/Persistence/Seeding/EmployeeSeedData.cs).
    Keep both in sync; the unit test SeedScriptConsistencyTests enforces it.

    Idempotent: inserts only employees (by natural key FirstName + LastName +
    Address) and phone numbers that do not exist yet. Never updates or deletes.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

-- Table variables live in tempdb and would otherwise take the SERVER collation. DATABASE_DEFAULT makes them
-- follow the database collation (Hebrew_100_CI_AS_SC), avoiding collation-conflict errors in the joins below.
DECLARE @Seed TABLE
(
    FirstName   nvarchar(100) COLLATE DATABASE_DEFAULT NOT NULL,
    LastName    nvarchar(100) COLLATE DATABASE_DEFAULT NOT NULL,
    Address     nvarchar(255) COLLATE DATABASE_DEFAULT NOT NULL,
    PhoneNumber nvarchar(20)  COLLATE DATABASE_DEFAULT NOT NULL
);

INSERT INTO @Seed (FirstName, LastName, Address, PhoneNumber)
VALUES
    -- "Israel Israeli" from the specification, with two phone numbers.
    (N'ישראל', N'ישראלי', N'דרך המכבים 68, ראשון לציון', N'+972501234567'),
    (N'ישראל', N'ישראלי', N'דרך המכבים 68, ראשון לציון', N'+972521234567'),
    -- Same name, different address: a distinct employee under the composite natural key.
    (N'ישראל', N'ישראלי', N'ויצמן 40, כפר סבא', N'+972509876543'),
    (N'שרה', N'כהן', N'הרצל 12, רחובות', N'+972541112233'),
    (N'דוד', N'לוי', N'השקמה 5, בית דגן', N'+972502223344'),
    -- Shared office line: phone numbers are not globally unique.
    (N'דוד', N'לוי', N'השקמה 5, בית דגן', N'+97239683400'),
    (N'מרים', N'מזרחי', N'שדרות רוטשילד 8, תל אביב-יפו', N'+972533334455'),
    (N'יוסף', N'פרץ', N'בן גוריון 22, באר שבע', N'+972584445566'),
    (N'רחל', N'ביטון', N'הנשיא 3, חיפה', N'+972525556677'),
    (N'רחל', N'ביטון', N'הנשיא 3, חיפה', N'+97239683400'),
    (N'אברהם', N'פרידמן', N'יפו 17, ירושלים', N'+972546667788'),
    (N'נועה', N'שפירא', N'הדקל 9, גילת', N'+972507778899');

INSERT INTO [dbo].[Employees] ([FirstName], [LastName], [Address])
SELECT DISTINCT s.FirstName, s.LastName, s.Address
FROM @Seed AS s
WHERE NOT EXISTS
(
    SELECT 1
    FROM [dbo].[Employees] AS e
    WHERE e.[FirstName] = s.FirstName
      AND e.[LastName]  = s.LastName
      AND e.[Address]   = s.Address
);

INSERT INTO [dbo].[EmployeePhones] ([EmployeeId], [PhoneNumber])
SELECT e.[Id], s.PhoneNumber
FROM @Seed AS s
INNER JOIN [dbo].[Employees] AS e
    ON  e.[FirstName] = s.FirstName
    AND e.[LastName]  = s.LastName
    AND e.[Address]   = s.Address
WHERE NOT EXISTS
(
    SELECT 1
    FROM [dbo].[EmployeePhones] AS p
    WHERE p.[EmployeeId]  = e.[Id]
      AND p.[PhoneNumber] = s.PhoneNumber
);

COMMIT TRANSACTION;
GO
