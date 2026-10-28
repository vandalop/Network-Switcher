@echo off
setlocal
cd /d "%~dp0"
echo ========================================================
echo   Network Switcher - Enable Silent Elevated Startup
echo ========================================================

net session >nul 2>&1
if %ERRORLEVEL% neq 0 (
    echo [NOTICE] Requesting Administrator elevation...
    powershell -Command "Start-Process '%~f0' -Verb RunAs"
    exit /b
)

set EXEPATH=%~dp0..\NetworkSwitcher.exe

if not exist "%EXEPATH%" (
    echo [ERROR] Could not find %EXEPATH%
    pause
    exit /b 1
)

echo Registering Windows Scheduled Task for silent logon startup...
schtasks /Create /TN "NetworkSwitcher" /TR "\"%EXEPATH%\"" /SC ONLOGON /RL HIGHEST /F

if %ERRORLEVEL% equ 0 (
    echo.
    echo [SUCCESS] Network Switcher will now start silently with Windows on logon!
    echo No UAC prompts will appear on reboot.
) else (
    echo.
    echo [ERROR] Failed to create scheduled task.
)

pause
