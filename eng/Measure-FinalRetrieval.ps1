#requires -Version 7.2
[CmdletBinding()]
param([ValidateSet('0.10')][string]$Version='0.10')
$ErrorActionPreference='Stop'
$taskRepo=Split-Path -Parent $PSScriptRoot
$taskWorkspace=Split-Path -Parent (Split-Path -Parent $taskRepo)
$taskDotnet=Join-Path $taskWorkspace 'work/runtimes/dotnet/dotnet.exe'
if(-not(Test-Path -LiteralPath $taskDotnet)){$taskDotnet=(Get-Command dotnet -ErrorAction Stop).Source}
$taskOriginal=@{}
$taskChanges=@{DOTNET_CLI_HOME=(Join-Path $taskRepo '.local/cli');NUGET_PACKAGES=(Join-Path $taskRepo '.local/packages');APPDATA=(Join-Path $taskRepo '.local/appdata');DOTNET_CLI_TELEMETRY_OPTOUT='1';DOTNET_GENERATE_ASPNET_CERTIFICATE='false'}
try{
  foreach($taskName in $taskChanges.Keys){$taskOriginal[$taskName]=[Environment]::GetEnvironmentVariable($taskName,'Process');[Environment]::SetEnvironmentVariable($taskName,$taskChanges[$taskName],'Process')}
  & (Join-Path $PSScriptRoot 'Start-EmbeddingModel.ps1')
  & (Join-Path $PSScriptRoot 'Start-LocalModel.ps1')
  & (Join-Path $PSScriptRoot 'Invoke-RetrievalChecks.ps1') -DotNetExe $taskDotnet
  $taskDll=Join-Path $taskRepo '.local/build-retrieval/bin/ContactCenterAI.RetrievalChecks/release/ContactCenterAI.RetrievalChecks.dll'
  $taskVersionFlag=if($Version -eq '0.10'){'--V10'}else{'--V09'}
  & $taskDotnet $taskDll $taskRepo --Live --Rerank $taskVersionFlag
  if($LASTEXITCODE -ne 0){throw 'FINAL_REGRESSION_FAILED'}
  & $taskDotnet $taskDll $taskRepo --Live --Rerank $taskVersionFlag --Additional
  if($LASTEXITCODE -ne 0){throw 'ADDITIONAL_EVALUATION_FAILED'}
  python (Join-Path $taskRepo 'evaluation/evaluators/final_rag_eval.py') --version $Version
  if($LASTEXITCODE -ne 0){throw 'FINAL_REPORT_FAILED'}
}finally{foreach($taskName in $taskOriginal.Keys){[Environment]::SetEnvironmentVariable($taskName,$taskOriginal[$taskName],'Process')}}
