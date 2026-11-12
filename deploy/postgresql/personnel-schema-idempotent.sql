CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

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
COMMIT;

