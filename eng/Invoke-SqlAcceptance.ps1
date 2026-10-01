#requires -Version 7.2
[CmdletBinding()]
param([string]$DotNetExe)
$ErrorActionPreference='Stop'
$taskRepo=Split-Path -Parent $PSScriptRoot
$taskWorkspace=Split-Path -Parent (Split-Path -Parent $taskRepo)
if(-not $DotNetExe){$DotNetExe=Join-Path $taskWorkspace 'work/runtimes/dotnet/dotnet.exe'}
if(-not(Test-Path -LiteralPath $DotNetExe)){$DotNetExe=(Get-Command dotnet -ErrorAction Stop).Source}
$taskChanges=@{DOTNET_CLI_HOME=(Join-Path $taskRepo '.local/cli');NUGET_PACKAGES=(Join-Path $taskRepo '.local/packages');APPDATA=(Join-Path $taskRepo '.local/appdata');DOTNET_CLI_TELEMETRY_OPTOUT='1';DOTNET_GENERATE_ASPNET_CERTIFICATE='false'}
$taskOriginal=@{}
try{
 foreach($taskName in $taskChanges.Keys){$taskOriginal[$taskName]=[Environment]::GetEnvironmentVariable($taskName,'Process');[Environment]::SetEnvironmentVariable($taskName,$taskChanges[$taskName],'Process')}
 $taskProject=Join-Path $taskRepo 'spikes/SqlProductAcceptance/SqlProductAcceptance.csproj'
 $taskArtifacts=Join-Path $taskRepo '.local/build-sql-acceptance'
 $taskRestore=@('restore',$taskProject,'--artifacts-path',$taskArtifacts,'--locked-mode','--configfile',(Join-Path $taskRepo 'NuGet.Config'))
 $taskFeed=Join-Path $taskWorkspace 'work/nuget-feed';if(Test-Path -LiteralPath $taskFeed){$taskRestore+=@('--source',$taskFeed)}
 & $DotNetExe @taskRestore;if($LASTEXITCODE -ne 0){throw 'SQL_ACCEPTANCE_RESTORE_FAILED'}
 & $DotNetExe build $taskProject --artifacts-path $taskArtifacts --no-restore -c Release;if($LASTEXITCODE -ne 0){throw 'SQL_ACCEPTANCE_BUILD_FAILED'}
 $taskDll=Join-Path $taskArtifacts 'bin/SqlProductAcceptance/release/SqlProductAcceptance.dll'
 & $DotNetExe $taskDll $taskRepo
 if($LASTEXITCODE -ne 0){& $DotNetExe $taskDll $taskRepo --managed;throw 'SQL_ENCRYPTED_PRODUCT_NOT_VERIFIED: see safe reports; no data changed.'}
}finally{foreach($taskName in $taskOriginal.Keys){[Environment]::SetEnvironmentVariable($taskName,$taskOriginal[$taskName],'Process')}}
