#requires -Version 7.2
[CmdletBinding()]
param([string]$DockerEndpoint)
$ErrorActionPreference = 'Stop'
$taskRepo = Split-Path -Parent $PSScriptRoot
$taskLocal = Join-Path $taskRepo 'deploy/local'
$taskDockerConfig = Join-Path $taskRepo '.local/docker-client'
New-Item -ItemType Directory -Path $taskDockerConfig -Force | Out-Null
# Use an empty project client config; do not read personal registry credentials.
$taskDockerArgs = @('--config', $taskDockerConfig)
if (-not $DockerEndpoint -and $IsWindows) { $DockerEndpoint = 'npipe:////./pipe/dockerDesktopLinuxEngine' }
if ($DockerEndpoint) { $taskDockerArgs += @('--host', $DockerEndpoint) }
$taskServer = & docker @taskDockerArgs version --format '{{.Server.Os}}' 2>$null
if ($LASTEXITCODE -ne 0 -or $taskServer -ne 'linux') {
    throw 'DOCKER_UNAVAILABLE: a Linux Docker engine must be accessible from this session. No services started.'
}
if (-not (Test-Path -LiteralPath (Join-Path $taskLocal '.env'))) {
    throw 'LOCAL_ENV_MISSING: run eng/Initialize-LocalEnvironment.ps1 first.'
}
$taskLicenseLine = Get-Content -LiteralPath (Join-Path $taskLocal '.env') | Where-Object { $_ -match '^CCAI_ACCEPT_SQL_EULA=' }
if ($taskLicenseLine -cne 'CCAI_ACCEPT_SQL_EULA=Y') {
    throw 'SQL_DEVELOPER_TERMS: review deploy/local/README.md and record acceptance in the local .env before starting SQL.'
}
& docker @taskDockerArgs compose --env-file (Join-Path $taskLocal '.env') -f (Join-Path $taskLocal 'compose.yaml') config --quiet
if ($LASTEXITCODE -ne 0) { throw 'COMPOSE_CONFIG_INVALID: no services started.' }
& docker @taskDockerArgs compose --env-file (Join-Path $taskLocal '.env') -f (Join-Path $taskLocal 'compose.yaml') up -d --wait --wait-timeout 180
if ($LASTEXITCODE -ne 0) { throw 'COMPOSE_START_FAILED: inspect local Docker service status; keep credentials private.' }
Write-Output 'Containers started; SQL health check passed. Keycloak discovery must still pass before B01 closes.'
