[CmdletBinding()]
param([string]$HostName='localhost', [int]$Port=5432, [string]$Database='ARGOS',
    [Alias('UserName')][string]$User='postgres',
    [string]$PostgresBin='C:\Program Files\PostgreSQL\18\bin')
$ErrorActionPreference='Stop'
# Shared secure prompt and read-only connection; Windows PowerShell 5.1 compatible.
& (Join-Path $PSScriptRoot 'inspect-postgres-schema.ps1') -HostName $HostName -Port $Port -Database $Database -UserName $User -PostgresBin $PostgresBin -VerifyComplete
