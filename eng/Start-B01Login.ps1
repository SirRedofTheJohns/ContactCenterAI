#requires -Version 7.2
[CmdletBinding()]
param(
    [switch]$TrustLocalCertificate,
    [switch]$BuildOnly,
    [string]$DotNetExe,
    [ValidateRange(1, 30)][int]$LoginTimeoutMinutes = 15
)
$ErrorActionPreference = 'Stop'
function Write-B01TlsFailure([Exception]$TaskException) {
    for ($taskCause = $TaskException; $null -ne $taskCause; $taskCause = $taskCause.InnerException) {
        Write-Output ('TLS_CLIENT Type=' + $taskCause.GetType().Name + '; HResult=0x' + $taskCause.HResult.ToString('X8'))
        if ($taskCause -is [ComponentModel.Win32Exception]) {
            Write-Output ('TLS_CLIENT NativeCode=0x' + $taskCause.NativeErrorCode.ToString('X8'))
        }
    }
}
function Write-B01ServerCodes([string]$TaskLogPath) {
    if (Test-Path -LiteralPath $TaskLogPath) {
        Get-Content -LiteralPath $TaskLogPath | Where-Object { $_ -match '^TLS_(SERVER|KEY|CERTIFICATE) ' } | ForEach-Object { Write-Output $_ }
    }
}
$taskRepo = Split-Path -Parent $PSScriptRoot
$taskWorkspace = Split-Path -Parent (Split-Path -Parent $taskRepo)
if (-not $BuildOnly -and (-not $IsWindows -or -not $TrustLocalCertificate)) {
    throw 'B01_TEMPORARY_TRUST_REQUIRED: on Windows use -TrustLocalCertificate to trust only this run''s localhost certificate in CurrentUser Root. The helper removes it when it exits normally and clears its synthetic password from the clipboard.'
}
if (-not $DotNetExe) {
    $DotNetExe = Join-Path $taskWorkspace 'work/runtimes/dotnet/dotnet.exe'
    if (-not (Test-Path -LiteralPath $DotNetExe)) { $DotNetExe = 'dotnet' }
}
$taskBuildArgs = @{ DotNetExe = $DotNetExe; OfflineOnly = $true }
$taskFeed = Join-Path $taskWorkspace 'work/nuget-feed'
if (Test-Path -LiteralPath $taskFeed) { $taskBuildArgs.PackageSource = $taskFeed }
& (Join-Path $PSScriptRoot 'Invoke-B01.ps1') @taskBuildArgs
if ($BuildOnly) { return }

$taskEdge = @(
    (Join-Path ${env:ProgramFiles(x86)} 'Microsoft/Edge/Application/msedge.exe'),
    (Join-Path $env:ProgramFiles 'Microsoft/Edge/Application/msedge.exe')
) | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $taskEdge) { throw 'EDGE_NOT_FOUND: use the manual OIDC runbook.' }
if ([Net.NetworkInformation.IPGlobalProperties]::GetIPGlobalProperties().GetActiveTcpListeners().Port -contains 7443) {
    throw 'PORT_7443_IN_USE: stop your previous B01 diagnostic before retrying. No existing process was stopped.'
}
try {
    $taskDiscovery = Invoke-RestMethod -Uri 'http://localhost:8080/realms/contactcenterai-local/.well-known/openid-configuration' -TimeoutSec 5
}
catch { throw 'KEYCLOAK_UNREACHABLE: start the existing local containers before this login check.' }
if ($taskDiscovery.issuer -cne 'http://localhost:8080/realms/contactcenterai-local') { throw 'OIDC_ISSUER_MISMATCH' }

