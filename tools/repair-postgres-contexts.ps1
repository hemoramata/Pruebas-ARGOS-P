[CmdletBinding()]
param([string]$HostName='localhost', [int]$Port=5432, [string]$Database='ARGOS',
    [string]$UserName='postgres', [string]$PostgresBin='C:\Program Files\PostgreSQL\18\bin',
    [switch]$CompleteBaseline)
$ErrorActionPreference='Stop'
$priorPassword=$env:PGPASSWORD
$priorTimeout=$env:PGCONNECT_TIMEOUT
$secret=$null
try {
    if ($env:ARGOS_DB_PASSWORD) { $env:PGPASSWORD=$env:ARGOS_DB_PASSWORD }
    elseif (-not $env:PGPASSWORD) {
        $secret=Read-Host 'Password PostgreSQL (entrada oculta)' -AsSecureString
        $credential=New-Object System.Management.Automation.PSCredential($UserName,$secret)
        $env:PGPASSWORD=$credential.GetNetworkCredential().Password
    }
    $env:PGCONNECT_TIMEOUT='5'
    $backupDir=Join-Path $env:LOCALAPPDATA ('ARGOS\Backups\'+[guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $backupDir -Force | Out-Null
    $backup=Join-Path $backupDir 'before-context-repair.dump'
    & "$PostgresBin\pg_dump.exe" -w -h $HostName -p $Port -U $UserName -d $Database -Fc -f $backup
    if ($LASTEXITCODE) { throw 'Backup failed. No repair applied.' }
    & "$PostgresBin\pg_restore.exe" --list $backup | Out-Null
    if ($LASTEXITCODE) { throw 'Backup archive unreadable. No repair applied.' }
    Write-Output "Backup local (contiene datos; no compartir): $backup"
    $sql=Join-Path (Split-Path $PSScriptRoot -Parent) 'deploy\postgresql\repair-missing-contexts.sql'
    & "$PostgresBin\psql.exe" -X -w -h $HostName -p $Port -U $UserName -d $Database -v ON_ERROR_STOP=1 -f $sql
    if ($LASTEXITCODE) { throw 'Repair rolled back. Retain the backup and report the error.' }
    if ($CompleteBaseline) {
        $baseline=Join-Path (Split-Path $PSScriptRoot -Parent) 'deploy\postgresql\baseline-main.sql'
        & "$PostgresBin\psql.exe" -X -w -h $HostName -p $Port -U $UserName -d $Database -v ON_ERROR_STOP=1 -f $baseline
        if ($LASTEXITCODE) { throw 'Context repair committed; main baseline refused or incomplete. Retain the backup and report the error.' }
    }
    & "$PostgresBin\psql.exe" -X -w -h $HostName -p $Port -U $UserName -d $Database -v ON_ERROR_STOP=1 -f (Join-Path $PSScriptRoot 'inspect-postgres-schema.sql')
    if ($LASTEXITCODE) { throw 'Repair committed; subsequent inspection failed.' }
    if ($CompleteBaseline) { Write-Output 'Repair and validated main migration baseline complete.' }
    else { Write-Output 'Repair complete. Main migration baseline remains pending.' }
}
finally {
    $env:PGPASSWORD=$priorPassword
    $env:PGCONNECT_TIMEOUT=$priorTimeout
    $credential=$null
    if($secret){$secret.Dispose()}
}
