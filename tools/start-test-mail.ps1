[CmdletBinding()]
param([string]$Username = 'postgres', [ValidateRange(1,65535)][int]$Port = 5432,
    [switch]$EnrollAdministrator, [switch]$UpdateMailCredential)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
. (Join-Path $PSScriptRoot 'test-mail-secret.ps1')
$app = Join-Path (Split-Path $root -Parent) 'ARGOS_PRUEBAS_20260917\app'
$currentApp = Join-Path (Split-Path $root -Parent) 'ARGOS_PRUEBAS_20260917\app-current'
$homeApp = Join-Path (Split-Path $root -Parent) 'ARGOS_PRUEBAS_20260917\app-home'
if (Test-Path -LiteralPath (Join-Path $homeApp 'ARGOS.API.dll')) { $currentApp = $homeApp }
if (Test-Path -LiteralPath (Join-Path $currentApp 'ARGOS.API.dll')) { $app = $currentApp }
if ($EnrollAdministrator -and !(Test-Path -LiteralPath (Join-Path $currentApp 'ARGOS.API.dll'))) {
    # Separate updated enrollment executable: no restart or overwrite of the running web API.
    $app = Join-Path (Split-Path $root -Parent) 'ARGOS_PRUEBAS_20260917\enrollment-app'
}
$saved = @{}
foreach ($key in @('ConnectionStrings__DefaultConnection','ASPNETCORE_ENVIRONMENT','Gmail__Sender','Gmail__AppPassword','ARGOS_KMS_TOKEN','ARGOS_KMS_URL','ARGOS_KMS_KEY','ARGOS_ENCRYPTION_PROVIDER')) {
    $saved[$key] = [Environment]::GetEnvironmentVariable($key,'Process')
}
$databaseSecret = $null; $mailSecret = $null
try {
    if (!(Test-Path -LiteralPath (Join-Path $app 'ARGOS.API.dll'))) { throw 'No existe la copia de pruebas instalada.' }
    if (!$EnrollAdministrator -and (Get-NetTCPConnection -State Listen -LocalPort 5193,7193 -ErrorAction SilentlyContinue)) {
        throw 'Detenga la API de PRUEBAS con Ctrl+C en su ventana PowerShell y repita. No detenga la original.'
    }
    $databaseSecret = Read-Host 'Contrasena de PostgreSQL' -AsSecureString
    $mailSecret = Get-ArgosTestMailSecret -Replace:$UpdateMailCredential
    $builder = [System.Data.Common.DbConnectionStringBuilder]::new()
    $builder['Host']='localhost'; $builder['Port']=$Port; $builder['Database']='ARGOS_PRUEBAS_20260917'; $builder['Username']=$Username
    $pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($databaseSecret)
    try { $builder['Password']=[Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer) }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer) }
    $env:ConnectionStrings__DefaultConnection=$builder.ConnectionString
    $builder.Clear()
    $pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($mailSecret)
    try { $env:Gmail__AppPassword=([Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer) -replace '\s','') }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer) }
    if ($env:Gmail__AppPassword.Length -ne 16) { throw 'La contrasena de aplicacion debe tener 16 caracteres, sin contar espacios.' }
    $env:Gmail__Sender='sistema.argosp@gmail.com'
    $env:ASPNETCORE_ENVIRONMENT='Development'
    foreach ($key in @('ARGOS_KMS_TOKEN','ARGOS_KMS_URL','ARGOS_KMS_KEY','ARGOS_ENCRYPTION_PROVIDER')) {
        [Environment]::SetEnvironmentVariable($key,$null,'Process')
    }
    if ($EnrollAdministrator) {
        Write-Host 'Alta interactiva SOLO en ARGOS_PRUEBAS_20260917. No crea un superusuario de los creadores.'
        & dotnet (Join-Path $app 'ARGOS.API.dll') --contentRoot $app --enroll-administrator true
        if ($LASTEXITCODE) { throw 'No se completo el alta de pruebas. Revise el mensaje de ARGOS.' }
        return
    }
    Write-Host 'ARGOS de PRUEBAS: https://localhost:7193/login/'
    Write-Host 'Remitente: sistema.argosp@gmail.com. Los codigos se envian al correo registrado de cada usuario.'
    Write-Host 'Gmail: credencial cifrada en el perfil de Windows. PostgreSQL: solo en memoria. No se envia ningun correo al arrancar.'
    & dotnet (Join-Path $app 'ARGOS.API.dll') --contentRoot $app --urls 'https://localhost:7193;http://localhost:5193'
    if ($LASTEXITCODE) { throw 'La API no pudo iniciar. No se modifico la base original.' }
}
finally {
    foreach ($key in $saved.Keys) { [Environment]::SetEnvironmentVariable($key,$saved[$key],'Process') }
    if ($null -ne $databaseSecret) { $databaseSecret.Dispose() }
    if ($null -ne $mailSecret) { $mailSecret.Dispose() }
}
