[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$taskRepo = Split-Path -Parent $PSScriptRoot
$taskPrefix = 'CCAI_SYNTHETIC_USER_PASSWORD='
$taskLines = @(Get-Content -LiteralPath (Join-Path $taskRepo 'deploy/local/.env') | Where-Object { $_.StartsWith($taskPrefix, [StringComparison]::Ordinal) })
if ($taskLines.Count -ne 1) { throw 'SYNTHETIC_PASSWORD_SETTING_INVALID' }
$taskPassword = $taskLines[0].Substring($taskPrefix.Length)
if ($taskPassword.Length -lt 32) { throw 'SYNTHETIC_PASSWORD_SETTING_INVALID' }
Set-Clipboard -Value $taskPassword
Write-Output 'Contraseña sintética copiada. Vuelve a Keycloak, pega con Ctrl+V en Password y pulsa Sign In.'
