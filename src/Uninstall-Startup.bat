@echo off
setlocal
cd /d "%~dp0"
echo ========================================================
echo   Network Switcher - Disable Startup
echo ========================================================

net session >nul 2>&1
if %ERRORLEVEL% neq 0 (
    echo [NOTICE] Requesting Administrator elevation...
    powershell -Command "Start-Process '%~f0' -Verb RunAs"
    exit /b
)

echo Removing Windows Scheduled Task...
schtasks /Delete /TN "NetworkSwitcher" /F

if %ERRORLEVEL% equ 0 (
    echo [SUCCESS] Startup task removed.
) else (
    echo [NOTICE] Task was not registered or already removed.
)

pause
