BEGIN;
SELECT pg_advisory_xact_lock(73139013);
SET LOCAL lock_timeout = '10s';
CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260911060400_InitialPersonnelPostgres') THEN
    CREATE TABLE "PersonnelPeople" (
        "Id" uuid NOT NULL,
        "OrganizationId" uuid NOT NULL,
        "Identity" text NOT NULL,
        "FullName" character varying(200) NOT NULL,
        "Revision" bigint NOT NULL,
        CONSTRAINT "PK_PersonnelPeople" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260911060400_InitialPersonnelPostgres') THEN
    CREATE TABLE "PersonnelAssignments" (
        "Id" uuid NOT NULL,
        "PersonId" uuid NOT NULL,
        "PositionId" uuid NOT NULL,
        "StartsAt" timestamp with time zone NOT NULL,
        "EndsAt" timestamp with time zone,
        "AuthorizationReference" text NOT NULL,
        "RevokedAt" timestamp with time zone,
        "RevocationAuthorizationReference" text,
        CONSTRAINT "PK_PersonnelAssignments" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_PersonnelAssignments_PersonnelPeople_PersonId" FOREIGN KEY ("PersonId") REFERENCES "PersonnelPeople" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260911060400_InitialPersonnelPostgres') THEN
    CREATE INDEX "IX_PersonnelAssignments_PersonId" ON "PersonnelAssignments" ("PersonId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260911060400_InitialPersonnelPostgres') THEN
    CREATE UNIQUE INDEX "IX_PersonnelPeople_OrganizationId_Identity" ON "PersonnelPeople" ("OrganizationId", "Identity");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260911060400_InitialPersonnelPostgres') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260911060400_InitialPersonnelPostgres', '10.0.11');
    END IF;
END $EF$;


CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);


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


CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260911115135_InitialDesignationPostgres') THEN
    CREATE TABLE "AdministratorDesignations" (
        "Id" uuid NOT NULL,
        "OrganizationId" uuid NOT NULL,
        "PersonId" uuid NOT NULL,
        "AssignmentId" uuid NOT NULL,
        "AuthorizationReference" character varying(200) NOT NULL,
        "AuthorizedBy" character varying(200) NOT NULL,
        "RecordedBy" character varying(200) NOT NULL,
        "RecordedAt" timestamp with time zone NOT NULL,
        "StartsAt" timestamp with time zone NOT NULL,
        "EndsAt" timestamp with time zone,
        "RevokedAt" timestamp with time zone,
        "RevocationReference" character varying(200),
        CONSTRAINT "PK_AdministratorDesignations" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260911115135_InitialDesignationPostgres') THEN
    CREATE INDEX "IX_AdministratorDesignations_OrganizationId_PersonId_Assignmen~" ON "AdministratorDesignations" ("OrganizationId", "PersonId", "AssignmentId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260911115135_InitialDesignationPostgres') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260911115135_InitialDesignationPostgres', '10.0.11');
    END IF;
END $EF$;



DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260913000100_ProtectRetainedRecords') THEN
    CREATE OR REPLACE FUNCTION argos_reject_retained_mutation() RETURNS trigger
    LANGUAGE plpgsql AS $$
    BEGIN
        RAISE EXCEPTION 'ARGOS retained records cannot be erased or overwritten'
            USING ERRCODE = '23514';
    END;
    $$;
    CREATE TRIGGER argos_case_retention BEFORE DELETE OR TRUNCATE ON "CaseFiles"
        FOR EACH STATEMENT EXECUTE FUNCTION argos_reject_retained_mutation();
    CREATE TRIGGER argos_content_retention BEFORE UPDATE OR DELETE OR TRUNCATE ON "ProtectedCaseContents"
        FOR EACH STATEMENT EXECUTE FUNCTION argos_reject_retained_mutation();
    CREATE TRIGGER argos_audit_retention BEFORE UPDATE OR DELETE OR TRUNCATE ON "AuditEntries"
        FOR EACH STATEMENT EXECUTE FUNCTION argos_reject_retained_mutation();
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260913000100_ProtectRetainedRecords') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260913000100_ProtectRetainedRecords', '10.0.11');
    END IF;
END $EF$;


COMMIT;