@echo off
rem Double-click this to build ReWASD.exe (it appears in the "dist" folder).
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1" %*
echo.
pause
