BEGIN;
DO $$
DECLARE relation_name text;
BEGIN
  FOREACH relation_name IN ARRAY ARRAY['CaseFiles','AuditEntries','ProtectedCaseContents','AccessAccounts','SuperuserAccounts','InstitutionalAuthorities','PersonnelPeople','PersonnelAssignments','AdministratorDesignations'] LOOP
    IF to_regclass(format('public.%I', relation_name)) IS NULL THEN
      RAISE EXCEPTION 'Missing table: %', relation_name;
    END IF;
  END LOOP;
  IF (SELECT count(*) FROM "__EFMigrationsHistory") <> 5 THEN
    RAISE EXCEPTION 'Expected all five migrations';
  END IF;
END $$;
INSERT INTO "CaseFiles" ("Id", "Number", "Status") VALUES ('00000000-0000-0000-0000-000000000123','TEST-RETENTION','Created');
DO $$
DECLARE command text;
BEGIN
  FOREACH command IN ARRAY ARRAY[
    'DELETE FROM "CaseFiles"', 'TRUNCATE "CaseFiles" CASCADE',
    'DELETE FROM "ProtectedCaseContents"', 'UPDATE "ProtectedCaseContents" SET "Ciphertext" = ''tampered''',
    'TRUNCATE "ProtectedCaseContents"', 'DELETE FROM "AuditEntries"',
    'UPDATE "AuditEntries" SET "Outcome" = ''tampered''', 'TRUNCATE "AuditEntries"'
  ] LOOP
    BEGIN
      EXECUTE command;
      RAISE EXCEPTION 'Protection missing: %', command;
    EXCEPTION WHEN check_violation THEN NULL;
    END;
  END LOOP;
END $$;
UPDATE "CaseFiles" SET "Status" = 'InProgress' WHERE "Number" = 'TEST-RETENTION';
ROLLBACK;
