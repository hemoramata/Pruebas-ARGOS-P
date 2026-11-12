DO $$ BEGIN
  IF (SELECT count(*) FROM "CaseFiles" WHERE "Number"='PRESERVE-ME' AND "Status"='Created') <> 1 THEN
    RAISE EXCEPTION 'Existing record changed';
  END IF;
  IF (SELECT count(*) FROM "__EFMigrationsHistory") <> 4 THEN
    RAISE EXCEPTION 'Wrong repair history';
  END IF;
  IF EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId"='20260911115057_InitialPostgres') THEN
    RAISE EXCEPTION 'Main migration was incorrectly baselined';
  END IF;
  PERFORM 1 FROM "PersonnelPeople";
  PERFORM 1 FROM "AccessAccounts";
  PERFORM 1 FROM "AdministratorDesignations";
END $$;
