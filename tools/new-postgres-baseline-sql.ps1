param([Parameter(Mandatory=$true)][string[]]$ExpectedSignatures)
$ErrorActionPreference = 'Stop'
if ($ExpectedSignatures.Count -lt 100) { throw 'Incomplete reference schema.' }
$values = ($ExpectedSignatures | ForEach-Object { "('" + $_.Replace("'", "''") + "')" }) -join ",`n"
$query = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'main-schema-signature.sql')).Trim().TrimEnd(';')
$tables = ($ExpectedSignatures | ForEach-Object {
    $parts = $_ | ConvertFrom-Json
    if ($parts[0] -eq 'table') { 'public."' + $parts[1].Replace('"','""') + '"' }
}) -join ', '
@"
-- Generated from a clean PostgreSQL 18 migration schema. No business records are modified.
BEGIN;
SET LOCAL search_path = public, pg_catalog;
SET LOCAL lock_timeout = '10s';
SET LOCAL statement_timeout = '60s';
SELECT pg_advisory_xact_lock(73139013);
LOCK TABLE $tables, public."__EFMigrationsHistory" IN ACCESS EXCLUSIVE MODE;
DO `$baseline`$
BEGIN
 IF EXISTS (
  WITH actual AS ($query), expected(signature) AS (VALUES $values)
  (SELECT signature FROM actual EXCEPT ALL SELECT signature FROM expected)
  UNION ALL
  (SELECT signature FROM expected EXCEPT ALL SELECT signature FROM actual)
 ) THEN
  RAISE EXCEPTION 'Main schema differs from the validated migration. Baseline refused.';
 END IF;
 IF (SELECT count(*) FROM "__EFMigrationsHistory" WHERE "MigrationId" IN (
 '20260911060400_InitialPersonnelPostgres','20260911115117_InitialAccessPostgres',
 '20260911115135_InitialDesignationPostgres','20260913000100_ProtectRetainedRecords')) <> 4 THEN
  RAISE EXCEPTION 'Required context repair and retention migration history missing.';
 END IF;
 INSERT INTO "__EFMigrationsHistory" ("MigrationId","ProductVersion")
 VALUES ('20260911115057_InitialPostgres','10.0.11') ON CONFLICT ("MigrationId") DO NOTHING;
END `$baseline`$;
COMMIT;
"@
