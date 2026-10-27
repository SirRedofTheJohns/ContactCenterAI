@echo off
rem Starts the self-contained resort demo with Docker. Data is kept between runs.
setlocal
cd /d "%~dp0"
where docker >nul 2>nul || (echo Docker is required. Install Docker Desktop, start it and run this file again. & exit /b 1)
docker info >nul 2>nul || (echo Docker is installed but not running. Start Docker Desktop and run this file again. & exit /b 1)
echo Building and starting the demo. The first run downloads images and packages and can take several minutes...
docker compose -f deploy/demo/compose.yaml up -d --build --wait
if errorlevel 1 (
  echo The demo did not become ready. Diagnose with: docker compose -f deploy/demo/compose.yaml logs
  echo If port 7452 is in use, stop the other program. If 18080 is in use, run: set CCAI_DEMO_IDP_PORT=18081 and start again.
  exit /b 1
)
echo.
echo Demo ready: http://127.0.0.1:7452
echo Fictional users: customer-a, customer-b, operations-admin - password 123456Aa!
start "" http://127.0.0.1:7452
