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
    WHERE [MigrationId] = N'20261101235928_InitialCreate'
)
BEGIN
    CREATE TABLE [Users] (
        [Id] uniqueidentifier NOT NULL,
        [DisplayName] nvarchar(120) NOT NULL,
        [Education] nvarchar(500) NOT NULL,
        [ExperienceSummary] nvarchar(2000) NOT NULL,
        [CreatedUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_Users] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101235928_InitialCreate'
)
BEGIN
    CREATE TABLE [Companies] (
        [Id] uniqueidentifier NOT NULL,
        [UserProfileId] uniqueidentifier NOT NULL,
        [Name] nvarchar(160) NOT NULL,
        [NormalizedName] nvarchar(160) NOT NULL,
        [Details] nvarchar(2000) NOT NULL,
        [WebsiteUrl] nvarchar(500) NOT NULL,
        CONSTRAINT [PK_Companies] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Companies_Users_UserProfileId] FOREIGN KEY ([UserProfileId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101235928_InitialCreate'
)
BEGIN
    CREATE TABLE [Skills] (
        [Id] uniqueidentifier NOT NULL,
        [UserProfileId] uniqueidentifier NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [Proficiency] nvarchar(40) NOT NULL,
        CONSTRAINT [PK_Skills] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Skills_Users_UserProfileId] FOREIGN KEY ([UserProfileId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101235928_InitialCreate'
)
BEGIN
    CREATE TABLE [UserPositionPreferences] (
        [Id] uniqueidentifier NOT NULL,
        [UserProfileId] uniqueidentifier NOT NULL,
        [Title] nvarchar(120) NOT NULL,
        [PreferenceType] nvarchar(30) NOT NULL,
        CONSTRAINT [PK_UserPositionPreferences] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_UserPositionPreferences_Users_UserProfileId] FOREIGN KEY ([UserProfileId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101235928_InitialCreate'
)
BEGIN
    CREATE TABLE [UserWorkPreferences] (
        [Id] uniqueidentifier NOT NULL,
        [UserProfileId] uniqueidentifier NOT NULL,
        [Preference] nvarchar(160) NOT NULL,
        CONSTRAINT [PK_UserWorkPreferences] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_UserWorkPreferences_Users_UserProfileId] FOREIGN KEY ([UserProfileId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101235928_InitialCreate'
)
BEGIN
    CREATE TABLE [Offers] (
        [Id] uniqueidentifier NOT NULL,
        [CompanyId] uniqueidentifier NULL,
        [Title] nvarchar(160) NOT NULL,
        [Description] nvarchar(max) NOT NULL,
        [Requirements] nvarchar(max) NOT NULL,
        [Location] nvarchar(200) NOT NULL,
        [WorkMode] nvarchar(80) NOT NULL,
        [Salary] nvarchar(120) NOT NULL,
        [SourceName] nvarchar(100) NOT NULL,
        [SourceExternalId] nvarchar(200) NOT NULL,
        [SourceUrl] nvarchar(1000) NOT NULL,
        [PublishedDate] date NULL,
        [CreatedUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_Offers] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Offers_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE SET NULL
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101235928_InitialCreate'
)
BEGIN
    CREATE TABLE [Applications] (
        [Id] uniqueidentifier NOT NULL,
        [UserProfileId] uniqueidentifier NOT NULL,
        [CompanyId] uniqueidentifier NOT NULL,
        [JobOfferId] uniqueidentifier NULL,
        [Position] nvarchar(160) NOT NULL,
        [ApplicationDate] date NOT NULL,
        [ResponseDate] date NULL,
        [Status] nvarchar(30) NOT NULL,
        [WorkMode] nvarchar(120) NOT NULL,
        [Salary] nvarchar(120) NOT NULL,
        [JobUrl] nvarchar(1000) NOT NULL,
        [JobDetails] nvarchar(max) NOT NULL,
        [Notes] nvarchar(4000) NOT NULL,
        [CreatedUtc] datetime2 NOT NULL,
        [UpdatedUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_Applications] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Applications_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Applications_Offers_JobOfferId] FOREIGN KEY ([JobOfferId]) REFERENCES [Offers] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_Applications_Users_UserProfileId] FOREIGN KEY ([UserProfileId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101235928_InitialCreate'
)
BEGIN
    CREATE TABLE [Documents] (
        [Id] uniqueidentifier NOT NULL,
        [ApplicationId] uniqueidentifier NOT NULL,
        [DocumentType] nvarchar(30) NOT NULL,
        [FileName] nvarchar(255) NOT NULL,
        [ContentType] nvarchar(120) NOT NULL,
        [SizeBytes] bigint NOT NULL,
        [Content] varbinary(max) NOT NULL,
        [UploadedUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_Documents] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Documents_Applications_ApplicationId] FOREIGN KEY ([ApplicationId]) REFERENCES [Applications] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101235928_InitialCreate'
)
BEGIN
    CREATE TABLE [History] (
        [Id] uniqueidentifier NOT NULL,
        [ApplicationId] uniqueidentifier NOT NULL,
        [EventType] nvarchar(40) NOT NULL,
        [Description] nvarchar(1000) NOT NULL,
        [OccurredUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_History] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_History_Applications_ApplicationId] FOREIGN KEY ([ApplicationId]) REFERENCES [Applications] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101235928_InitialCreate'
)
BEGIN
    CREATE TABLE [Interviews] (
        [Id] uniqueidentifier NOT NULL,
        [ApplicationId] uniqueidentifier NOT NULL,
        [ScheduledUtc] datetimeoffset NOT NULL,
        [InterviewType] nvarchar(100) NOT NULL,
        [LocationOrLink] nvarchar(1000) NOT NULL,
        [ContactName] nvarchar(160) NOT NULL,
        [Notes] nvarchar(2000) NOT NULL,
        CONSTRAINT [PK_Interviews] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Interviews_Applications_ApplicationId] FOREIGN KEY ([ApplicationId]) REFERENCES [Applications] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101235928_InitialCreate'
)
BEGIN
    CREATE TABLE [Reminders] (
        [Id] uniqueidentifier NOT NULL,
        [ApplicationId] uniqueidentifier NOT NULL,
        [DueDate] date NOT NULL,
        [Note] nvarchar(1000) NOT NULL,
        [IsCompleted] bit NOT NULL,
        [CreatedUtc] datetime2 NOT NULL,
        [CompletedUtc] datetime2 NULL,
        CONSTRAINT [PK_Reminders] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Reminders_Applications_ApplicationId] FOREIGN KEY ([ApplicationId]) REFERENCES [Applications] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101235928_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedUtc', N'DisplayName', N'Education', N'ExperienceSummary') AND [object_id] = OBJECT_ID(N'[Users]'))
        SET IDENTITY_INSERT [Users] ON;
    EXEC(N'INSERT INTO [Users] ([Id], [CreatedUtc], [DisplayName], [Education], [ExperienceSummary])
    VALUES (''a85dba15-fb20-4bdb-92b8-f8a0a13c4e1b'', ''2026-01-01T00:00:00.0000000Z'', N''Mi perfil'', N''Tecnicatura en Desarrollo de Software'', N''Sin experiencia profesional en desarrollo; programas de formación y proyectos académicos.'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedUtc', N'DisplayName', N'Education', N'ExperienceSummary') AND [object_id] = OBJECT_ID(N'[Users]'))
        SET IDENTITY_INSERT [Users] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101235928_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'PreferenceType', N'Title', N'UserProfileId') AND [object_id] = OBJECT_ID(N'[UserPositionPreferences]'))
        SET IDENTITY_INSERT [UserPositionPreferences] ON;
    EXEC(N'INSERT INTO [UserPositionPreferences] ([Id], [PreferenceType], [Title], [UserProfileId])
    VALUES (''0914b6a2-f70e-41c0-96d2-8b1420cf818f'', N''Principal'', N''Desarrolladora Web Junior'', ''a85dba15-fb20-4bdb-92b8-f8a0a13c4e1b''),
    (''3cf6c65c-aa64-4e5e-86af-f9cd325befaa'', N''Alternativo'', N''QA Tester Junior'', ''a85dba15-fb20-4bdb-92b8-f8a0a13c4e1b''),
    (''4ba281d1-67ba-4522-a9a8-d240cbd7170a'', N''Principal'', N''.NET Trainee / Junior'', ''a85dba15-fb20-4bdb-92b8-f8a0a13c4e1b''),
    (''50ebc4f6-e96d-46e9-a205-8244fdc476ca'', N''Alternativo'', N''Help Desk'', ''a85dba15-fb20-4bdb-92b8-f8a0a13c4e1b''),
    (''81622e2e-2449-40ef-ae3d-81469f19a29b'', N''Alternativo'', N''Analista funcional Junior'', ''a85dba15-fb20-4bdb-92b8-f8a0a13c4e1b''),
    (''91f940e7-a341-4d60-90b0-7ee813f2ec78'', N''Alternativo'', N''Soporte Técnico IT'', ''a85dba15-fb20-4bdb-92b8-f8a0a13c4e1b''),
    (''98357990-14de-4e06-9d4c-4c12e04c28f5'', N''Principal'', N''Desarrolladora Backend Junior'', ''a85dba15-fb20-4bdb-92b8-f8a0a13c4e1b''),
    (''db61a6a4-6e0d-4c86-a4ab-d5c3478f7cf4'', N''Principal'', N''Programadora C#'', ''a85dba15-fb20-4bdb-92b8-f8a0a13c4e1b'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'PreferenceType', N'Title', N'UserProfileId') AND [object_id] = OBJECT_ID(N'[UserPositionPreferences]'))
        SET IDENTITY_INSERT [UserPositionPreferences] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101235928_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Preference', N'UserProfileId') AND [object_id] = OBJECT_ID(N'[UserWorkPreferences]'))
        SET IDENTITY_INSERT [UserWorkPreferences] ON;
    EXEC(N'INSERT INTO [UserWorkPreferences] ([Id], [Preference], [UserProfileId])
    VALUES (''4501c04b-7926-43a8-93c0-aa8df27c82d2'', N''Remoto'', ''a85dba15-fb20-4bdb-92b8-f8a0a13c4e1b''),
    (''b4d8560e-3c28-4bd0-8ca7-a1ab21c71c17'', N''Híbrido'', ''a85dba15-fb20-4bdb-92b8-f8a0a13c4e1b''),
    (''c6cf8e5e-940a-4a81-900a-c66a482fe016'', N''Presencial en Zona Sur / Buenos Aires'', ''a85dba15-fb20-4bdb-92b8-f8a0a13c4e1b'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Preference', N'UserProfileId') AND [object_id] = OBJECT_ID(N'[UserWorkPreferences]'))
        SET IDENTITY_INSERT [UserWorkPreferences] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101235928_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Applications_CompanyId] ON [Applications] ([CompanyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101235928_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Applications_JobOfferId] ON [Applications] ([JobOfferId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101235928_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Applications_UserProfileId_ApplicationDate] ON [Applications] ([UserProfileId], [ApplicationDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101235928_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Applications_UserProfileId_JobOfferId] ON [Applications] ([UserProfileId], [JobOfferId]) WHERE [JobOfferId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101235928_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Companies_UserProfileId_NormalizedName] ON [Companies] ([UserProfileId], [NormalizedName]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101235928_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Documents_ApplicationId] ON [Documents] ([ApplicationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101235928_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_History_ApplicationId_OccurredUtc] ON [History] ([ApplicationId], [OccurredUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101235928_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Interviews_ApplicationId] ON [Interviews] ([ApplicationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101235928_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Offers_CompanyId] ON [Offers] ([CompanyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101235928_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Offers_SourceName_SourceExternalId] ON [Offers] ([SourceName], [SourceExternalId]) WHERE [SourceExternalId] <> ''''');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101235928_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Reminders_ApplicationId_IsCompleted_DueDate] ON [Reminders] ([ApplicationId], [IsCompleted], [DueDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101235928_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Skills_UserProfileId] ON [Skills] ([UserProfileId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101235928_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_UserPositionPreferences_UserProfileId] ON [UserPositionPreferences] ([UserProfileId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101235928_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_UserWorkPreferences_UserProfileId] ON [UserWorkPreferences] ([UserProfileId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101235928_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261101235928_InitialCreate', N'10.0.9');
END;

COMMIT;
GO
