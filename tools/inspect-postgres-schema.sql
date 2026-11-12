BEGIN READ ONLY;
SELECT current_database() AS database, current_user AS inspector,
       current_setting('transaction_read_only') AS read_only;

-- Only schema metadata: no expedient, account or credential contents.
SELECT e.name AS expected_table,
       CASE WHEN c.oid IS NULL THEN 'MISSING' ELSE 'PRESENT' END AS state
FROM unnest(ARRAY['CaseFiles','AuditEntries','ProtectedCaseContents',
 'Evidences','EvidenceSources','CaseFileEvidenceLinks','Organizations','OrganizationSetups',
 'Procedures','PersonnelPeople','PersonnelAssignments','AccessAccounts',
 'AccessPositionPermissions','SensitiveDecisions','SuperuserAccounts',
 'InstitutionalAuthorities','AdministratorDesignations','__EFMigrationsHistory']) AS e(name)
LEFT JOIN pg_class c ON c.oid = to_regclass(format('public.%I', e.name))
ORDER BY e.name;

SELECT table_name, column_name, data_type, is_nullable, character_maximum_length
FROM information_schema.columns WHERE table_schema = 'public'
ORDER BY table_name, ordinal_position;

SELECT c.relname AS table_name, t.tgname, t.tgenabled,
       pg_get_triggerdef(t.oid) AS definition
FROM pg_trigger t JOIN pg_class c ON c.oid = t.tgrelid
JOIN pg_namespace n ON n.oid = c.relnamespace
WHERE n.nspname = 'public' AND NOT t.tgisinternal
ORDER BY c.relname, t.tgname;

SELECT c.relname AS table_name, con.conname, pg_get_constraintdef(con.oid) AS definition
FROM pg_constraint con JOIN pg_class c ON c.oid = con.conrelid
JOIN pg_namespace n ON n.oid = c.relnamespace
WHERE n.nspname = 'public' ORDER BY c.relname, con.conname;

SELECT rolname, rolsuper, rolcreatedb, rolcreaterole
FROM pg_roles WHERE rolname = current_user;
COMMIT;
