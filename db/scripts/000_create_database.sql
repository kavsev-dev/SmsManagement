/*
    000_create_database.sql
    ----------------------------------------------------------------------------
    Creates the SmsManagement database with the Hebrew collation. Run first,
    connected to [master], before 001_create_schema.sql:

        sqlcmd -S <server> -d master -f 65001 -i db/scripts/000_create_database.sql

    Collation Hebrew_100_CI_AS_SC is set at DATABASE level only. Every text
    column created by 001_create_schema.sql inherits it (no column overrides):
      Hebrew_100  newest Hebrew collation version in SQL Server 2022
      CI          case-insensitive (Latin text mixed with Hebrew)
      AS          accent-sensitive (text with niqqud is distinct from text without)
      SC          supplementary characters (emoji) handled as single characters

    Text columns are nvarchar (UTF-16), so Hebrew is stored losslessly; the
    collation governs sorting and comparison.

    When the application starts and the database does not exist, EF Core
    creates it the same way (CREATE DATABASE ... COLLATE Hebrew_100_CI_AS_SC).

    Idempotent: does nothing if the database already exists with the expected
    collation. Fails if it exists with a different one, because the tables
    would inherit the wrong collation.
*/
SET NOCOUNT ON;

IF DB_ID(N'SmsManagement') IS NULL
BEGIN
    CREATE DATABASE [SmsManagement] COLLATE Hebrew_100_CI_AS_SC;
    PRINT N'Database [SmsManagement] created with collation Hebrew_100_CI_AS_SC.';
END
ELSE
BEGIN
    -- Read the collation from inside the database: from master, DATABASEPROPERTYEX and sys.databases return
    -- NULL while an AUTO_CLOSE database (the default on Express / LocalDB) is closed. Executing in its context
    -- opens it first.
    DECLARE @current nvarchar(128);
    EXEC [SmsManagement].sys.sp_executesql
        N'SELECT @collation = CONVERT(nvarchar(128), DATABASEPROPERTYEX(DB_NAME(), N''Collation''));',
        N'@collation nvarchar(128) OUTPUT',
        @collation = @current OUTPUT;

    IF @current = N'Hebrew_100_CI_AS_SC'
        PRINT N'Database [SmsManagement] already exists with collation Hebrew_100_CI_AS_SC.';
    ELSE
        RAISERROR(
            N'Database [SmsManagement] already exists with collation %s; expected Hebrew_100_CI_AS_SC. Tables created in it would inherit the wrong collation. While the database is still empty, fix it with: ALTER DATABASE [SmsManagement] COLLATE Hebrew_100_CI_AS_SC;',
            16, 1, @current);
END
GO
