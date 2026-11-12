CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

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
COMMIT;

