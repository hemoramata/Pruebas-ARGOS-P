BEGIN READ ONLY;
DO $$
BEGIN
 IF EXISTS (SELECT 1 FROM unnest(ARRAY['CaseFiles','AuditEntries','ProtectedCaseContents',
 'Evidences','EvidenceSources','CaseFileEvidenceLinks','Organizations','OrganizationSetups',
 'Procedures','PersonnelPeople','PersonnelAssignments','AccessAccounts',
 'AccessPositionPermissions','SensitiveDecisions','SuperuserAccounts',
 'InstitutionalAuthorities','AdministratorDesignations','__EFMigrationsHistory']) e(name)
 WHERE to_regclass(format('public.%I',e.name)) IS NULL) THEN
  RAISE EXCEPTION 'Required ARGOS tables missing';
 END IF;
 IF (SELECT count(*) FROM public."__EFMigrationsHistory" WHERE "MigrationId" IN (
 '20260911115057_InitialPostgres','20260911060400_InitialPersonnelPostgres',
 '20260911115117_InitialAccessPostgres','20260911115135_InitialDesignationPostgres',
 '20260913000100_ProtectRetainedRecords')) <> 5 THEN
  RAISE EXCEPTION 'Required migration history incomplete';
 END IF;
 IF EXISTS (
  SELECT 1 FROM (VALUES ('CaseFiles','argos_case_retention',42),
   ('ProtectedCaseContents','argos_content_retention',58),
   ('AuditEntries','argos_audit_retention',58)) e(tablename,triggername,eventmask)
  WHERE NOT EXISTS (SELECT 1 FROM pg_trigger t
   WHERE t.tgrelid=to_regclass(format('public.%I',e.tablename)) AND t.tgname=e.triggername
   AND NOT t.tgisinternal AND t.tgenabled IN ('O','A') AND t.tgtype=e.eventmask
   AND t.tgqual IS NULL AND t.tgnargs=0
   AND t.tgfoid=to_regprocedure('public.argos_reject_retained_mutation()'))
 ) THEN
  RAISE EXCEPTION 'Required retention triggers missing, disabled or misconfigured';
 END IF;
END $$;
SELECT 'PASS: required tables, migration history and retention trigger configuration' AS verification;
COMMIT;
