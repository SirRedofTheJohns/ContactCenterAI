@echo off
set "TASK_PWSH=%USERPROFILE%\.cache\codex-runtimes\codex-primary-runtime\dependencies\native\powershell\pwsh.exe"
if exist "%TASK_PWSH%" (
  "%TASK_PWSH%" -NoProfile -File "%~dp0eng\Start-Demo.ps1" -FreshData %*
) else (
  pwsh -NoProfile -File "%~dp0eng\Start-Demo.ps1" -FreshData %*
)
