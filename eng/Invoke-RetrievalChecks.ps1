#requires -Version 7.2
[CmdletBinding()]
param([switch]$Live,[switch]$Rerank,[string]$DotNetExe)
$ErrorActionPreference='Stop'
$taskRepo=Split-Path -Parent $PSScriptRoot
if($Live){
    if(-not $Rerank){throw 'HISTORICAL_BASELINE_FROZEN: see evaluation/reports/demo-v0.7; use Measure-FinalRetrieval.ps1 for current live evaluation.'}
    & (Join-Path $PSScriptRoot 'Measure-FinalRetrieval.ps1') -Version '0.10'
    return
}
$taskWorkspace=Split-Path -Parent (Split-Path -Parent $taskRepo)
if(-not $DotNetExe){$DotNetExe=Join-Path $taskWorkspace 'work/runtimes/dotnet/dotnet.exe'}
if(-not(Test-Path -LiteralPath $DotNetExe)){$DotNetExe=(Get-Command dotnet -ErrorAction Stop).Source}
$taskChanges=@{DOTNET_CLI_HOME=(Join-Path $taskRepo '.local/cli');NUGET_PACKAGES=(Join-Path $taskRepo '.local/packages');DOTNET_CLI_TELEMETRY_OPTOUT='1';DOTNET_GENERATE_ASPNET_CERTIFICATE='false'}
if($IsWindows){$taskChanges.APPDATA=Join-Path $taskRepo '.local/appdata'}
$taskOriginal=@{}
try{
    foreach($taskName in $taskChanges.Keys){$taskOriginal[$taskName]=[Environment]::GetEnvironmentVariable($taskName,'Process');[Environment]::SetEnvironmentVariable($taskName,$taskChanges[$taskName],'Process')}
    if($Live){& (Join-Path $PSScriptRoot 'Start-EmbeddingModel.ps1');if($Rerank){& (Join-Path $PSScriptRoot 'Start-LocalModel.ps1')}}
    $taskProject=Join-Path $taskRepo 'tests/ContactCenterAI.RetrievalChecks/ContactCenterAI.RetrievalChecks.csproj'
    $taskArtifacts=Join-Path $taskRepo '.local/build-retrieval'
    $taskRestore=@('restore',$taskProject,'--artifacts-path',$taskArtifacts,'--locked-mode','--configfile',(Join-Path $taskRepo 'NuGet.Config'))
    $taskFeed=Join-Path $taskWorkspace 'work/nuget-feed';if(Test-Path -LiteralPath $taskFeed){$taskRestore+=@('--source',$taskFeed)}
    & $DotNetExe @taskRestore;if($LASTEXITCODE -ne 0){throw 'RETRIEVAL_RESTORE_FAILED'}
    & $DotNetExe build $taskProject --artifacts-path $taskArtifacts --no-restore -c Release;if($LASTEXITCODE -ne 0){throw 'RETRIEVAL_BUILD_FAILED'}
    $taskArguments=@((Join-Path $taskArtifacts 'bin/ContactCenterAI.RetrievalChecks/release/ContactCenterAI.RetrievalChecks.dll'),$taskRepo)
    if($Live){$taskArguments+='--Live'};if($Rerank){$taskArguments+='--Rerank'}
    & $DotNetExe @taskArguments;if($LASTEXITCODE -ne 0){throw 'RETRIEVAL_CHECK_FAILED'}
}finally{foreach($taskName in $taskOriginal.Keys){[Environment]::SetEnvironmentVariable($taskName,$taskOriginal[$taskName],'Process')}}
