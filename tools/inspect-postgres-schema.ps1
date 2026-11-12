[CmdletBinding()]
param(
    [string]$HostName = 'localhost', [int]$Port = 5432,
    [string]$Database = 'ARGOS', [string]$UserName = 'postgres',
    [string]$PostgresBin = 'C:\Program Files\PostgreSQL\18\bin',
    [switch]$VerifyComplete
)
$ErrorActionPreference = 'Stop'
$psql = Join-Path $PostgresBin 'psql.exe'
if (-not (Test-Path -LiteralPath $psql)) { throw 'No se encontró psql.' }
$priorPassword = $env:PGPASSWORD
$priorOptions = $env:PGOPTIONS
$priorTimeout = $env:PGCONNECT_TIMEOUT
$secret = $null
try {
    if ($env:ARGOS_DB_PASSWORD) { $env:PGPASSWORD = $env:ARGOS_DB_PASSWORD }
    elseif (-not $env:PGPASSWORD) {
        $secret = Read-Host 'Contraseña PostgreSQL (entrada oculta)' -AsSecureString
        $credential = New-Object System.Management.Automation.PSCredential($UserName, $secret)
        $env:PGPASSWORD = $credential.GetNetworkCredential().Password
    }
    $env:PGOPTIONS = '-c default_transaction_read_only=on -c statement_timeout=15000'
    $env:PGCONNECT_TIMEOUT = '5'
    & $psql -X -w -h $HostName -p $Port -U $UserName -d $Database -v ON_ERROR_STOP=1 -f (Join-Path $PSScriptRoot 'inspect-postgres-schema.sql')
    if ($LASTEXITCODE -ne 0) { throw 'Inspección incompleta; revise el diagnóstico anterior.' }
    if ($VerifyComplete) {
        & $psql -X -w -h $HostName -p $Port -U $UserName -d $Database -v ON_ERROR_STOP=1 -f (Join-Path $PSScriptRoot 'verify-postgres-schema.sql')
        if ($LASTEXITCODE -ne 0) { throw 'Schema verification failed.' }
    }
}
finally {
    $env:PGPASSWORD = $priorPassword
    $env:PGOPTIONS = $priorOptions
    $env:PGCONNECT_TIMEOUT = $priorTimeout
    $credential = $null
    if ($secret) { $secret.Dispose() }
}
