#requires -Version 7.2
[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$taskRepo=Split-Path -Parent $PSScriptRoot
$taskWorkspace=Split-Path -Parent (Split-Path -Parent $taskRepo)
$taskDotnet=Join-Path $taskWorkspace 'work/runtimes/dotnet/dotnet.exe'
if (-not (Test-Path -LiteralPath $taskDotnet)) { $taskDotnet=(Get-Command dotnet).Source }
$taskDotnet=[IO.Path]::GetFullPath($taskDotnet)
$taskAssemblies=@('build-channel-diagnostics','build-resort','build-ingress','build-channels') | ForEach-Object {
    [IO.Path]::GetFullPath((Join-Path $taskRepo ('.local/'+$_+'/bin/ContactCenterAI.Channels/release/ContactCenterAI.Channels.dll')))
}
if ($IsWindows) {
    $taskHosts=@(Get-CimInstance Win32_Process -Filter "Name = 'dotnet.exe'" | Where-Object {
        $taskCommand=$_.CommandLine
        $_.ExecutablePath -eq $taskDotnet -and $taskCommand -and @($taskAssemblies | Where-Object {$taskCommand.Replace('/','\').Contains($_)}).Count -gt 0
    })
    if ($taskHosts.Count -gt 1) { throw 'CHANNEL_HOST_SELECTION_AMBIGUOUS' }
    foreach ($taskHost in $taskHosts) { Stop-Process -Id $taskHost.ProcessId -ErrorAction Stop }
} else {
    $taskMarker=Join-Path $taskRepo '.local/channels/resort-process.json'
    if (Test-Path -LiteralPath $taskMarker) {
        $taskRecord=Get-Content -LiteralPath $taskMarker -Raw | ConvertFrom-Json
        $taskProcess=Get-Process -Id $taskRecord.pid -ErrorAction SilentlyContinue
        if ($taskProcess -and $taskProcess.Path -eq $taskRecord.executable -and $taskProcess.StartTime.ToUniversalTime().ToString('o') -eq $taskRecord.startedAtUtc) { Stop-Process -Id $taskProcess.Id }
    }
}
try { if ((Invoke-WebRequest -Uri 'http://127.0.0.1:7454/health/live' -TimeoutSec 2).StatusCode -eq 200) { throw 'CHANNEL_PORT_ALREADY_OWNED' } }
catch { if ($_.Exception.Message -eq 'CHANNEL_PORT_ALREADY_OWNED') { throw } }
$taskShell=(Get-Process -Id $PID).Path
$taskLaunch=@{FilePath=$taskShell;ArgumentList=@('-NoProfile','-File',('"'+(Join-Path $PSScriptRoot 'Start-Channels.ps1')+'"'))
    WorkingDirectory=$taskRepo;PassThru=$true
    RedirectStandardOutput=(Join-Path $taskRepo '.local/channels/resort-host.stdout.log');RedirectStandardError=(Join-Path $taskRepo '.local/channels/resort-host.stderr.log')}
if ($IsWindows) { $taskLaunch.WindowStyle='Hidden' }
$taskLauncher=Start-Process @taskLaunch
@{pid=$taskLauncher.Id;executable=$taskShell;startedAtUtc=$taskLauncher.StartTime.ToUniversalTime().ToString('o')} |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $taskRepo '.local/channels/resort-process.json') -Encoding utf8
$taskReady=$false;$taskDeadline=[DateTimeOffset]::UtcNow.AddSeconds(45)
do {
    if ($taskLauncher.HasExited) { throw 'CHANNEL_START_FAILED: consulta el registro local; no se borraron mensajes.' }
    try { $taskReady=(Invoke-WebRequest -Uri 'http://127.0.0.1:7454/health/live' -TimeoutSec 1).StatusCode -eq 200 } catch {}
    if (-not $taskReady) { Start-Sleep -Milliseconds 300 }
} while (-not $taskReady -and [DateTimeOffset]::UtcNow -lt $taskDeadline)
if (-not $taskReady) { throw 'CHANNEL_START_TIMEOUT' }
Write-Output 'Canales reiniciados. Inbox/outbox y túnel existente conservados. Un túnel nuevo requiere actualizar el callback en Meta.'
