@echo off
set "TASK_PS=%USERPROFILE%\.cache\codex-runtimes\codex-primary-runtime\dependencies\native\powershell\pwsh.exe"
if not exist "%TASK_PS%" set "TASK_PS=pwsh.exe"
"%TASK_PS%" -NoProfile -File "%~dp0eng\Start-Demo.ps1" %*
if errorlevel 1 pause
