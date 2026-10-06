#requires -Version 7.2
[CmdletBinding()]
param([switch]$NoBrowser,[switch]$Rebuild)
$ErrorActionPreference='Stop'
& (Join-Path $PSScriptRoot 'Prepare-Resort.ps1')
& (Join-Path $PSScriptRoot 'Start-Demo.ps1') -Resort -NoBrowser -Rebuild:$Rebuild -IntentProvider simulated
& (Join-Path $PSScriptRoot 'Restart-ChannelHost.ps1')
Write-Output 'Resort listo: http://127.0.0.1:7452/resort.html. Puedes cerrar esta terminal; los servicios siguen en segundo plano.'
if (-not $NoBrowser) { Start-Process 'http://127.0.0.1:7452/resort.html' }
