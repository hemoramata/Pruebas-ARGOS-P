[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidatePattern('^ARGOS-[A-Za-z0-9-]+$')][string]$ServiceName,
    [Parameter(Mandatory)][string]$PublishedDirectory,
    [Parameter(Mandatory)][string]$InstallDirectory,
    [Parameter(Mandatory)][string]$CertificatePath,
    [Parameter(Mandatory)][uri]$HttpsUrl
)
$ErrorActionPreference = 'Stop'
$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'La instalación requiere PowerShell como administrador. No se ha modificado nada.'
}
if ($HttpsUrl.Scheme -ne 'https') { throw 'Se requiere HTTPS.' }
if (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue) { throw 'El servicio ya existe; no se sobrescribe.' }
$source = (Resolve-Path -LiteralPath $PublishedDirectory).Path
$cert = (Resolve-Path -LiteralPath $CertificatePath).Path
if (-not (Test-Path -LiteralPath (Join-Path $source 'ARGOS.API.exe'))) { throw 'Use una publicación win-x64 autónoma de ARGOS.API.' }
if (-not [IO.Path]::IsPathRooted($InstallDirectory)) { throw 'Use una ruta absoluta nueva.' }
$target = [IO.Path]::GetFullPath($InstallDirectory).TrimEnd('\')
if (Test-Path -LiteralPath $target) { throw 'El destino ya existe; no se sobrescribe ninguna instalación.' }
if ($target.Contains('"') -or $target.StartsWith('\\')) { throw 'Use una ruta local válida.' }

# Secrets are entered interactively, never passed in process arguments or written as JSON.
$dbSecret = Read-Host 'Cadena PostgreSQL completa (Host=...;Database=...;Username=...;Password=...)' -AsSecureString
$certSecret = Read-Host 'Contraseña del certificado PFX del servidor' -AsSecureString
$sender = Read-Host 'Correo remitente institucional'
$mailSecret = Read-Host 'Contraseña de aplicación del correo' -AsSecureString
function Reveal-ForConfiguration([Security.SecureString]$Value) {
    $pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($Value)
    try { [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer) }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer) }
}
$db = Reveal-ForConfiguration $dbSecret
if (-not $db.StartsWith('Host=', [StringComparison]::OrdinalIgnoreCase)) { throw 'Cadena PostgreSQL inválida.' }
$certPassword = Reveal-ForConfiguration $certSecret
$certificate = New-Object Security.Cryptography.X509Certificates.X509Certificate2($cert, $certPassword, ([Security.Cryptography.X509Certificates.X509KeyStorageFlags]::EphemeralKeySet))
try {
    if (-not $certificate.HasPrivateKey -or $certificate.NotAfter -le [DateTime]::Now -or $certificate.NotBefore -gt [DateTime]::Now) {
        throw 'El certificado debe tener clave privada y estar vigente.'
    }
    if ($certificate.Extensions | Where-Object { $_.Oid.Value -eq '1.3.6.1.4.1.311.84.1.1' }) {
        throw 'No se admite el certificado de desarrollo de ASP.NET para un servicio.'
    }
} finally { $certificate.Dispose() }

# Create a restricted root BEFORE copying binary files or credentials.
New-Item -ItemType Directory -Path $target | Out-Null
& icacls.exe $target /inheritance:r /grant:r '*S-1-5-18:(OI)(CI)F' '*S-1-5-32-544:(OI)(CI)F' | Out-Null
if ($LASTEXITCODE) { throw 'No se pudo proteger el directorio; instalación detenida.' }
$app = Join-Path $target 'app'
$state = Join-Path $target 'state'
New-Item -ItemType Directory -Path $app, $state | Out-Null
Get-ChildItem -LiteralPath $source -Force | Copy-Item -Destination $app -Recurse
$installedCert = Join-Path $state 'server.pfx'
Copy-Item -LiteralPath $cert -Destination $installedCert
$values = @{
    'ConnectionStrings:DefaultConnection' = $db
    'Kestrel:Endpoints:Https:Url' = $HttpsUrl.AbsoluteUri.TrimEnd('/')
    'Kestrel:Endpoints:Https:Certificate:Path' = $installedCert
    'Kestrel:Endpoints:Https:Certificate:Password' = $certPassword
    'Gmail:Sender' = $sender
    'Gmail:AppPassword' = (Reveal-ForConfiguration $mailSecret)
}
Add-Type -AssemblyName System.Security
$bytes = [Text.Encoding]::UTF8.GetBytes(($values | ConvertTo-Json -Compress))
$config = Join-Path $state 'server.dpapi'
try {
    $cipher = [Security.Cryptography.ProtectedData]::Protect($bytes, $null, [Security.Cryptography.DataProtectionScope]::LocalMachine)
    [IO.File]::WriteAllBytes($config, $cipher)
} finally {
    [Array]::Clear($bytes, 0, $bytes.Length)
    $values.Clear(); $db = $null; $certPassword = $null
    $dbSecret.Dispose(); $certSecret.Dispose(); $mailSecret.Dispose()
}
$binary = '"' + (Join-Path $app 'ARGOS.API.exe') + '" --contentRoot "' + $app + '" --environment Production --service-config "' + $config + '"'
# Remain manual until every security and recovery setting succeeds.
New-Service -Name $ServiceName -BinaryPathName $binary -StartupType Manual -DisplayName $ServiceName | Out-Null
& sc.exe config $ServiceName obj= "NT SERVICE\$ServiceName" | Out-Null
if ($LASTEXITCODE) { throw 'No se pudo asignar la identidad de servicio. No se ha iniciado.' }
& icacls.exe $target /grant "NT SERVICE\${ServiceName}:(RX)" | Out-Null
if ($LASTEXITCODE) { throw 'No se pudo asignar acceso al directorio.' }
& icacls.exe $app /grant "NT SERVICE\${ServiceName}:(OI)(CI)RX" | Out-Null
if ($LASTEXITCODE) { throw 'No se pudo asignar lectura de la aplicación.' }
& icacls.exe $state /grant "NT SERVICE\${ServiceName}:(OI)(CI)M" | Out-Null
if ($LASTEXITCODE) { throw 'No se pudo asignar acceso al estado protegido.' }
& sc.exe failure $ServiceName reset= 86400 actions= restart/60000/restart/120000/restart/300000 | Out-Null
if ($LASTEXITCODE) { throw 'No se pudo configurar recuperación automática.' }
Set-Service -Name $ServiceName -StartupType Automatic
Write-Host "Servicio $ServiceName instalado, todavía detenido. No se ejecutaron migraciones ni se inició otro servidor."
Write-Host 'Revise el certificado, permisos, base elegida y puertos antes de iniciarlo con Start-Service.'
