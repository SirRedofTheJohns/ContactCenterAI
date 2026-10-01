#requires -Version 7.2
[CmdletBinding()]
param([switch]$Live, [string]$DotNetExe)
$ErrorActionPreference='Stop'
$taskRepo=Split-Path -Parent $PSScriptRoot
$taskWorkspace=Split-Path -Parent (Split-Path -Parent $taskRepo)
if (-not $DotNetExe) { $DotNetExe=Join-Path $taskWorkspace 'work/runtimes/dotnet/dotnet.exe' }
if (-not (Test-Path -LiteralPath $DotNetExe)) { $DotNetExe=(Get-Command dotnet -ErrorAction Stop).Source }
$taskPrevious=@{}
$taskEnvironment=@{DOTNET_CLI_HOME=(Join-Path $taskRepo '.local/cli');DOTNET_CLI_TELEMETRY_OPTOUT='1';DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1';DOTNET_GENERATE_ASPNET_CERTIFICATE='false';DOTNET_ADD_GLOBAL_TOOLS_TO_PATH='false';NUGET_PACKAGES=(Join-Path $taskRepo '.local/packages');NUGET_HTTP_CACHE_PATH=(Join-Path $taskRepo '.local/nuget-http')}
if ($IsWindows) { $taskEnvironment.APPDATA=Join-Path $taskRepo '.local/appdata' }
try {
    foreach ($taskName in $taskEnvironment.Keys) { $taskPrevious[$taskName]=[Environment]::GetEnvironmentVariable($taskName,'Process');[Environment]::SetEnvironmentVariable($taskName,$taskEnvironment[$taskName],'Process') }
    $taskArtifacts=Join-Path $taskRepo '.local/build-ai'
    $taskProject=Join-Path $taskRepo 'tests/ContactCenterAI.LocalAiChecks/ContactCenterAI.LocalAiChecks.csproj'
    $taskRestore=@('restore',$taskProject,'--artifacts-path',$taskArtifacts,'--configfile',(Join-Path $taskRepo 'NuGet.Config'),'--locked-mode')
    $taskFeed=Join-Path $taskWorkspace 'work/nuget-feed'
    if (Test-Path -LiteralPath $taskFeed) { $taskRestore+=@('--source',$taskFeed) }
    & $DotNetExe @taskRestore
    if ($LASTEXITCODE -ne 0) { throw 'LOCAL_AI_RESTORE_FAILED' }
    & $DotNetExe build $taskProject --artifacts-path $taskArtifacts --no-restore -c Release
    if ($LASTEXITCODE -ne 0) { throw 'LOCAL_AI_BUILD_FAILED' }
    $taskArgs=@((Join-Path $taskArtifacts 'bin/ContactCenterAI.LocalAiChecks/release/ContactCenterAI.LocalAiChecks.dll'),$taskRepo)
    if ($Live) { $taskArgs+='--live' }
    & $DotNetExe @taskArgs
    if ($LASTEXITCODE -ne 0) { throw 'LOCAL_AI_CHECK_FAILED' }
}
finally { foreach ($taskName in $taskPrevious.Keys) {[Environment]::SetEnvironmentVariable($taskName,$taskPrevious[$taskName],'Process')} }
