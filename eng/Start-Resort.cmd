@echo off
where pwsh.exe >nul 2>nul
if errorlevel 1 (
  echo PowerShell 7 is required. Run eng/Start-Resort.ps1 with PowerShell 7.
  exit /b 1
)
pwsh.exe -NoProfile -File "%~dp0Start-Resort.ps1" %*
