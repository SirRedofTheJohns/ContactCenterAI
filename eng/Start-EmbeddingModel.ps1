#requires -Version 7.2
[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$taskRepo=Split-Path -Parent $PSScriptRoot
$taskDirectory=Join-Path $taskRepo '.local/embedding'
$taskDigest='7907646426070047a77226ac3e684fbbe8410524f7b4a74d02837e43f2146bab'
$taskManifest=Join-Path $taskDirectory 'models/manifests/registry.ollama.ai/library/bge-m3/latest'
if (-not (Test-Path -LiteralPath $taskManifest) -or (Get-FileHash -LiteralPath $taskManifest -Algorithm SHA256).Hash.ToLowerInvariant() -cne $taskDigest) {throw 'EMBEDDING_MODEL_NOT_PREPARED: falta BGE-M3 aprobado; este lanzador no descarga modelos.'}
$taskTags=$null
try {$taskTags=Invoke-RestMethod -Uri 'http://127.0.0.1:11435/api/tags' -TimeoutSec 2} catch {}
if (-not $taskTags) {
    $taskOllama=Join-Path $env:LOCALAPPDATA 'Programs/Ollama/ollama.exe'
    if (-not (Test-Path -LiteralPath $taskOllama)) {$taskOllama=(Get-Command ollama -ErrorAction Stop).Source}
    New-Item -ItemType Directory -Path (Join-Path $taskDirectory 'tmp') -Force | Out-Null
    $taskChanges=@{OLLAMA_HOST='127.0.0.1:11435';OLLAMA_MODELS=(Join-Path $taskDirectory 'models');OLLAMA_NO_CLOUD='1';OLLAMA_TMPDIR=(Join-Path $taskDirectory 'tmp');OLLAMA_NUM_PARALLEL='1';OLLAMA_MAX_LOADED_MODELS='1';CCAI_BFF_CLIENT_SECRET=$null;CCAI_SOURCE_SERVICE_KEY=$null;CCAI_OPERATIONAL_DB_PASSWORD=$null}
    $taskOriginal=@{}
    try {
        foreach($taskName in $taskChanges.Keys){$taskOriginal[$taskName]=[Environment]::GetEnvironmentVariable($taskName,'Process');[Environment]::SetEnvironmentVariable($taskName,$taskChanges[$taskName],'Process')}
        $taskLaunch=@{FilePath=$taskOllama;ArgumentList='serve';WorkingDirectory=$taskDirectory;PassThru=$true;RedirectStandardOutput=(Join-Path $taskDirectory 'server.stdout.log');RedirectStandardError=(Join-Path $taskDirectory 'server.stderr.log')}
        if($IsWindows){$taskLaunch.WindowStyle='Hidden'}
        $taskProcess=Start-Process @taskLaunch
        @{pid=$taskProcess.Id;executable=$taskOllama;startedAtUtc=$taskProcess.StartTime.ToUniversalTime().ToString('o')}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $taskDirectory 'process.json') -Encoding utf8
    } finally {foreach($taskName in $taskOriginal.Keys){[Environment]::SetEnvironmentVariable($taskName,$taskOriginal[$taskName],'Process')}}
    $taskDeadline=[DateTimeOffset]::UtcNow.AddSeconds(15)
    do{try{$taskTags=Invoke-RestMethod -Uri 'http://127.0.0.1:11435/api/tags' -TimeoutSec 2}catch{};if(-not $taskTags){Start-Sleep -Milliseconds 250}}while(-not $taskTags -and [DateTimeOffset]::UtcNow -lt $taskDeadline)
}
if(-not ($taskTags.models|Where-Object{$_.name -ceq 'bge-m3:latest' -and $_.digest -ceq $taskDigest})){throw 'EMBEDDING_MODEL_PIN_MISMATCH'}
$taskBody=@{model='bge-m3:latest';input=@('Preparar búsqueda bilingüe de la propiedad ficticia');truncate=$false;keep_alive='30m';options=@{num_gpu=0;num_thread=8}}|ConvertTo-Json -Depth 6 -Compress
$taskWarm=Invoke-RestMethod -Uri 'http://127.0.0.1:11435/api/embed' -Method Post -ContentType 'application/json; charset=utf-8' -Body $taskBody -TimeoutSec 60
if($taskWarm.model -cne 'bge-m3:latest' -or $taskWarm.embeddings.Count -ne 1 -or $taskWarm.embeddings[0].Count -ne 1024){throw 'EMBEDDING_WARMUP_FAILED'}
Write-Output 'Búsqueda semántica local preparada; modelo bilingüe verificado.'
