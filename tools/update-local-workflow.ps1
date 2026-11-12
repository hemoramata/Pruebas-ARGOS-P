[CmdletBinding()]
param([string]$Database = 'ARGOS', [string]$Username = 'postgres',
    [ValidateRange(1,65535)][int]$Port = 5432,
    [string]$PostgresBin = 'C:\Program Files\PostgreSQL\18\bin')

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$apiRoot = Join-Path $root 'src\ARGOS.API'
$release = Join-Path $root 'obj\procedure-ui-release\app'
$sql = Join-Path $root 'deploy\postgresql\workflow-schema-idempotent.sql'
$oldPgPassword = $env:PGPASSWORD
$oldPgEncoding = $env:PGCLIENTENCODING
$oldConnection = $env:ConnectionStrings__DefaultConnection
$oldEnvironment = $env:ASPNETCORE_ENVIRONMENT
$secret = $null
try {
    $env:PGCLIENTENCODING = 'UTF8'
    if (!(Test-Path (Join-Path $release 'ARGOS.API.dll'))) { throw 'Primero prepare la compilación procedure-ui-release.' }
    foreach ($requiredFile in @($sql, (Join-Path $PostgresBin 'psql.exe'), (Join-Path $PostgresBin 'pg_dump.exe'), (Join-Path $PostgresBin 'pg_restore.exe'))) {
        if (!(Test-Path -LiteralPath $requiredFile -PathType Leaf)) { throw "No existe el archivo requerido: $requiredFile" }
    }
    $secret = Read-Host 'Contraseña PostgreSQL (solo se utiliza en esta sesión)' -AsSecureString
    $pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secret)
    try { $env:PGPASSWORD = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer) }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer) }
    $pgArgs = @('-h', 'localhost', '-p', "$Port", '-U', $Username, '-d', $Database, '-w')
    & (Join-Path $PostgresBin 'psql.exe') @pgArgs -X -v ON_ERROR_STOP=1 -c 'SELECT 1' | Out-Null
    if ($LASTEXITCODE) { throw 'No fue posible validar la conexión PostgreSQL.' }
    $backupDir = Join-Path $root 'BackUp'
    New-Item -ItemType Directory -Force -Path $backupDir | Out-Null
    $backup = Join-Path $backupDir ('before-workflow-' + [guid]::NewGuid().ToString('N') + '.dump')
    & (Join-Path $PostgresBin 'pg_dump.exe') @pgArgs -Fc -f $backup
    if ($LASTEXITCODE) { throw 'Falló el respaldo; no se aplicará la actualización.' }
    & (Join-Path $PostgresBin 'pg_restore.exe') --list $backup | Out-Null
    if ($LASTEXITCODE) { throw 'No se pudo leer el respaldo; no se aplicará la actualización.' }
    & (Join-Path $PostgresBin 'psql.exe') @pgArgs -X -v ON_ERROR_STOP=1 -f $sql
    if ($LASTEXITCODE) { throw 'Falló la actualización del esquema; revise el diagnóstico.' }
    Write-Host "Respaldo conservado en $backup"
    # Stop only the known executable from this project's previous Release build.
    $expected = Join-Path $apiRoot 'bin\Release\net10.0\ARGOS.API.exe'
    Get-Process ARGOS.API -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $expected } | Stop-Process
    $priorPublishedDll = Join-Path $root 'obj\workflow-release\app\ARGOS.API.dll'
    $currentPublishedDll = Join-Path $release 'ARGOS.API.dll'
    $knownDlls = @($priorPublishedDll, (Join-Path $root 'obj\workflow-runs-release\app\ARGOS.API.dll'),
        (Join-Path $root 'obj\audit-release\app\ARGOS.API.dll'),
        (Join-Path $root 'obj\audit-hardening-release\app\ARGOS.API.dll'),
        (Join-Path $root 'obj\institutional-authorization-release\app\ARGOS.API.dll'), $currentPublishedDll)
    foreach ($taskProcess in (Get-CimInstance Win32_Process -Filter "Name = 'dotnet.exe'")) {
        foreach ($knownDll in $knownDlls) {
            if ($taskProcess.CommandLine -and $taskProcess.CommandLine.Contains('"' + $knownDll + '"')) {
                Stop-Process -Id $taskProcess.ProcessId -ErrorAction Stop
                Wait-Process -Id $taskProcess.ProcessId -Timeout 10 -ErrorAction SilentlyContinue
                break
            }
        }
    }
    $connection = [System.Data.Common.DbConnectionStringBuilder]::new()
    $connection['Host'] = 'localhost'
    $connection['Port'] = $Port
    $connection['Database'] = $Database
    $connection['Username'] = $Username
    $connection['Password'] = $env:PGPASSWORD
    $env:ConnectionStrings__DefaultConnection = $connection.ConnectionString
    $connection.Clear()
    $env:PGPASSWORD = $oldPgPassword
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    Write-Host 'Iniciando la compilación actualizada en https://localhost:7020'
    & dotnet (Join-Path $release 'ARGOS.API.dll') --contentRoot $apiRoot --urls 'https://localhost:7020;http://localhost:5189'
    if ($LASTEXITCODE) { throw 'La aplicación no pudo iniciar; el respaldo se conserva.' }
}
finally {
    $env:PGCLIENTENCODING = $oldPgEncoding
    $env:PGPASSWORD = $oldPgPassword
    $env:ConnectionStrings__DefaultConnection = $oldConnection
    $env:ASPNETCORE_ENVIRONMENT = $oldEnvironment
    if ($null -ne $secret) { $secret.Dispose() }
}
