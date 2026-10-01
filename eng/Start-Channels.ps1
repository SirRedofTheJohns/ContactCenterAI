#requires -Version 7.2
[CmdletBinding()]
param([switch]$PrepareOnly, [switch]$InspectTelegram, [switch]$Status, [string]$DotNetExe)
$ErrorActionPreference = 'Stop'
$taskRepo = Split-Path -Parent $PSScriptRoot
$taskWorkspace = Split-Path -Parent (Split-Path -Parent $taskRepo)
$taskConfig = Join-Path $taskRepo 'deploy/channels/.env'
$taskData = Join-Path $taskRepo '.local/channels'
function Restrict-ChannelPath([string]$taskPath, [bool]$taskDirectory) {
    if ((Get-Item -LiteralPath $taskPath).Attributes -band [System.IO.FileAttributes]::ReparsePoint) { throw 'CHANNEL_STORAGE_LINK_REJECTED' }
    if ($IsWindows) {
        $taskAcl = Get-Acl -LiteralPath $taskPath
        $taskAcl.SetAccessRuleProtection($true, $false)
        foreach ($taskRule in @($taskAcl.Access)) { [void]$taskAcl.RemoveAccessRuleSpecific($taskRule) }
        $taskSid = [System.Security.Principal.WindowsIdentity]::GetCurrent().User
        $taskInheritance = if ($taskDirectory) { [System.Security.AccessControl.InheritanceFlags]'ContainerInherit, ObjectInherit' } else { [System.Security.AccessControl.InheritanceFlags]::None }
        foreach ($taskAccount in @($taskSid, [System.Security.Principal.SecurityIdentifier]::new('S-1-5-18'))) {
            $taskRule = [System.Security.AccessControl.FileSystemAccessRule]::new($taskAccount, 'FullControl', $taskInheritance, 'None', 'Allow')
            $taskAcl.AddAccessRule($taskRule)
        }
        Set-Acl -LiteralPath $taskPath -AclObject $taskAcl
        $taskChecked = Get-Acl -LiteralPath $taskPath
        if (-not $taskChecked.AreAccessRulesProtected) { throw 'CHANNEL_STORAGE_RESTRICTION_FAILED' }
    } else {
        $taskMode = if ($taskDirectory) { [System.IO.UnixFileMode]'UserRead, UserWrite, UserExecute' } else { [System.IO.UnixFileMode]'UserRead, UserWrite' }
        [System.IO.File]::SetUnixFileMode($taskPath, $taskMode)
    }
}
New-Item -ItemType Directory -Force -Path $taskData | Out-Null
Restrict-ChannelPath $taskData $true
foreach ($taskExisting in Get-ChildItem -LiteralPath $taskData -File) { Restrict-ChannelPath $taskExisting.FullName $false }
if (-not (Test-Path -LiteralPath $taskConfig)) {
    Copy-Item -LiteralPath (Join-Path $taskRepo 'deploy/channels/.env.example') -Destination $taskConfig
    $taskContents = [System.IO.File]::ReadAllText($taskConfig)
    $taskVerify = [Convert]::ToHexString([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(24))
    [System.IO.File]::WriteAllText($taskConfig, $taskContents.Replace('CCAI_CHANNEL_META_VERIFY_TOKEN=', ('CCAI_CHANNEL_META_VERIFY_TOKEN=' + $taskVerify)))
}
Restrict-ChannelPath $taskConfig $false
if ($PrepareOnly) { Write-Host 'Configuración local preparada: deploy/channels/.env. Ambos canales siguen desactivados.'; return }
$taskPrevious = @{}
$taskAllowed = @('AI_MODE','TELEGRAM_ENABLED','TELEGRAM_ENDPOINT_ID','TELEGRAM_ACCESS_TOKEN','TELEGRAM_RECIPIENTS','META_ENABLED','META_ENDPOINT_ID','META_ACCESS_TOKEN','META_APP_SECRET','META_VERIFY_TOKEN','META_API_VERSION','META_RECIPIENTS','META_TEST_RESOURCES_CONFIRMED') | ForEach-Object { 'CCAI_CHANNEL_' + $_ }
try {
    foreach ($taskLine in [System.IO.File]::ReadAllLines($taskConfig)) {
        if (-not $taskLine.Trim() -or $taskLine.TrimStart().StartsWith('#')) { continue }
        if ($taskLine -notmatch '^([A-Z_]+)=(.*)$' -or $Matches[1] -notin $taskAllowed) { throw 'CHANNEL_CONFIG_FORMAT_REJECTED' }
        $taskName = $Matches[1]; $taskValue = $Matches[2]
        if (-not $taskPrevious.ContainsKey($taskName)) { $taskPrevious[$taskName] = [Environment]::GetEnvironmentVariable($taskName, 'Process') }
        [Environment]::SetEnvironmentVariable($taskName, $taskValue, 'Process')
    }
    foreach ($taskSetting in @{ CCAI_CHANNEL_STORAGE_RESTRICTED='true'; DOTNET_CLI_TELEMETRY_OPTOUT='1'; DOTNET_GENERATE_ASPNET_CERTIFICATE='false'; DOTNET_CLI_HOME=(Join-Path $taskRepo '.local/cli'); NUGET_PACKAGES=(Join-Path $taskRepo '.local/packages') }.GetEnumerator()) {
        $taskPrevious[$taskSetting.Key] = [Environment]::GetEnvironmentVariable($taskSetting.Key, 'Process')
        [Environment]::SetEnvironmentVariable($taskSetting.Key, $taskSetting.Value, 'Process')
    }
    if (-not $DotNetExe) { $DotNetExe = Join-Path $taskWorkspace 'work/runtimes/dotnet/dotnet.exe' }
    if (-not (Test-Path -LiteralPath $DotNetExe)) { $DotNetExe = (Get-Command dotnet -ErrorAction Stop).Source }
    $taskArtifacts = Join-Path $taskRepo '.local/build-channels'
    $taskRestore = @('restore', (Join-Path $taskRepo 'src/ContactCenterAI.Channels/ContactCenterAI.Channels.csproj'), '--locked-mode', '--artifacts-path', $taskArtifacts, '--configfile', (Join-Path $taskRepo 'NuGet.Config'))
    $taskFeed = Join-Path $taskWorkspace 'work/nuget-feed'
    if (Test-Path -LiteralPath $taskFeed) { $taskRestore += @('--source', $taskFeed) }
    Push-Location -LiteralPath $taskRepo
    try {
        $taskSdk = (Get-Content -LiteralPath (Join-Path $taskRepo 'global.json') -Raw | ConvertFrom-Json).sdk.version
        if ((& $DotNetExe --version) -cne $taskSdk) { throw 'CHANNEL_SDK_MISMATCH' }
        & $DotNetExe @taskRestore
        if ($LASTEXITCODE -ne 0) { throw 'CHANNEL_RESTORE_FAILED' }
        & $DotNetExe build (Join-Path $taskRepo 'src/ContactCenterAI.Channels/ContactCenterAI.Channels.csproj') --no-restore --artifacts-path $taskArtifacts -c Release
        if ($LASTEXITCODE -ne 0) { throw 'CHANNEL_BUILD_FAILED' }
        $taskArguments = @()
        if ($InspectTelegram) { $taskArguments = @('--inspect-telegram') } elseif ($Status) { $taskArguments = @('--status') }
        & $DotNetExe (Join-Path $taskArtifacts 'bin/ContactCenterAI.Channels/release/ContactCenterAI.Channels.dll') @taskArguments
        if ($LASTEXITCODE -ne 0) { throw 'CHANNEL_RUN_FAILED' }
    } finally { Pop-Location }
} finally {
    foreach ($taskName in $taskPrevious.Keys) { [Environment]::SetEnvironmentVariable($taskName, $taskPrevious[$taskName], 'Process') }
}
