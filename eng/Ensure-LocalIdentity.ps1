#requires -Version 7.2
[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$taskRepo=Split-Path -Parent $PSScriptRoot
$taskIssuer='http://localhost:8080/realms/contactcenterai-local'
function Test-LocalIdentity {
 try{return (Invoke-RestMethod -Uri ($taskIssuer+'/.well-known/openid-configuration') -TimeoutSec 8).issuer -ceq $taskIssuer}catch{return $false}
}
if(Test-LocalIdentity){return}
if(-not(Get-Command docker -ErrorAction SilentlyContinue)){throw 'KEYCLOAK_OFFLINE: abre Docker Desktop con el contenedor existente de ContactCenterAI.'}
$taskDockerArgs=@('--config',(Join-Path $taskRepo '.local/docker-client'))
if($IsWindows){$taskDockerArgs+=@('--host','npipe:////./pipe/dockerDesktopLinuxEngine')}else{$taskDockerArgs+=@('--host','unix:///var/run/docker.sock')}
$taskContainer='contactcenterai-local-keycloak-1'
$taskProject=& docker @taskDockerArgs inspect --format '{{index .Config.Labels "com.docker.compose.project"}}' $taskContainer 2>$null
if($LASTEXITCODE -ne 0 -or $taskProject -cne 'contactcenterai-local'){throw 'KEYCLOAK_OFFLINE: Docker no permite acceder al contenedor existente de ContactCenterAI. Abre Docker Desktop y el grupo contactcenterai-local.'}
$taskService=& docker @taskDockerArgs inspect --format '{{index .Config.Labels "com.docker.compose.service"}}' $taskContainer 2>$null
if($LASTEXITCODE -ne 0 -or $taskService -cne 'keycloak'){throw 'KEYCLOAK_CONTAINER_IDENTITY_REJECTED: no se inició ningún contenedor.'}
& docker @taskDockerArgs start $taskContainer | Out-Null
if($LASTEXITCODE -ne 0){throw 'KEYCLOAK_START_FAILED: no se modificaron datos ni credenciales.'}
Write-Output 'Iniciando el servicio de acceso existente; tus cuentas se conservan.'
$taskDeadline=[DateTimeOffset]::UtcNow.AddSeconds(40)
do{
 if(Test-LocalIdentity){return}
 Start-Sleep -Milliseconds 500
}while([DateTimeOffset]::UtcNow -lt $taskDeadline)
throw 'KEYCLOAK_START_TIMEOUT: el issuer local todavía no está disponible.'
