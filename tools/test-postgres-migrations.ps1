[CmdletBinding()]
param([string]$PostgresBin = 'C:\Program Files\PostgreSQL\18\bin', [int]$Port = 55439,
    [string]$SchemaBackup, [switch]$GenerateBaseline, [switch]$IntegrationTests,
    [ValidateSet('Debug','Release')][string]$Configuration = 'Release', [string]$TestFilter)
$ErrorActionPreference = 'Stop'
# PostgreSQL deliberately returns nonzero for several negative validation
# fixtures below; those are checked explicitly with $LASTEXITCODE.
$PSNativeCommandUseErrorActionPreference = $false
$root = Split-Path $PSScriptRoot -Parent
$run = Join-Path $root ('obj\pg-validation-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $run | Out-Null
$data = Join-Path $run 'data'
$started = $false
$priorTestConnection = $env:ARGOS_TEST_POSTGRES_ADMIN
function Invoke-TemporarySql([string]$Database, [string]$Name, [string]$Sql) {
    $file = Join-Path $run ($Name + '.sql')
    [IO.File]::WriteAllText($file, $Sql)
    & "$PostgresBin\psql.exe" -X -w -h 127.0.0.1 -p $Port -U argos_test -d $Database -v ON_ERROR_STOP=1 -f $file
    if ($LASTEXITCODE) { throw "$Name failed" }
}
# Disposable local test instance only; no institutional connection or credentials.
try {
    & "$PostgresBin\initdb.exe" -D $data -U argos_test -A trust --encoding=UTF8 --locale=C > (Join-Path $run 'init.log')
    if ($LASTEXITCODE) { throw 'initdb failed' }
    & "$PostgresBin\pg_ctl.exe" -D $data -l (Join-Path $run 'server.log') -o "-h 127.0.0.1 -p $Port" -w start
    if ($LASTEXITCODE) { throw 'Temporary PostgreSQL start failed' }
    $started = $true
    & "$PostgresBin\createdb.exe" -w -h 127.0.0.1 -p $Port -U argos_test legacy_test
    if ($LASTEXITCODE) { throw 'Legacy fixture database failed' }
    & "$PostgresBin\psql.exe" -X -w -h 127.0.0.1 -p $Port -U argos_test -d legacy_test -v ON_ERROR_STOP=1 -f (Join-Path $PSScriptRoot 'fixtures\legacy-main-schema.sql') > (Join-Path $run 'legacy-setup.log')
    if ($LASTEXITCODE) { throw 'Legacy fixture schema failed' }
    $legacySeed = Join-Path $run 'legacy-seed.sql'
    @'
DROP TABLE IF EXISTS "__EFMigrationsHistory";
INSERT INTO "CaseFiles" ("Id","Number","Status") VALUES ('00000000-0000-0000-0000-000000000321','PRESERVE-ME','Created');
'@ | Set-Content -LiteralPath $legacySeed -NoNewline
    & "$PostgresBin\psql.exe" -X -w -h 127.0.0.1 -p $Port -U argos_test -d legacy_test -v ON_ERROR_STOP=1 -f $legacySeed
    if ($LASTEXITCODE) { throw 'Legacy fixture seed failed' }
    foreach ($pass in 1..2) {
        & "$PostgresBin\psql.exe" -X -w -h 127.0.0.1 -p $Port -U argos_test -d legacy_test -v ON_ERROR_STOP=1 -f (Join-Path $root 'deploy\postgresql\repair-missing-contexts.sql') > (Join-Path $run "repair-$pass.log")
        if ($LASTEXITCODE) { throw 'Legacy repair failed' }
    }
    & "$PostgresBin\psql.exe" -X -w -h 127.0.0.1 -p $Port -U argos_test -d legacy_test -v ON_ERROR_STOP=1 -f (Join-Path $PSScriptRoot 'test-postgres-repair.sql')
    if ($LASTEXITCODE) { throw 'Legacy repair assertions failed' }
    $scripts = @('argos','personnel','access','designation')
    foreach ($pass in 1..2) {
        foreach ($name in $scripts) {
            & "$PostgresBin\psql.exe" -X -w -h 127.0.0.1 -p $Port -U argos_test -d postgres -v ON_ERROR_STOP=1 -f (Join-Path $root "deploy\postgresql\$name-schema-idempotent.sql") > (Join-Path $run "$name-$pass.log")
            if ($LASTEXITCODE) { throw "Migration $name failed on pass $pass" }
        }
    }
    & "$PostgresBin\psql.exe" -X -w -h 127.0.0.1 -p $Port -U argos_test -d postgres -v ON_ERROR_STOP=1 -f (Join-Path $PSScriptRoot 'test-postgres-retention.sql')
    if ($LASTEXITCODE) { throw 'Retention verification failed' }
    & "$PostgresBin\psql.exe" -X -w -h 127.0.0.1 -p $Port -U argos_test -d postgres -v ON_ERROR_STOP=1 -f (Join-Path $PSScriptRoot 'inspect-postgres-schema.sql') > (Join-Path $run 'inspection.log')
    if ($LASTEXITCODE) { throw 'Read-only schema inspection failed' }
    $reference = @(& "$PostgresBin\psql.exe" -X -w -h 127.0.0.1 -p $Port -U argos_test -d postgres -v ON_ERROR_STOP=1 -At -f (Join-Path $PSScriptRoot 'main-schema-signature.sql'))
    if ($LASTEXITCODE) { throw 'Reference schema failed' }
    $baselineSql = & (Join-Path $PSScriptRoot 'new-postgres-baseline-sql.ps1') -ExpectedSignatures $reference
    $baselineFile = Join-Path $run 'baseline-main.sql'
    [IO.File]::WriteAllText($baselineFile, $baselineSql)
    if ($GenerateBaseline) {
        [IO.File]::WriteAllText((Join-Path $root 'deploy\postgresql\baseline-main.sql'), $baselineSql)
    } elseif ([IO.File]::ReadAllText((Join-Path $root 'deploy\postgresql\baseline-main.sql')) -cne $baselineSql) {
        throw 'Checked-in baseline does not match the migrated reference schema.'
    }
    Invoke-TemporarySql 'legacy_test' 'drift-fixture' 'DROP INDEX "IX_Procedures_Code";'
    $nativeErrorPreference = $ErrorActionPreference; $ErrorActionPreference = 'Continue'
    & "$PostgresBin\psql.exe" -X -w -h 127.0.0.1 -p $Port -U argos_test -d legacy_test -v ON_ERROR_STOP=1 -f $baselineFile > (Join-Path $run 'baseline-rejected.log') 2>&1
    $ErrorActionPreference = $nativeErrorPreference
    if ($LASTEXITCODE -eq 0) { throw 'Baseline accepted a missing index' }
    & "$PostgresBin\psql.exe" -X -w -h 127.0.0.1 -p $Port -U argos_test -d legacy_test -v ON_ERROR_STOP=1 -f (Join-Path $PSScriptRoot 'test-postgres-repair.sql')
    if ($LASTEXITCODE) { throw 'Rejected baseline changed history or records' }
    Invoke-TemporarySql 'legacy_test' 'index-fixture-recovery' 'CREATE INDEX "IX_Procedures_Code" ON "Procedures" ("Code");'
    foreach ($pass in 1..2) {
        & "$PostgresBin\psql.exe" -X -w -h 127.0.0.1 -p $Port -U argos_test -d legacy_test -v ON_ERROR_STOP=1 -f $baselineFile > (Join-Path $run "baseline-$pass.log")
        if ($LASTEXITCODE) { throw 'Compatible baseline failed' }
    }
    Invoke-TemporarySql 'legacy_test' 'baseline-assertions' 'DO $$ BEGIN IF (SELECT count(*) FROM "__EFMigrationsHistory") <> 5 OR (SELECT count(*) FROM "CaseFiles" WHERE "Number"=''PRESERVE-ME'') <> 1 THEN RAISE EXCEPTION ''Baseline assertions failed''; END IF; END $$;'
    Write-Output 'PASS: schema drift rejected atomically; matching baseline repeatable; existing record preserved.'
    & "$PostgresBin\psql.exe" -X -w -h 127.0.0.1 -p $Port -U argos_test -d legacy_test -v ON_ERROR_STOP=1 -f (Join-Path $PSScriptRoot 'verify-postgres-schema.sql')
    if ($LASTEXITCODE) { throw 'Complete schema verification failed' }
    Invoke-TemporarySql 'legacy_test' 'disable-trigger-fixture' 'ALTER TABLE "CaseFiles" DISABLE TRIGGER argos_case_retention;'
    $nativeErrorPreference = $ErrorActionPreference; $ErrorActionPreference = 'Continue'
    & "$PostgresBin\psql.exe" -X -w -h 127.0.0.1 -p $Port -U argos_test -d legacy_test -v ON_ERROR_STOP=1 -f (Join-Path $PSScriptRoot 'verify-postgres-schema.sql') > (Join-Path $run 'disabled-trigger-rejected.log') 2>&1
    $ErrorActionPreference = $nativeErrorPreference
    if ($LASTEXITCODE -eq 0) { throw 'Verifier accepted disabled retention' }
    Invoke-TemporarySql 'legacy_test' 'enable-trigger-fixture' 'ALTER TABLE "CaseFiles" ENABLE TRIGGER argos_case_retention;'
    if ($SchemaBackup) {
        # Restore schema ONLY: the trust-authenticated test instance must never receive real rows.
        & "$PostgresBin\createdb.exe" -w -h 127.0.0.1 -p $Port -U argos_test restored_schema
        if ($LASTEXITCODE) { throw 'Schema comparison database failed' }
        & "$PostgresBin\pg_restore.exe" -w -h 127.0.0.1 -p $Port -U argos_test -d restored_schema --schema-only --no-owner --no-privileges --exit-on-error $SchemaBackup > (Join-Path $run 'schema-restore.log')
        if ($LASTEXITCODE) { throw 'Backup schema restore failed' }
        $signatures = @{}
        foreach ($targetDb in @('postgres','restored_schema')) {
            $signatures[$targetDb] = @(& "$PostgresBin\psql.exe" -X -w -h 127.0.0.1 -p $Port -U argos_test -d $targetDb -v ON_ERROR_STOP=1 -At -f (Join-Path $PSScriptRoot 'main-schema-signature.sql'))
            if ($LASTEXITCODE) { throw 'Schema signature failed' }
        }
        $differences = @(Compare-Object $signatures['postgres'] $signatures['restored_schema'])
        $differences | Format-Table -AutoSize -Wrap | Out-String -Width 500 | Set-Content (Join-Path $run 'schema-differences.txt')
        if ($differences.Count) { throw "Main schema differs: see $run\schema-differences.txt. Do not baseline." }
        Write-Output 'PASS: backup main tables, columns, constraints and indexes match migrations. No real rows restored.'
    }
    Write-Output 'PASS: four contexts, repeat application, retention protections.'
    foreach ($pass in 1..2) {
        & "$PostgresBin\psql.exe" -X -w -h 127.0.0.1 -p $Port -U argos_test -d postgres -v ON_ERROR_STOP=1 -f (Join-Path $root 'deploy\postgresql\workflow-schema-idempotent.sql') > (Join-Path $run "workflow-$pass.log")
        if ($LASTEXITCODE) { throw "Workflow SQL deployment failed on pass $pass" }
    }
    Write-Output 'PASS: workflow SQL deployment and repeat application.'
    # Exercise a complete dump/restore with synthetic data only, never institutional records.
    Invoke-TemporarySql 'postgres' 'recovery-fixture' 'INSERT INTO "CaseFiles" ("Id","Number","Status") VALUES (''00000000-0000-0000-0000-000000000654'',''RESTORE-FIXTURE'',''Created'');'
    $recoveryDump = Join-Path $run 'synthetic-recovery.dump'
    & "$PostgresBin\pg_dump.exe" -w -h 127.0.0.1 -p $Port -U argos_test -d postgres -Fc -f $recoveryDump
    if ($LASTEXITCODE) { throw 'Synthetic backup failed' }
    & "$PostgresBin\createdb.exe" -w -h 127.0.0.1 -p $Port -U argos_test recovery_test
    if ($LASTEXITCODE) { throw 'Recovery database creation failed' }
    & "$PostgresBin\pg_restore.exe" -w -h 127.0.0.1 -p $Port -U argos_test -d recovery_test --no-owner --no-privileges --exit-on-error $recoveryDump
    if ($LASTEXITCODE) { throw 'Synthetic full restoration failed' }
    Invoke-TemporarySql 'recovery_test' 'recovery-assertions' 'DO $$ BEGIN IF (SELECT count(*) FROM "CaseFiles" WHERE "Number"=''RESTORE-FIXTURE'') <> 1 OR NOT EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId"=''20260917014136_InstitutionalOperationAuthorization'') OR NOT EXISTS (SELECT 1 FROM pg_trigger WHERE tgname=''argos_policy_retention'' AND tgenabled=''O'') OR (SELECT count(*) FROM pg_constraint WHERE conname IN (''FK_Evidence_ProtectedContent'',''FK_Case_ProcedureVersion'')) <> 2 THEN RAISE EXCEPTION ''Recovery assertions failed''; END IF; END $$;'
    Write-Output 'PASS: full synthetic backup restored; record, migration, retention trigger and foreign keys preserved.'
    if ($IntegrationTests) {
        $env:ARGOS_TEST_POSTGRES_ADMIN = "Host=127.0.0.1;Port=$Port;Database=postgres;Username=argos_test;Pooling=false"
        $filterArgs = @()
        if ($TestFilter) { $filterArgs = @('--filter', $TestFilter) }
        & dotnet test (Join-Path $root 'tests\ARGOS.IntegrationTests') --configuration $Configuration --logger "trx;LogFileName=integration.trx" --results-directory $run @filterArgs
        if ($LASTEXITCODE) { throw 'PostgreSQL integration tests failed' }
    }
}
finally {
    $env:ARGOS_TEST_POSTGRES_ADMIN = $priorTestConnection
    if ($started) { & "$PostgresBin\pg_ctl.exe" -D $data -m fast -w stop }
    Write-Output "Test logs: $run"
}
