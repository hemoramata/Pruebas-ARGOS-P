param(
    [string]$Output = (Join-Path $PSScriptRoot '..\artifacts\argos-linux-x64')
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'src\ARGOS.API\ARGOS.API.csproj'
$outputPath = [IO.Path]::GetFullPath($Output)
if (Test-Path -LiteralPath $outputPath) { throw "El destino ya existe: $outputPath" }
dotnet publish $project -c Release -r linux-x64 --self-contained true -o $outputPath
New-Item -ItemType Directory -Path (Join-Path $outputPath 'deploy') | Out-Null
Copy-Item (Join-Path $root 'deploy\linux\argos-api.service') (Join-Path $outputPath 'deploy\argos-api.service')
Copy-Item (Join-Path $root 'deploy\linux\argos.env.example') (Join-Path $outputPath 'deploy\argos.env.example')
Write-Host "Paquete Linux creado en: $outputPath"
