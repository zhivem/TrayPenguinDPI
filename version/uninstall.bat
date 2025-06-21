@echo off
title TrayPenguinDPI Uninstaller
color 0B

:: Очистка экрана
cls

echo.
echo    _______             __         ____        __           __            
echo   / ____(_)______     / /_       / __ \____  / /___  _____/ /_____  _____
echo  / /_  / / ___/ /    / __ \     / /_/ / __ \/ / __ \/ ___/ __/ __ \/ ___/
echo / __/ / (__  ) /____/ /_/ /    / ____/ /_/ / / /_/ / /__/ /_/ /_/ / /    
echo/_/   /_/____/______/_____(_)  /_/    \____/_/\____/\___/\__/\____/_/     
echo.
echo                        [TrayPenguinDPI - Uninstaller]
echo ==============================================================================
echo.
echo     Initializing uninstallation process for TrayPenguinDPI...
echo ==============================================================================
timeout /t 2 /nobreak >nul

echo.
echo [CHECK] Verifying administrator privileges...
net session >nul 2>&1
if %ERRORLEVEL% neq 0 (
    echo.
    echo [WARNING] Administrator rights required!
    echo           Restarting with elevated privileges...
    timeout /t 2 /nobreak >nul
    powershell -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
    exit /b
)
echo.
echo [OK] Administrator privileges confirmed.
timeout /t 1 /nobreak >nul

:: Завершение процессов
echo.
echo [STEP 1] Terminating processes...
call :LoadingBar

echo          - TrayPenguinDPI.exe
taskkill /IM TrayPenguinDPI.exe /F >nul 2>&1
if %ERRORLEVEL% neq 0 (echo          [FAILED] Could not terminate TrayPenguinDPI.exe) else (echo          [OK] Terminated successfully)

echo          - winws.exe
taskkill /IM winws.exe /F >nul 2>&1
if %ERRORLEVEL% neq 0 (echo          [FAILED] Could not terminate winws.exe) else (echo          [OK] Terminated successfully)
timeout /t 1 /nobreak >nul

:: Остановка и удаление служб
echo.
echo [STEP 2] Stopping and removing services...
call :LoadingBar

echo          - Zapret
net stop Zapret >nul 2>&1
sc delete Zapret >nul 2>&1

echo          - WinDivert
net stop "WinDivert
