@echo off
rem Stops the demo containers. Bookings, accounts and configuration are kept.
setlocal
cd /d "%~dp0"
docker compose -f deploy/demo/compose.yaml stop
