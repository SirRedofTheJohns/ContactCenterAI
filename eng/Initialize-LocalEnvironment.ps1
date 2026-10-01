#requires -Version 7.2
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$taskRepo = Split-Path -Parent $PSScriptRoot
$taskEnvFile = Join-Path $taskRepo 'deploy/local/.env'
if (Test-Path -LiteralPath $taskEnvFile) {
    Write-Output 'Local .env already exists; no credentials changed.'
    return
}
function New-TaskSecret {
    $taskBytes = [System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32)
    return 'Ccai!' + [Convert]::ToBase64String($taskBytes).TrimEnd('=').Replace('+', '-').Replace('/', '_')
}
$taskValues = @(
    'CCAI_ACCEPT_SQL_EULA=REVIEW_REQUIRED'
    ('CCAI_SQL_SA_PASSWORD=' + (New-TaskSecret))
    ('CCAI_KEYCLOAK_ADMIN_PASSWORD=' + (New-TaskSecret))
    ('CCAI_BFF_CLIENT_SECRET=' + (New-TaskSecret))
    ('CCAI_SOURCE_SERVICE_KEY=' + (New-TaskSecret))
    'CCAI_SYNTHETIC_USER_PASSWORD=123456Aa!'
)
[IO.File]::WriteAllLines($taskEnvFile, $taskValues, [Text.UTF8Encoding]::new($false))
Write-Output 'Random service credentials created in ignored deploy/local/.env; fictional demo users share 123456Aa!.'
Write-Output 'Review SQL Server Developer terms in deploy/local/README.md before starting SQL.'
