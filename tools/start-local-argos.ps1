[CmdletBinding()]
param(
    [string]$Database = 'ARGOS',
    [string]$Username = 'postgres',
    [ValidateRange(1,65535)][int]$Port = 5432
)

$ErrorActionPreference = 'Stop'
$projectPath = Join-Path (Split-Path $PSScriptRoot -Parent) 'src\ARGOS.API\ARGOS.API.csproj'
$priorConnection = $env:ConnectionStrings__DefaultConnection
$secret = $null
$plainPassword = $null
$connection = $null
try {
    if ([string]::IsNullOrWhiteSpace($priorConnection)) {
        # The generic builder safely quotes semicolons and quotes in credentials.
        $connection = [System.Data.Common.DbConnectionStringBuilder]::new()
        $connection['Host'] = 'localhost'
        $connection['Port'] = $Port
        $connection['Database'] = $Database
        $connection['Username'] = $Username
        $secret = Read-Host "Contraseña PostgreSQL para $Username (base $Database)" -AsSecureString
        $pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secret)
        try { $plainPassword = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer) }
        finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer) }
        $connection['Password'] = $plainPassword
        $env:ConnectionStrings__DefaultConnection = $connection.ConnectionString
        $plainPassword = $null
        $connection.Clear()
    }
    Write-Host 'Iniciando ARGOS local en https://localhost:7020. Use Ctrl+C para detenerlo.'
    Write-Host 'La sesión requiere un certificado HTTPS de desarrollo confiable.'
    & dotnet run --project $projectPath --configuration Release --no-build --launch-profile https
    if ($LASTEXITCODE -ne 0) { throw 'ARGOS no pudo iniciar. Revise el diagnóstico anterior.' }
}
finally {
    $env:ConnectionStrings__DefaultConnection = $priorConnection
    $plainPassword = $null
    if ($null -ne $connection) { $connection.Clear() }
    if ($null -ne $secret) { $secret.Dispose() }
}
