@echo off
rem Deletes ONLY this demo's containers and volumes (bookings, sessions, identity data, generated secrets).
setlocal
cd /d "%~dp0"
echo This permanently deletes the demo data of project "contactcenterai-demo".
set /p answer=Type DELETE to continue:
if not "%answer%"=="DELETE" (echo Nothing was deleted. & exit /b 0)
docker compose -f deploy/demo/compose.yaml down -v
echo Demo data deleted. Run start-demo.cmd to create a fresh demo with current dates.
