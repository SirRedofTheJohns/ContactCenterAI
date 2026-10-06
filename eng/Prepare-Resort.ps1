#requires -Version 7.2
[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$taskRepo=Split-Path -Parent $PSScriptRoot
& (Join-Path $PSScriptRoot 'Start-Channels.ps1') -PrepareOnly
$taskConfig=Join-Path $taskRepo 'deploy/channels/.env'
$taskText=[IO.File]::ReadAllText($taskConfig)
if ($taskText -notmatch '(?m)^CCAI_CHANNEL_RESORT_BRIDGE_KEY=.{32,128}\r?$') {
    $taskKey=[Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
    $taskText=[regex]::Replace($taskText,'(?m)^CCAI_CHANNEL_RESORT_BRIDGE_KEY=.*\r?\n?','')
    $taskText+="`nCCAI_CHANNEL_RESORT_BRIDGE_KEY=$taskKey`n"
}
if ($taskText -match '(?m)^CCAI_CHANNEL_RESORT_ENABLED=') { $taskText=[regex]::Replace($taskText,'(?m)^CCAI_CHANNEL_RESORT_ENABLED=.*','CCAI_CHANNEL_RESORT_ENABLED=true') }
else { $taskText+="CCAI_CHANNEL_RESORT_ENABLED=true`n" }
[IO.File]::WriteAllText($taskConfig,$taskText,[Text.UTF8Encoding]::new($false))
$taskData=Join-Path $taskRepo '.local/resort'
New-Item -ItemType Directory -Force -Path $taskData | Out-Null
if ($IsWindows) {
    $taskAcl=[Security.AccessControl.DirectorySecurity]::new();$taskAcl.SetAccessRuleProtection($true,$false)
    foreach ($taskAccount in @([Security.Principal.WindowsIdentity]::GetCurrent().User,[Security.Principal.NTAccount]::new([Environment]::MachineName,(Split-Path -Leaf $env:USERPROFILE)),[Security.Principal.SecurityIdentifier]::new('S-1-5-18'))) {
        $taskAcl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new($taskAccount,'FullControl','ContainerInherit, ObjectInherit','None','Allow'))
    }
    [IO.FileSystemAclExtensions]::SetAccessControl([IO.DirectoryInfo]::new($taskData),$taskAcl)
} else { [IO.File]::SetUnixFileMode($taskData,[IO.UnixFileMode]'UserRead,UserWrite,UserExecute') }
Write-Output 'Resort preparado. Clave del puente guardada solo en la configuración local; datos anteriores conservados.'
