$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$files = Get-ChildItem (Join-Path $root 'src\ARGOS.API\wwwroot') -Recurse -Filter '*.js'
foreach ($file in $files) {
    & node --check $file.FullName
    if ($LASTEXITCODE -ne 0) { throw "JavaScript inválido: $($file.FullName)" }
}
Write-Host "PASS: $($files.Count) archivos JavaScript válidos."
