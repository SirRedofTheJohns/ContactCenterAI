#requires -Version 7.2
[CmdletBinding()]
param([switch]$Resort, [switch]$NoBrowser, [switch]$FreshData, [switch]$Rebuild, [ValidateSet('simulated','local-llm')][string]$IntentProvider)
$ErrorActionPreference='Stop'
$taskRepo=Split-Path -Parent $PSScriptRoot
$taskWorkspace=Split-Path -Parent (Split-Path -Parent $taskRepo)
$taskDotnet=Join-Path $taskWorkspace 'work/runtimes/dotnet/dotnet.exe'
if (-not (Test-Path -LiteralPath $taskDotnet)) { $taskDotnet=(Get-Command dotnet -ErrorAction Stop).Source }
$taskPreviousLocation=Get-Location
$taskPreviousEnvironment=@{}
$taskUrl='http://127.0.0.1:7452'
$taskProfilePath=Join-Path $taskRepo '.local/model/profile.json'
if (-not $IntentProvider) {
    if (Test-Path -LiteralPath $taskProfilePath) {$IntentProvider=(Get-Content -LiteralPath $taskProfilePath -Raw | ConvertFrom-Json).mode}
    else {$IntentProvider='simulated'}
}
if ($IntentProvider -notin @('simulated','local-llm')) {throw 'INTENT_PROVIDER_MODE_INVALID'}
$taskDesiredProvider=if ($IntentProvider -eq 'local-llm') {'ollama:qwen3-vl@901cae732162:intent-local-v1'} else {'simulated-intent-v1'}
function Test-DemoAvailable {
    try {
        $taskReady=Invoke-RestMethod -Uri ($taskUrl+'/health/ready') -TimeoutSec 2
        $taskPage=Invoke-WebRequest -Uri ($taskUrl+'/') -TimeoutSec 2
        $taskSource=Invoke-RestMethod -Uri 'http://127.0.0.1:7453/health/live' -TimeoutSec 2
        $taskIdentity=Invoke-RestMethod -Uri 'http://localhost:8080/realms/contactcenterai-local/.well-known/openid-configuration' -TimeoutSec 8
        if($taskIdentity.issuer -cne 'http://localhost:8080/realms/contactcenterai-local'){return $false}
        if ($IntentProvider -eq 'local-llm') {
            $taskModels=Invoke-RestMethod -Uri 'http://127.0.0.1:11434/api/tags' -TimeoutSec 2
            if (-not ($taskModels.models | Where-Object {$_.name -ceq 'qwen3-vl:latest' -and $_.digest -ceq '901cae73216286ea8c5aba8b46d307ff7188f737285ec500c795a12f05225d28'})) {return $false}
            $taskEmbeddingModels=Invoke-RestMethod -Uri 'http://127.0.0.1:11435/api/tags' -TimeoutSec 2
            if(-not($taskEmbeddingModels.models|Where-Object{$_.name -ceq 'bge-m3:latest' -and $_.digest -ceq '7907646426070047a77226ac3e684fbbe8410524f7b4a74d02837e43f2146bab'})){return $false}
        }
        if ($Resort) { $taskCatalog=Invoke-RestMethod -Uri ($taskUrl+'/v2/resort/catalog') -TimeoutSec 2; if (-not $taskCatalog.fictional) {return $false} }
        return $taskReady.status -eq 'Ready' -and $taskReady.profile -eq 'demo-assistant-v0.10' -and $taskReady.modelProvider -ceq $taskDesiredProvider -and
            $taskSource.component -eq 'ReservationSource' -and $taskPage.Content.Contains('<title>ContactCenterAI · Demo</title>')
    } catch { return $false }
}
if (-not $FreshData -and -not $Rebuild -and (Test-DemoAvailable)) {
    Write-Output 'La demo ya está lista. Usuarios customer-a / customer-b; clave 123456Aa!.'
    if (-not $NoBrowser) { Start-Process $taskUrl }
    return
}
$taskEnvironment=@{
    CCAI_INTENT_PROVIDER=$IntentProvider
    CCAI_RETRIEVAL_PROVIDER=$(if($IntentProvider -eq 'local-llm'){'semantic'}else{'topic'})
    DOTNET_CLI_HOME=(Join-Path $taskRepo '.local/cli'); DOTNET_CLI_TELEMETRY_OPTOUT='1'
    DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1'; DOTNET_GENERATE_ASPNET_CERTIFICATE='false'
    DOTNET_ADD_GLOBAL_TOOLS_TO_PATH='false'; NUGET_PACKAGES=(Join-Path $taskRepo '.local/packages')
    NUGET_HTTP_CACHE_PATH=(Join-Path $taskRepo '.local/nuget-http')
}
if ($IntentProvider -eq 'local-llm') {& (Join-Path $PSScriptRoot 'Start-LocalModel.ps1');& (Join-Path $PSScriptRoot 'Start-EmbeddingModel.ps1')}
if ($IsWindows) { $taskEnvironment.APPDATA=Join-Path $taskRepo '.local/appdata' }
$taskEnvPath=Join-Path $taskRepo 'deploy/local/.env'
if (-not (Test-Path -LiteralPath $taskEnvPath)) { throw 'La configuración local no existe. La preparación de la demo debe realizarse una vez.' }
foreach ($taskLine in [IO.File]::ReadAllLines($taskEnvPath)) {
    if ($taskLine.StartsWith('CCAI_BFF_CLIENT_SECRET=')) { $taskEnvironment.CCAI_BFF_CLIENT_SECRET=$taskLine.Split('=',2)[1] }
    if ($taskLine.StartsWith('CCAI_SOURCE_SERVICE_KEY=')) { $taskEnvironment.CCAI_SOURCE_SERVICE_KEY=$taskLine.Split('=',2)[1] }
}
if (-not $taskEnvironment.CCAI_SOURCE_SERVICE_KEY) {
    $taskEnvironment.CCAI_SOURCE_SERVICE_KEY=[Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
    [IO.File]::AppendAllText($taskEnvPath, "`nCCAI_SOURCE_SERVICE_KEY="+$taskEnvironment.CCAI_SOURCE_SERVICE_KEY+"`n",[Text.UTF8Encoding]::new($false))
}
if ($Resort) {
    $taskChannelConfig=Join-Path $taskRepo 'deploy/channels/.env'
    if (-not (Test-Path -LiteralPath $taskChannelConfig)) { throw 'Primero prepara la configuración de canales.' }
    foreach ($taskLine in [IO.File]::ReadAllLines($taskChannelConfig)) {
        if ($taskLine.StartsWith('CCAI_CHANNEL_RESORT_BRIDGE_KEY=')) { $taskEnvironment.CCAI_RESORT_BRIDGE_KEY=$taskLine.Split('=',2)[1] }
        if ($taskLine.StartsWith('CCAI_CHANNEL_META_ENDPOINT_ID=')) { $taskEnvironment.CCAI_RESORT_META_ENDPOINT_ID=$taskLine.Split('=',2)[1] }
    }
    if (-not $taskEnvironment.CCAI_RESORT_BRIDGE_KEY) { throw 'Falta la clave privada del puente del resort. Ejecuta Prepare-Resort.ps1.' }
    $taskEnvironment.CCAI_RESORT_ENABLED='true'
    $taskUrl='http://127.0.0.1:7452'
}
function Stop-OwnedDemo {
    param([string]$Marker='process.json',[string]$Project='ContactCenterAI.Api')
    $taskMarker=Join-Path $taskRepo ('.local/demo/'+$Marker)
    if (-not (Test-Path -LiteralPath $taskMarker)) { return }
    $taskMetadata=Get-Content -LiteralPath $taskMarker -Raw | ConvertFrom-Json
    $taskExisting=Get-Process -Id $taskMetadata.pid -ErrorAction SilentlyContinue
    if (-not $taskExisting) { return }
    $taskExpectedDll=Join-Path $taskRepo ('.local/build-ingress/bin/'+$Project+'/release/'+$Project+'.dll')
    if ($taskMetadata.executable -ne $taskDotnet -or $taskMetadata.assembly -ne $taskExpectedDll -or
        $taskExisting.Path -ne $taskDotnet -or
        [Math]::Abs(($taskExisting.StartTime.ToUniversalTime()-([datetimeoffset]$taskMetadata.startedAtUtc).UtcDateTime).TotalSeconds) -gt 1) {
        throw 'No se puede confirmar que el proceso anterior pertenece a esta demo. No se detuvo ningún proceso.'
    }
    $taskExisting.Kill()
    if (-not $taskExisting.WaitForExit(5000)) { throw 'La instancia anterior no terminó.' }
}
try {
    Set-Location -LiteralPath $taskRepo
    foreach ($taskName in $taskEnvironment.Keys) {
        $taskPreviousEnvironment[$taskName]=[Environment]::GetEnvironmentVariable($taskName,'Process')
        [Environment]::SetEnvironmentVariable($taskName,$taskEnvironment[$taskName],'Process')
    }
    & (Join-Path $PSScriptRoot 'Ensure-LocalIdentity.ps1')
    $taskDiscovery=Invoke-RestMethod -Uri 'http://localhost:8080/realms/contactcenterai-local/.well-known/openid-configuration' -TimeoutSec 8
    if ($taskDiscovery.issuer -cne 'http://localhost:8080/realms/contactcenterai-local') { throw 'El servicio de acceso local no coincide con la demo.' }
    Stop-OwnedDemo
    Stop-OwnedDemo -Marker 'source-process.json' -Project 'ContactCenterAI.Simulator'
    if ($FreshData) {
        $taskLocalRoot=[IO.Path]::GetFullPath((Join-Path $taskRepo '.local'))
        $taskBackupRoot=Join-Path $taskLocalRoot 'demo-backups'
        $taskBackup=Join-Path $taskBackupRoot ([DateTimeOffset]::UtcNow.ToString('yyyyMMdd-HHmmss')+'-'+[Guid]::NewGuid().ToString('N'))
        $taskDataPaths=@((Join-Path $taskLocalRoot 'demo/operations.db'),(Join-Path $taskLocalRoot 'source/reservations.db'))
        $taskMoves=@()
        foreach ($taskDirectory in @($taskLocalRoot,(Join-Path $taskLocalRoot 'demo'),(Join-Path $taskLocalRoot 'source'),$taskBackupRoot)) {
            if ((Test-Path -LiteralPath $taskDirectory) -and ((Get-Item -LiteralPath $taskDirectory).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'No se archivó ningún dato: la ruta local contiene un enlace.' }
        }
        foreach ($taskDataPath in $taskDataPaths) {
            foreach ($taskSuffix in @('','-wal','-shm')) {
                $taskSourcePath=[IO.Path]::GetFullPath($taskDataPath+$taskSuffix)
                $taskTargetPath=[IO.Path]::GetFullPath((Join-Path $taskBackup ([IO.Path]::GetFileName($taskSourcePath))))
                if (-not $taskSourcePath.StartsWith($taskLocalRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase) -or
                    -not $taskTargetPath.StartsWith($taskLocalRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) { throw 'Ruta de archivo fuera de la demo.' }
                if (Test-Path -LiteralPath $taskSourcePath) {
                    if ((Get-Item -LiteralPath $taskSourcePath).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'No se archivó ningún dato: una base contiene un enlace.' }
                    $taskMoves+=@{Source=$taskSourcePath;Target=$taskTargetPath}
                }
            }
        }
        New-Item -ItemType Directory -Path $taskBackup -Force | Out-Null
        foreach ($taskMove in $taskMoves) { Move-Item -LiteralPath $taskMove.Source -Destination $taskMove.Target }
        Write-Output 'La presentación anterior se conservó en .local/demo-backups. Se prepara otra con reservas y fechas nuevas.'
    }
    $taskArtifacts=Join-Path $taskRepo '.local/build-ingress'
    $taskRestore=@('restore','ContactCenterAI.slnx','--artifacts-path',$taskArtifacts,'--configfile','NuGet.Config','--locked-mode')
    $taskFeed=Join-Path $taskWorkspace 'work/nuget-feed'
    if (Test-Path -LiteralPath $taskFeed) { $taskRestore+=@('--source',$taskFeed) }
    & $taskDotnet @taskRestore
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo preparar la demo.' }
    & $taskDotnet build ContactCenterAI.slnx --artifacts-path $taskArtifacts --no-restore -c Release
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo compilar la demo.' }
    $taskDll=Join-Path $taskArtifacts 'bin/ContactCenterAI.Api/release/ContactCenterAI.Api.dll'
    $taskDemoDirectory=Join-Path $taskRepo '.local/demo'
    New-Item -ItemType Directory -Path $taskDemoDirectory -Force | Out-Null
    $taskSourceAvailable=$false
    try { $taskSourceAvailable=(Invoke-RestMethod -Uri 'http://127.0.0.1:7453/health/live' -TimeoutSec 2).component -eq 'ReservationSource' } catch {}
    if (-not $taskSourceAvailable) {
        $taskSourceDll=Join-Path $taskArtifacts 'bin/ContactCenterAI.Simulator/release/ContactCenterAI.Simulator.dll'
        $taskSourceLaunch=@{ FilePath=$taskDotnet; ArgumentList=('"'+$taskSourceDll+'"'); WorkingDirectory=$taskRepo; PassThru=$true
            RedirectStandardOutput=(Join-Path $taskDemoDirectory 'source.stdout.log'); RedirectStandardError=(Join-Path $taskDemoDirectory 'source.stderr.log') }
        if ($IsWindows) { $taskSourceLaunch.WindowStyle='Hidden' }
        $taskSourceProcess=Start-Process @taskSourceLaunch
        $taskSourceDeadline=[DateTimeOffset]::UtcNow.AddSeconds(10)
        do {
            if ($taskSourceProcess.HasExited) { throw 'La fuente ficticia no pudo arrancar; consulta .local/demo/source.stderr.log.' }
            try { $taskSourceAvailable=(Invoke-RestMethod -Uri 'http://127.0.0.1:7453/health/live' -TimeoutSec 1).component -eq 'ReservationSource' } catch {}
            if (-not $taskSourceAvailable) { Start-Sleep -Milliseconds 250 }
        } while (-not $taskSourceAvailable -and [DateTimeOffset]::UtcNow -lt $taskSourceDeadline)
        if (-not $taskSourceAvailable) { $taskSourceProcess.Kill(); throw 'La fuente ficticia no respondió.' }
        @{pid=$taskSourceProcess.Id;startedAtUtc=$taskSourceProcess.StartTime.ToUniversalTime().ToString('o');executable=$taskDotnet;assembly=$taskSourceDll} |
            ConvertTo-Json | Set-Content -LiteralPath (Join-Path $taskDemoDirectory 'source-process.json') -Encoding utf8
    }
    $taskLaunch=@{
        FilePath=$taskDotnet; ArgumentList=('"'+$taskDll+'" --demo-local'); WorkingDirectory=$taskRepo
        PassThru=$true
        RedirectStandardOutput=(Join-Path $taskDemoDirectory 'server.stdout.log')
        RedirectStandardError=(Join-Path $taskDemoDirectory 'server.stderr.log')
    }
    if ($IsWindows) { $taskLaunch.WindowStyle='Hidden' }
    $taskProcess=Start-Process @taskLaunch
    $taskDeadline=[DateTimeOffset]::UtcNow.AddSeconds(45)
    $taskAvailable=$false
    do {
        if ($taskProcess.HasExited) { throw 'La demo no pudo arrancar. El registro local está en .local/demo/server.stderr.log.' }
        $taskAvailable=Test-DemoAvailable
        if ($taskAvailable) { break }
        Start-Sleep -Milliseconds 250
    } while ([DateTimeOffset]::UtcNow -lt $taskDeadline)
    if (-not $taskAvailable) { $taskProcess.Kill(); throw 'La demo no respondió dentro del tiempo de arranque.' }
    New-Item -ItemType Directory -Path (Split-Path -Parent $taskProfilePath) -Force | Out-Null
    @{mode=$IntentProvider} | ConvertTo-Json | Set-Content -LiteralPath $taskProfilePath -Encoding utf8
    @{pid=$taskProcess.Id; startedAtUtc=$taskProcess.StartTime.ToUniversalTime().ToString('o'); executable=$taskDotnet; assembly=$taskDll} |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $taskDemoDirectory 'process.json') -Encoding utf8
    Write-Output 'Demo lista: http://127.0.0.1:7452 | usuarios customer-a / customer-b | clave 123456Aa!'
    if (-not $NoBrowser) { Start-Process $taskUrl }
}
finally {
    foreach ($taskName in $taskPreviousEnvironment.Keys) { [Environment]::SetEnvironmentVariable($taskName,$taskPreviousEnvironment[$taskName],'Process') }
    Set-Location -LiteralPath $taskPreviousLocation.Path
}
