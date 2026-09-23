@echo off
setlocal

REM Double-click or run from cmd to create a self-contained TissueTrainer build.
REM Requires .NET 8 SDK on this build machine only.

set SCRIPT_DIR=%~dp0
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_DIR%publish-self-contained.ps1" %*

if errorlevel 1 (
    echo.
    echo Publish failed.
    exit /b 1
)

echo.
pause
