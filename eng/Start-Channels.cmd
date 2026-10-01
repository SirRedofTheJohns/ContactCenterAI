@echo off
setlocal
set "taskPwsh=%USERPROFILE%\.cache\codex-runtimes\codex-primary-runtime\dependencies\native\powershell\pwsh.exe"
if exist "%taskPwsh%" goto bundled
pwsh.exe -NoProfile -File "%~dp0Start-Channels.ps1" %*
exit /b %errorlevel%
:bundled
"%taskPwsh%" -NoProfile -File "%~dp0Start-Channels.ps1" %*
exit /b %errorlevel%
