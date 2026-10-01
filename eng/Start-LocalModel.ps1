#requires -Version 7.2
[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$taskRepo=Split-Path -Parent $PSScriptRoot
$taskModelDirectory=Join-Path $taskRepo '.local/model'
New-Item -ItemType Directory -Path $taskModelDirectory -Force | Out-Null
$taskModelTag='qwen3-vl:latest'
$taskModelDigest='901cae73216286ea8c5aba8b46d307ff7188f737285ec500c795a12f05225d28'
$taskTags=$null
try {$taskTags=Invoke-RestMethod -Uri 'http://127.0.0.1:11434/api/tags' -TimeoutSec 3} catch {}
if (-not $taskTags) {
    $taskOllama=Join-Path $env:LOCALAPPDATA 'Programs/Ollama/ollama.exe'
    if (-not (Test-Path -LiteralPath $taskOllama)) {$taskOllama=(Get-Command ollama -ErrorAction SilentlyContinue).Source}
    if (-not $taskOllama) {throw 'LOCAL_MODEL_RUNTIME_NOT_INSTALLED: esta selección necesita Ollama y el modelo aprobado; no se descargó nada.'}
    $taskTemporary=Join-Path $taskModelDirectory 'tmp'
    New-Item -ItemType Directory -Path $taskTemporary -Force | Out-Null
    $taskChanges=@{OLLAMA_HOST='127.0.0.1:11434';OLLAMA_NO_CLOUD='1';OLLAMA_TMPDIR=$taskTemporary;OLLAMA_CONTEXT_LENGTH='2048';OLLAMA_NUM_PARALLEL='1';OLLAMA_MAX_LOADED_MODELS='1';CCAI_BFF_CLIENT_SECRET=$null;CCAI_SOURCE_SERVICE_KEY=$null;CCAI_OPERATIONAL_DB_PASSWORD=$null}
    $taskOriginal=@{}
    try {
        foreach ($taskName in $taskChanges.Keys) {$taskOriginal[$taskName]=[Environment]::GetEnvironmentVariable($taskName,'Process');[Environment]::SetEnvironmentVariable($taskName,$taskChanges[$taskName],'Process')}
        $taskLaunch=@{FilePath=$taskOllama;ArgumentList='serve';WorkingDirectory=$taskModelDirectory;PassThru=$true;RedirectStandardOutput=(Join-Path $taskModelDirectory 'server.stdout.log');RedirectStandardError=(Join-Path $taskModelDirectory 'server.stderr.log')}
        if ($IsWindows) {$taskLaunch.WindowStyle='Hidden'}
        $taskModelProcess=Start-Process @taskLaunch
        @{pid=$taskModelProcess.Id;executable=$taskOllama;startedAtUtc=$taskModelProcess.StartTime.ToUniversalTime().ToString('o')} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $taskModelDirectory 'process.json') -Encoding utf8
    }
    finally {foreach ($taskName in $taskOriginal.Keys) {[Environment]::SetEnvironmentVariable($taskName,$taskOriginal[$taskName],'Process')}}
    $taskDeadline=[DateTimeOffset]::UtcNow.AddSeconds(12)
    do {try {$taskTags=Invoke-RestMethod -Uri 'http://127.0.0.1:11434/api/tags' -TimeoutSec 2} catch {}; if (-not $taskTags) {Start-Sleep -Milliseconds 250}} while (-not $taskTags -and [DateTimeOffset]::UtcNow -lt $taskDeadline)
}
if (-not $taskTags -or -not ($taskTags.models | Where-Object {$_.name -ceq $taskModelTag -and $_.digest -ceq $taskModelDigest})) {throw 'LOCAL_MODEL_PIN_MISMATCH: falta el modelo exacto aprobado; no se descargó ni sustituyó ningún modelo.'}
Write-Output 'Preparando la IA local ya instalada. La carga inicial puede tardar; no se enviarán datos a cloud.'
$taskPromptDirectory=Join-Path $taskRepo 'src/ContactCenterAI.Infrastructure/Prompts'
$taskSystem=Get-Content -LiteralPath (Join-Path $taskPromptDirectory 'intent-local-v1.system.txt') -Raw
$taskSchema=Get-Content -LiteralPath (Join-Path $taskPromptDirectory 'intent-local-v1.schema.json') -Raw | ConvertFrom-Json -AsHashtable
$taskSchema.properties.language=@{type='string';const='es'}
$taskUser=@{language='es';message='¿Cuál es la política de cancelación?'} | ConvertTo-Json -Compress
$taskPrompt="<|im_start|>system`n"+$taskSystem+"<|im_end|>`n<|im_start|>user`n"+$taskUser+"<|im_end|>`n<|im_start|>assistant`n<think>`n`n</think>`n`n"
$taskBody=@{model=$taskModelTag;prompt=$taskPrompt;raw=$true;stream=$false;think=$false;format=$taskSchema;keep_alive='15m';options=@{temperature=0;num_ctx=2048;num_predict=128;num_gpu=0;num_thread=8}} | ConvertTo-Json -Depth 12 -Compress
try {
    $taskWarm=Invoke-RestMethod -Uri 'http://127.0.0.1:11434/api/generate' -Method Post -ContentType 'application/json; charset=utf-8' -Body $taskBody -TimeoutSec 90
    if (-not $taskWarm.done -or $taskWarm.done_reason -cne 'stop' -or $taskWarm.model -cne $taskModelTag) {throw 'INVALID_MODEL_ENVELOPE'}
    $taskProposal=$taskWarm.response | ConvertFrom-Json
    if ($taskProposal.intent -cne 'faq' -or $taskProposal.language -cne 'es' -or $taskProposal.topic -cne 'cancellation' -or $null -ne $taskProposal.reservationId) {throw 'MODEL_WARMUP_PROPOSAL_REJECTED'}
} catch {throw 'LOCAL_AI_WARMUP_FAILED: la demo anterior se conserva; no se imprimió el cuerpo del modelo.'}
Write-Output 'IA local preparada. El producto conservará su límite de ocho segundos por consulta.'
$taskRankSystem=Get-Content -LiteralPath (Join-Path $taskPromptDirectory 'evidence-selection-v2.system.txt') -Raw
$taskCorpus=Get-Content -LiteralPath (Join-Path $taskRepo 'src/ContactCenterAI.Infrastructure/Knowledge/demo-v1.json') -Raw|ConvertFrom-Json
$taskRankCandidates=@();$taskRankIds=@('KB-CANCELLATION-ES','KB-SERVICES-ES','KB-IDENTITY-ES','KB-PAYMENTS-ES','KB-POOL-ES')
foreach($taskRankId in $taskRankIds){$taskSection=$taskCorpus|Where-Object{$_.documentId -ceq $taskRankId};$taskRankCandidates+=@{choice=$taskRankCandidates.Count+1;title=$taskSection.title;passage=$taskSection.content.Substring(0,[Math]::Min(600,$taskSection.content.Length))}}
$taskRankUser=@{language='es';question='¿Qué establece CP-001?';candidates=$taskRankCandidates}|ConvertTo-Json -Depth 8 -Compress -EscapeHandling EscapeNonAscii
$taskRankSchema=@{type='object';properties=@{choice=@{type='integer';enum=@(0,1,2,3,4,5)}};required=@('choice');additionalProperties=$false}
$taskRankPrompt="<|im_start|>system`n"+$taskRankSystem+"<|im_end|>`n<|im_start|>user`n"+$taskRankUser+"<|im_end|>`n<|im_start|>assistant`n<think>`n`n</think>`n`n"
$taskRankBody=@{model=$taskModelTag;prompt=$taskRankPrompt;raw=$true;stream=$false;think=$false;format=$taskRankSchema;keep_alive='15m';options=@{temperature=0;num_ctx=2048;num_predict=32;num_gpu=0;num_thread=8}}|ConvertTo-Json -Depth 10 -Compress
try{
    $taskRankWarm=Invoke-RestMethod -Uri 'http://127.0.0.1:11434/api/generate' -Method Post -ContentType 'application/json; charset=utf-8' -Body $taskRankBody -TimeoutSec 90
    if(-not $taskRankWarm.done -or $taskRankWarm.done_reason -cne 'stop' -or $taskRankWarm.model -cne $taskModelTag -or $taskRankWarm.prompt_eval_count -gt 1900 -or ($taskRankWarm.response|ConvertFrom-Json).choice -ne 1){throw 'RERANK_WARMUP_REJECTED'}
}catch{throw 'EVIDENCE_SELECTION_WARMUP_FAILED: la demo anterior se conserva; no se exportó el cuerpo del modelo.'}
Write-Output 'Selección de fuentes preparada antes de abrir la pantalla.'
