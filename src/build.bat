@echo off
setlocal
cd /d "%~dp0"
echo ========================================================
echo   Building Network Switcher (Ultra-Lightweight Edition)
echo ========================================================

set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe

if not exist "%CSC%" (
    echo [ERROR] C# compiler not found at %CSC%
    pause
    exit /b 1
)

echo Compiling C# source files...
"%CSC%" /target:winexe /optimize+ /win32manifest:app.manifest /win32icon:app.ico /r:System.Windows.Forms.dll,System.Drawing.dll /out:..\NetworkSwitcher.exe Config.cs StartupManager.cs MemoryHelper.cs NetworkManager.cs IconGenerator.cs ToggleSwitch.cs QuickControlsFlyout.cs TrayApplicationContext.cs Program.cs

if %ERRORLEVEL% equ 0 (
    echo [SUCCESS] NetworkSwitcher.exe built successfully!
    echo Output: %~dp0..\NetworkSwitcher.exe
) else (
    echo [ERROR] Compilation failed.
)

pause
