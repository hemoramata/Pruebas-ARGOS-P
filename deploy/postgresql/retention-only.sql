START TRANSACTION;

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

