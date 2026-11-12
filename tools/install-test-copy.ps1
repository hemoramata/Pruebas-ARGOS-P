[CmdletBinding()]
param(
    [ValidatePattern('^[A-Za-z0-9_]+$')][string]$SourceDatabase = 'ARGOS',
    [ValidatePattern('^ARGOS_PRUEBAS_[A-Za-z0-9_]+$')][string]$TestDatabase = 'ARGOS_PRUEBAS_20260917',
    [string]$Username = 'postgres',
    [ValidateRange(1,65535)][int]$Port = 5432,
    [string]$PostgresBin = 'C:\Program Files\PostgreSQL\18\bin'
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$package = Join-Path $root 'obj\procedure-ui-release\app'
$copyRoot = Join-Path (Split-Path $root -Parent) $TestDatabase
$oldPassword = $env:PGPASSWORD
$oldEncoding = $env:PGCLIENTENCODING
$oldConnection = $env:ConnectionStrings__DefaultConnection
$oldEnvironment = $env:ASPNETCORE_ENVIRONMENT
$externalSettings = @{}
foreach ($key in @('Gmail__Sender','Gmail__AppPassword','ARGOS_KMS_TOKEN','ARGOS_KMS_URL','ARGOS_KMS_KEY','ARGOS_ENCRYPTION_PROVIDER')) {
    $externalSettings[$key] = [Environment]::GetEnvironmentVariable($key, 'Process')
}
$secret = $null
try {
    if ($SourceDatabase -eq $TestDatabase) { throw 'La base de pruebas debe ser distinta de la original.' }
    if ($TestDatabase.Length -gt 63 -or $SourceDatabase.Length -gt 63) { throw 'El nombre de base excede 63 caracteres.' }
    if (Test-Path -LiteralPath $copyRoot) { throw "La carpeta ya existe: $copyRoot. Se conserva sin cambios." }
    foreach ($file in @((Join-Path $package 'ARGOS.API.dll'), (Join-Path $root 'deploy\postgresql\workflow-schema-idempotent.sql'),
        (Join-Path $PostgresBin 'psql.exe'), (Join-Path $PostgresBin 'pg_dump.exe'), (Join-Path $PostgresBin 'pg_restore.exe'), (Join-Path $PostgresBin 'createdb.exe'))) {
        if (!(Test-Path -LiteralPath $file -PathType Leaf)) { throw "Falta un archivo requerido: $file" }
    }
    if (Get-NetTCPConnection -State Listen -LocalPort 5193,7193 -ErrorAction SilentlyContinue) { throw 'Los puertos 5193/7193 ya estan ocupados. No se detendra ningun servidor.' }
    $secret = Read-Host 'Contrasena PostgreSQL (entrada oculta, no se guarda)' -AsSecureString
    $pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secret)
    try { $env:PGPASSWORD = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer) }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer) }
    $env:PGCLIENTENCODING = 'UTF8'
    $pg = @('-X','-w','-h','localhost','-p',"$Port",'-U',$Username,'-v','ON_ERROR_STOP=1')
    & (Join-Path $PostgresBin 'psql.exe') @pg -d $SourceDatabase -c 'SELECT 1' | Out-Null
    if ($LASTEXITCODE) { throw 'No se pudo conectar a la base original. No se modifico nada.' }
    $exists = & (Join-Path $PostgresBin 'psql.exe') @pg -d postgres -At -c "SELECT count(*) FROM pg_database WHERE datname='$TestDatabase'"
    if ($LASTEXITCODE -or "$exists".Trim() -ne '0') { throw 'La base destino existe o no pudo verificarse. No se sobrescribira.' }
    New-Item -ItemType Directory -Path $copyRoot | Out-Null
    # A full backup can contain personal data. Restrict this new folder to the current user and SYSTEM.
    $acl = Get-Acl -LiteralPath $copyRoot
    $acl.SetAccessRuleProtection($true, $false)
    foreach ($sid in @([Security.Principal.WindowsIdentity]::GetCurrent().User, [Security.Principal.SecurityIdentifier]::new('S-1-5-18'))) {
        $rule = [Security.AccessControl.FileSystemAccessRule]::new($sid,'FullControl','ContainerInherit,ObjectInherit','None','Allow')
        $acl.AddAccessRule($rule)
    }
    Set-Acl -LiteralPath $copyRoot -AclObject $acl
    $backup = Join-Path $copyRoot 'original-before-test.dump'
    & (Join-Path $PostgresBin 'pg_dump.exe') -w -h localhost -p $Port -U $Username -d $SourceDatabase -Fc -f $backup
    if ($LASTEXITCODE) { throw 'Fallo el respaldo. La base original no fue modificada.' }
    & (Join-Path $PostgresBin 'pg_restore.exe') --list $backup | Out-Null
    if ($LASTEXITCODE) { throw 'No se pudo leer el respaldo; no se continuara.' }
    & (Join-Path $PostgresBin 'createdb.exe') -w -h localhost -p $Port -U $Username $TestDatabase
    if ($LASTEXITCODE) { throw 'No se pudo crear la base de pruebas.' }
    & (Join-Path $PostgresBin 'pg_restore.exe') -w -h localhost -p $Port -U $Username -d $TestDatabase --single-transaction --exit-on-error $backup
    if ($LASTEXITCODE) { throw 'Fallo la restauracion. Se conservan el respaldo y la base destino para diagnostico.' }
    & (Join-Path $PostgresBin 'psql.exe') @pg -d $TestDatabase -f (Join-Path $root 'deploy\postgresql\workflow-schema-idempotent.sql')
    if ($LASTEXITCODE) { throw 'Fallo la migracion de la copia. La base original no fue modificada.' }
    $app = Join-Path $copyRoot 'app'
    Copy-Item -LiteralPath $package -Destination $app -Recurse
    foreach ($source in (Get-ChildItem -LiteralPath $package -Recurse -File)) {
        $relative = $source.FullName.Substring($package.Length).TrimStart([char]92)
        if ((Get-FileHash -LiteralPath $source.FullName).Hash -ne (Get-FileHash -LiteralPath (Join-Path $app $relative)).Hash) { throw "La copia no coincide: $relative" }
    }
    $builder = [System.Data.Common.DbConnectionStringBuilder]::new()
    $builder['Host']='localhost'; $builder['Port']=$Port; $builder['Database']=$TestDatabase
    $builder['Username']=$Username; $builder['Password']=$env:PGPASSWORD
    $env:ConnectionStrings__DefaultConnection = $builder.ConnectionString
    $builder.Clear()
    $env:PGPASSWORD = $oldPassword
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    foreach ($key in $externalSettings.Keys) { [Environment]::SetEnvironmentVariable($key, $null, 'Process') }
    Write-Host "Copia verificada: $copyRoot"
    Write-Host "Base de pruebas: $TestDatabase. La original sigue intacta."
    Write-Host 'Abrir https://localhost:7193/procedures/ . Ctrl+C detiene solamente esta API.'
    Write-Host 'No se copiaron credenciales Gmail/KMS. Los servicios externos requieren configuracion de pruebas.'
    & dotnet (Join-Path $app 'ARGOS.API.dll') --contentRoot $app --urls 'https://localhost:7193;http://localhost:5193'
    if ($LASTEXITCODE) { throw 'La API de pruebas no pudo iniciar. Se conservaron copia y respaldo.' }
}
finally {
    $env:PGPASSWORD=$oldPassword; $env:PGCLIENTENCODING=$oldEncoding
    $env:ConnectionStrings__DefaultConnection=$oldConnection; $env:ASPNETCORE_ENVIRONMENT=$oldEnvironment
    foreach ($key in $externalSettings.Keys) { [Environment]::SetEnvironmentVariable($key, $externalSettings[$key], 'Process') }
    if ($null -ne $secret) { $secret.Dispose() }
}
