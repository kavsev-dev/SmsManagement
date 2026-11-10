IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109112546_InitialCreate'
)
BEGIN
    CREATE TABLE [Employees] (
        [Id] bigint NOT NULL IDENTITY,
        [FirstName] nvarchar(100) NOT NULL,
        [LastName] nvarchar(100) NOT NULL,
        [Address] nvarchar(255) NOT NULL,
        CONSTRAINT [PK_Employees] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109112546_InitialCreate'
)
BEGIN
    CREATE TABLE [Messages] (
        [Id] uniqueidentifier NOT NULL,
        [To] nvarchar(20) NOT NULL,
        [Body] nvarchar(160) NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [ProviderName] nvarchar(50) NOT NULL,
        [ErrorMessage] nvarchar(max) NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [SentAt] datetimeoffset NULL,
        CONSTRAINT [PK_Messages] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Messages_Status] CHECK ([Status] IN ('Pending', 'Sent', 'Failed'))
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109112546_InitialCreate'
)
BEGIN
    CREATE TABLE [EmployeePhones] (
        [Id] bigint NOT NULL IDENTITY,
        [EmployeeId] bigint NOT NULL,
        [PhoneNumber] nvarchar(20) NOT NULL,
        CONSTRAINT [PK_EmployeePhones] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_EmployeePhones_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109112546_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_EmployeePhones_PhoneNumber] ON [EmployeePhones] ([PhoneNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109112546_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_EmployeePhones_EmployeeId_PhoneNumber] ON [EmployeePhones] ([EmployeeId], [PhoneNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109112546_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_Employees_FirstName_LastName_Address] ON [Employees] ([FirstName], [LastName], [Address]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109112546_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Messages_CreatedAt] ON [Messages] ([CreatedAt] DESC);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109112546_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Messages_Status_CreatedAt] ON [Messages] ([Status], [CreatedAt] DESC);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109112546_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261109112546_InitialCreate', N'10.0.14');
END;

COMMIT;
GO

