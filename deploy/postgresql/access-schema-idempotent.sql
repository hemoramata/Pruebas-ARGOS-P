CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260911115117_InitialAccessPostgres') THEN
    CREATE TABLE "AccessAccounts" (
        "PersonId" uuid NOT NULL,
        "Identification" character varying(100) NOT NULL,
        "Salt" bytea NOT NULL,
        "PasswordDigest" bytea NOT NULL,
        "Channel" integer NOT NULL,
        "Destination" text NOT NULL,
        "VerificationReference" text NOT NULL,
        "Enabled" boolean NOT NULL,
        "MustChangePassword" boolean NOT NULL,
        "FailedAttempts" integer NOT NULL,
        "LockedUntilTicks" bigint NOT NULL,
        CONSTRAINT "PK_AccessAccounts" PRIMARY KEY ("PersonId")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260911115117_InitialAccessPostgres') THEN
    CREATE TABLE "AccessPositionPermissions" (
        "OrganizationId" uuid NOT NULL,
        "PositionId" uuid NOT NULL,
        "Permission" character varying(100) NOT NULL,
        CONSTRAINT "PK_AccessPositionPermissions" PRIMARY KEY ("OrganizationId", "PositionId", "Permission")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260911115117_InitialAccessPostgres') THEN
    CREATE TABLE "InstitutionalAuthorities" (
        "Id" uuid NOT NULL,
        "OrganizationId" uuid NOT NULL,
        "FullName" character varying(200) NOT NULL,
        "Email" character varying(254) NOT NULL,
        "VerificationReference" character varying(200) NOT NULL,
        "Enabled" boolean NOT NULL,
        CONSTRAINT "PK_InstitutionalAuthorities" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260911115117_InitialAccessPostgres') THEN
    CREATE TABLE "SensitiveDecisions" (
        "Id" uuid NOT NULL,
        "OrganizationId" uuid NOT NULL,
        "OperationCode" character varying(100) NOT NULL,
        "SubjectType" character varying(100) NOT NULL,
        "SubjectReference" character varying(200) NOT NULL,
        "InitiatedBy" uuid NOT NULL,
        "InitiatedAt" timestamp with time zone NOT NULL,
        "Status" integer NOT NULL,
        "AuthorizedBy" uuid,
        "AuthorizedAt" timestamp with time zone,
        "VerificationReference" character varying(200),
        "AuthorizationReference" character varying(200),
        "ExecutedAt" timestamp with time zone,
        "ExecutionReference" character varying(200),
        "RejectionReference" character varying(200),
        CONSTRAINT "PK_SensitiveDecisions" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260911115117_InitialAccessPostgres') THEN
    CREATE TABLE "SuperuserAccounts" (
        "PersonId" uuid NOT NULL,
        "OrganizationId" uuid NOT NULL,
        "Identification" character varying(100) NOT NULL,
        "FullName" character varying(200) NOT NULL,
        "ExternalEmail" character varying(254) NOT NULL,
        "EmailVerificationReference" character varying(200) NOT NULL,
        "Enabled" boolean NOT NULL,
        CONSTRAINT "PK_SuperuserAccounts" PRIMARY KEY ("PersonId")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260911115117_InitialAccessPostgres') THEN
    CREATE UNIQUE INDEX "IX_AccessAccounts_Identification" ON "AccessAccounts" ("Identification");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260911115117_InitialAccessPostgres') THEN
    CREATE INDEX "IX_SensitiveDecisions_OrganizationId_Status_InitiatedAt" ON "SensitiveDecisions" ("OrganizationId", "Status", "InitiatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260911115117_InitialAccessPostgres') THEN
    CREATE UNIQUE INDEX "IX_SuperuserAccounts_OrganizationId_Identification" ON "SuperuserAccounts" ("OrganizationId", "Identification");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260911115117_InitialAccessPostgres') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260911115117_InitialAccessPostgres', '10.0.11');
    END IF;
END $EF$;
COMMIT;