$taskEnvFile = Join-Path $taskRepo 'deploy/local/.env'
if (-not (Test-Path -LiteralPath $taskEnvFile)) { throw 'LOCAL_ENV_MISSING' }
$taskSettings = @{}
foreach ($taskName in @('CCAI_BFF_CLIENT_SECRET', 'CCAI_SYNTHETIC_USER_PASSWORD')) {
    $taskLines = @(Get-Content -LiteralPath $taskEnvFile | Where-Object { $_.StartsWith($taskName + '=', [StringComparison]::Ordinal) })
    if ($taskLines.Count -ne 1) { throw 'LOCAL_LOGIN_SETTING_INVALID' }
    $taskSettings[$taskName] = $taskLines[0].Substring($taskName.Length + 1)
    if ($taskSettings[$taskName].Length -lt 32) { throw 'LOCAL_LOGIN_SETTING_INVALID' }
}
$taskRun = [Guid]::NewGuid().ToString('D')
$taskDirectory = Join-Path $taskRepo '.local/b01-login'
New-Item -ItemType Directory -Path $taskDirectory -Force | Out-Null
$taskReportPath = Join-Path $taskRepo 'docs/progress/b01-login-evidence.json'
if (Test-Path -LiteralPath $taskReportPath) {
    # Preserve an earlier report locally so a failed rerun cannot leave a stale public PASS.
    Move-Item -LiteralPath $taskReportPath -Destination (Join-Path $taskDirectory ('previous-report-' + $taskRun + '.json'))
}
$taskAssembly = Join-Path $taskRepo 'spikes/RuntimeCompatibility/bin/Release/net10.0/RuntimeCompatibility.dll'
$taskServer = $null
$taskCertificate = $null
$taskAddedCertificate = $false
$taskPreviousEnvironment = @{}
try {
    # Only the child receives the client secret; no secret in command arguments or output.
    $taskChildEnvironment = @{
        CCAI_BFF_CLIENT_SECRET = $taskSettings.CCAI_BFF_CLIENT_SECRET
        CCAI_B01_LOGIN_RUN_ID = $taskRun
        DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    }
    try {
        foreach ($taskName in $taskChildEnvironment.Keys) {
            $taskPreviousEnvironment[$taskName] = [Environment]::GetEnvironmentVariable($taskName, 'Process')
            [Environment]::SetEnvironmentVariable($taskName, $taskChildEnvironment[$taskName], 'Process')
        }
        $taskServer = Start-Process -FilePath $DotNetExe -ArgumentList @(('"' + $taskAssembly + '"'), '--serve-login') -WorkingDirectory $taskRepo -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $taskDirectory 'server.out.log') -RedirectStandardError (Join-Path $taskDirectory 'server.err.log')
    }
    finally {
        foreach ($taskName in $taskPreviousEnvironment.Keys) {
            [Environment]::SetEnvironmentVariable($taskName, $taskPreviousEnvironment[$taskName], 'Process')
        }
    }
    $taskReady = $null
    $taskDeadline = [DateTimeOffset]::UtcNow.AddSeconds(30)
    while ([DateTimeOffset]::UtcNow -lt $taskDeadline) {
        if ($taskServer.HasExited) {
            Write-B01ServerCodes (Join-Path $taskDirectory 'server.out.log')
            throw 'OIDC_DIAGNOSTIC_START_FAILED: see certificate codes above; no login was validated.'
        }
        $taskReadyPath = Join-Path $taskDirectory 'ready.json'
        if (Test-Path -LiteralPath $taskReadyPath) {
            try { $taskCandidate = Get-Content -LiteralPath $taskReadyPath -Raw | ConvertFrom-Json } catch { $taskCandidate = $null }
            if ($taskCandidate.runId -ceq $taskRun -and $taskCandidate.processId -eq $taskServer.Id) { $taskReady = $taskCandidate; break }
        }
        Start-Sleep -Milliseconds 300
    }
    if (-not $taskReady) { throw 'OIDC_DIAGNOSTIC_START_TIMEOUT' }
    $taskCertificate = [Security.Cryptography.X509Certificates.X509Certificate2]::new((Join-Path $taskRepo '.local/diagnostic-certs/localhost.cer'))
    if ($taskCertificate.Thumbprint -cne $taskReady.certificateThumbprint -or $taskCertificate.Subject -cne 'CN=localhost' -or $taskCertificate.HasPrivateKey) {
        throw 'DIAGNOSTIC_CERTIFICATE_MISMATCH'
    }
    $taskStore = [Security.Cryptography.X509Certificates.X509Store]::new('Root', 'CurrentUser')
    try {
        $taskStore.Open([Security.Cryptography.X509Certificates.OpenFlags]::ReadWrite)
        if (-not $taskStore.Certificates.Contains($taskCertificate)) {
            $taskStore.Add($taskCertificate)
            $taskAddedCertificate = $true
        }
    }
    finally { $taskStore.Dispose() }
    Write-Output 'Se confía temporalmente en el certificado localhost de esta ejecución; se retirará al terminar.'
    try {
        $taskHandler = [Net.Http.HttpClientHandler]::new()
        $taskHandler.UseProxy = $false
        $taskHandler.AllowAutoRedirect = $false
        $taskHttp = [Net.Http.HttpClient]::new($taskHandler)
        $taskHttp.Timeout = [TimeSpan]::FromSeconds(10)
        $taskAnonymous = $taskHttp.GetAsync('https://localhost:7443/proof').GetAwaiter().GetResult()
        if ([int]$taskAnonymous.StatusCode -ne 401) { throw 'ANONYMOUS_PROOF_NOT_DENIED' }
    }
    catch {
        Write-B01TlsFailure $_.Exception
        Start-Sleep -Milliseconds 300
        Write-B01ServerCodes (Join-Path $taskDirectory 'server.out.log')
        throw 'DIAGNOSTIC_HTTPS_FAILED: see TLS_CLIENT/TLS_SERVER codes above; certificate validation remains enabled.'
    }
    finally {
        if ($taskAnonymous) { $taskAnonymous.Dispose() }
        if ($taskHttp) { $taskHttp.Dispose() }
        if ($taskHandler) { $taskHandler.Dispose() }
    }
    Write-Output 'PASS: acceso sin sesión rechazado (HTTP 401) mediante HTTPS validado.'

    $taskResults = $null
    foreach ($taskAccount in @('customer-a', 'customer-b')) {
        Set-Clipboard -Value $taskSettings.CCAI_SYNTHETIC_USER_PASSWORD
        Write-Output ('Se abrirá ' + $taskAccount + '. Pega la contraseña con Ctrl+V y pulsa Sign In. La contraseña es sintética y no se imprime.')
        Write-Output ('Si Keycloak pide completar perfil, usa nombre Customer, apellido ' + $taskAccount.Substring($taskAccount.Length - 1).ToUpperInvariant() + ' y email ' + $taskAccount + '@example.invalid (datos ficticios).')
        $taskProfile = Join-Path $taskRepo ('.local/b01-browser/' + $taskRun + '/' + $taskAccount)
        $taskBrowserArgs = @('--new-window', '--no-first-run', '--inprivate', ('--user-data-dir="' + $taskProfile + '"'), ('https://localhost:7443/login?account=' + $taskAccount))
        # Visible because the user must complete an interactive login in a separate local profile.
        Start-Process -FilePath $taskEdge -ArgumentList $taskBrowserArgs | Out-Null
        $taskDeadline = [DateTimeOffset]::UtcNow.AddMinutes($LoginTimeoutMinutes)
        $taskAccountPassed = $false
        while ([DateTimeOffset]::UtcNow -lt $taskDeadline) {
            if ($taskServer.HasExited) { throw 'OIDC_DIAGNOSTIC_STOPPED: no complete report recorded.' }
            $taskResultPath = Join-Path $taskDirectory 'results.json'
            if (Test-Path -LiteralPath $taskResultPath) {
                try { $taskCandidate = Get-Content -LiteralPath $taskResultPath -Raw | ConvertFrom-Json } catch { $taskCandidate = $null }
                if ($taskCandidate.runId -ceq $taskRun) {
                    $taskRow = @($taskCandidate.accounts | Where-Object { $_.account -ceq $taskAccount })
                    if ($taskRow.Count -eq 1 -and $taskRow[0].failed) { throw ('OIDC_LOGIN_FAILED: ' + $taskAccount + '; no tokens or provider errors exported.') }
                    if ($taskRow.Count -eq 1 -and $taskRow[0].cookieRoundtrip -and -not $taskRow[0].passed) { throw ('OIDC_LOGIN_EVIDENCE_INCOMPLETE: ' + $taskAccount + '; see sanitized .local/b01-login/results.json.') }
                    if ($taskRow.Count -eq 1 -and $taskRow[0].passed) { $taskResults = $taskCandidate; $taskAccountPassed = $true; break }
                }
            }
            Start-Sleep -Seconds 1
        }
        if (-not $taskAccountPassed) { throw ('OIDC_LOGIN_TIMEOUT: ' + $taskAccount + '; B01 remains pending.') }
        Write-Output ('PASS: ' + $taskAccount + ', subject esperado, rol Customer, Code+PKCE y cookie HTTPS.')
    }
    if (@($taskResults.accounts | Where-Object { $_.passed }).Count -ne 2) { throw 'TWO_ACCOUNT_EVIDENCE_INCOMPLETE' }
    $taskReport = [ordered]@{
        schemaVersion = 1
        ticket = 'B01'
        result = 'PASS'
        source = 'Interactive user browser, real local Keycloak and diagnostic middleware'
        runId = $taskRun
        observedAtUtc = $taskResults.observedAtUtc
        anonymousProofStatus = 401
        httpsCertificateValidation = 'Enabled; temporary CurrentUser localhost trust'
        issuer = 'http://localhost:8080/realms/contactcenterai-local'
        clientId = 'contactcenterai-bff'
        accounts = $taskResults.accounts
        scope = $taskResults.scope
        productAuthorizationTested = $false
    }
    $taskReport | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $taskReportPath -Encoding utf8
    Write-Output 'PASS: ambas cuentas verificadas. Evidencia sin secretos guardada en docs/progress/b01-login-evidence.json.'
    Write-Output 'Puedes cerrar las dos ventanas de prueba. El resultado permite revisar el cierre de B01.'
}
finally {
    try {
        if ($taskServer -and -not $taskServer.HasExited) {
            # Let .NET dispose the certificate and delete its temporary Windows key container.
            [IO.File]::WriteAllText((Join-Path $taskDirectory ('stop-' + $taskRun)), 'stop')
            if (-not $taskServer.WaitForExit(10000)) {
                $taskServer.Kill()
                $taskServer.WaitForExit(5000) | Out-Null
                Write-Output 'El servidor no respondió a la parada normal y fue detenido; la limpieza de su clave temporal no está confirmada.'
            }
        }
    }
    catch { Write-Output 'No se pudo detener el proceso diagnóstico propio; revisa el puerto 7443. Continúa la retirada del certificado.' }
    if ($taskAddedCertificate -and $taskCertificate) {
        $taskStore = [Security.Cryptography.X509Certificates.X509Store]::new('Root', 'CurrentUser')
        try {
            $taskStore.Open([Security.Cryptography.X509Certificates.OpenFlags]::ReadWrite)
            $taskStore.Remove($taskCertificate)
            Write-Output 'Certificado temporal retirado.'
        }
        catch { Write-Output 'No se pudo retirar el certificado; usa su thumbprint en .local/b01-login/ready.json para la retirada manual indicada en el runbook.' }
        finally { $taskStore.Dispose() }
    }
    try {
        if ((Get-Clipboard -Raw) -ceq $taskSettings.CCAI_SYNTHETIC_USER_PASSWORD) { Set-Clipboard -Value '' }
    }
    catch { Write-Output 'No se pudo limpiar el portapapeles; borra la contraseña sintética antes de compartir contenido.' }
    if ($taskCertificate) { $taskCertificate.Dispose() }
    if ($taskServer) { $taskServer.Dispose() }
}
