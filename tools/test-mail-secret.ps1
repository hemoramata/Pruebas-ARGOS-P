# Windows DPAPI protects this credential for the current Windows user and machine.
function Get-ArgosTestMailSecret {
    param(
        [string]$Directory = (Join-Path $env:LOCALAPPDATA 'ARGOS\Credentials\ARGOS_PRUEBAS_20260917'),
        [switch]$Replace,
        [scriptblock]$Prompt = { Read-Host 'Contrasena DE APLICACION de sistema.argosp@gmail.com (no la clave normal)' -AsSecureString }
    )
    $path = Join-Path $Directory 'sistema.argosp.gmail.xml'
    $visualPath = Join-Path $Directory 'sistema.argosp.gmail.dpapi'
    Add-Type -AssemblyName System.Security
    if (!$Replace -and (Test-Path -LiteralPath $visualPath)) {
        try {
            $bytes = [System.Security.Cryptography.ProtectedData]::Unprotect([IO.File]::ReadAllBytes($visualPath), $null, [System.Security.Cryptography.DataProtectionScope]::CurrentUser)
            try {
                $value = [Text.Encoding]::UTF8.GetString($bytes)
                if ($value.Length -ne 16) { throw 'Invalid credential' }
                $result = ConvertTo-SecureString $value -AsPlainText -Force
                Write-Host 'Credencial recuperada del asistente visual de ARGOS.'
                return $result
            }
            finally { [Array]::Clear($bytes, 0, $bytes.Length); $value = $null }
        }
        catch { Write-Host 'No se pudo recuperar la credencial del asistente. Ingresela nuevamente.' }
    }
    if (!$Replace -and (Test-Path -LiteralPath $path)) {
        try {
            $credential = Import-Clixml -LiteralPath $path -ErrorAction Stop
            if ($credential -isnot [System.Management.Automation.PSCredential] -or
                $credential.UserName -ne 'sistema.argosp@gmail.com' -or $credential.Password.Length -ne 16) {
                throw 'Invalid credential'
            }
            Write-Host 'Credencial de Gmail recuperada del almacenamiento cifrado de Windows.'
            return $credential.Password
        }
        catch { Write-Host 'No se pudo recuperar la credencial guardada. Ingresela nuevamente para reemplazarla.' }
    }
    while ($true) {
        $secret = & $Prompt
        if ($secret -isnot [System.Security.SecureString]) { throw 'Se requiere una entrada segura.' }
        $pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secret)
        try { $plain = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer) -replace '\s','' }
        finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer); $secret.Dispose() }
        if ($plain.Length -eq 16) { break }
        $plain = $null
        Write-Host 'La contrasena de aplicacion debe tener 16 caracteres. Vuelva a ingresarla.'
    }
    try {
        $normalized = ConvertTo-SecureString -String $plain -AsPlainText -Force
        $null = New-Item -ItemType Directory -Path $Directory -Force
        $sid = [System.Security.Principal.WindowsIdentity]::GetCurrent().User
        $null = & icacls.exe $Directory /inheritance:r /grant:r ('*' + $sid.Value + ':(OI)(CI)F') '*S-1-5-18:(OI)(CI)F'
        if ($LASTEXITCODE -ne 0) { throw 'No se pudo proteger el directorio de credenciales.' }
        $credential = [System.Management.Automation.PSCredential]::new('sistema.argosp@gmail.com', $normalized)
        $credential | Export-Clixml -LiteralPath $path -ErrorAction Stop
        $bytes = [Text.Encoding]::UTF8.GetBytes($plain)
        try {
            $encrypted = [System.Security.Cryptography.ProtectedData]::Protect($bytes, $null, [System.Security.Cryptography.DataProtectionScope]::CurrentUser)
            [IO.File]::WriteAllBytes($visualPath + '.new', $encrypted)
            Move-Item -LiteralPath ($visualPath + '.new') -Destination $visualPath -Force
        }
        finally { [Array]::Clear($bytes, 0, $bytes.Length) }
        Write-Host 'Credencial de Gmail guardada cifrada para este usuario de Windows. No tendra que escribirla en cada inicio.'
        return $normalized
    }
    finally { $plain = $null }
}
