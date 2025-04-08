@echo off
title TreyPenguinDPI Update
color 0A

:: Header
echo ==========================================
echo       TreyPenguinDPI Update Tool
echo ==========================================
echo Starting update process...
timeout /t 1 /nobreak >nul

:: Check for administrator rights
echo [CHECK] Verifying administrator privileges...
net session >nul 2>&1
if %ERRORLEVEL% neq 0 (
    echo [WARNING] Administrator rights required!
    echo           Restarting with elevated privileges...
    powershell -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
    exit /b
)
echo [OK] Administrator privileges confirmed.
timeout /t 1 /nobreak >nul

:: Terminate processes
echo [STEP 1] Terminating processes...
echo          - TrayPenguinDPI.exe
taskkill /IM TreyPenguinDPI.exe /F >nul 2>&1
if %ERRORLEVEL% neq 0 (echo          [FAILED] Could not terminate TrayPenguinDPI.exe) else (echo          [OK] Terminated successfully)
echo          - winws.exe
taskkill /IM winws.exe /F >nul 2>&1
if %ERRORLEVEL% neq 0 (echo          [FAILED] Could not terminate winws.exe) else (echo          [OK] Terminated successfully)
timeout /t 1 /nobreak >nul

:: Stop and delete services
echo [STEP 2] Stopping and removing services...
echo          - Zapret
net stop Zapret 2>nul
sc delete Zapret 2>nul
echo          - WinDivert
net stop "WinDivert" 2>nul
sc delete "WinDivert" 2>nul
echo          - WinDivert14
net stop "WinDivert14" 2>nul
sc delete "WinDivert14" 2>nul
echo          [OK] Services handled (if existed).
timeout /t 1 /nobreak >nul

:: Wait for processes to fully terminate
echo [INFO] Waiting for processes to release files...
timeout /t 5 /nobreak >nul

:: Clean directory
echo [STEP 3] Cleaning current directory...
:: Удаляем все папки
for /d %%i in (*) do rd /s /q "%%i" 2>nul
:: Удаляем все файлы, кроме самого скрипта
for %%i in (*) do if /i not "%%i"=="%~nx0" del /f /q "%%i" 2>nul
if %ERRORLEVEL% neq 0 (
    echo          [FAILED] Could not remove some files. Check update_error.log
    echo Error: Failed to remove some files. They might be in use. > update_error.log
) else (
    echo          [OK] Directory cleaned successfully
)
timeout /t 1 /nobreak >nul

:: Download new version
echo [STEP 4] Downloading new version...
powershell -Command "Invoke-WebRequest -Uri 'https://github.com/zhivem/TrayPenguinDPI/raw/refs/heads/master/update/program.zip' -OutFile 'program.zip'" >nul 2>&1
if %ERRORLEVEL% neq 0 (
    echo          [FAILED] Download failed. See update_error.log
    echo Error: Failed to download archive. >> update_error.log
    exit /b 1
) else (
    echo          [OK] Download completed
)
timeout /t 1 /nobreak >nul

:: Extract archive
echo [STEP 5] Extracting new version...
powershell -Command "Expand-Archive -Path 'program.zip' -DestinationPath '.' -Force" >nul 2>&1
if %ERRORLEVEL% neq 0 (
    echo          [FAILED] Extraction failed. See update_error.log
    echo Error: Failed to extract archive. >> update_error.log
    del program.zip
    exit /b 1
) else (
    echo          [OK] Extraction completed
)
timeout /t 1 /nobreak >nul

:: Remove temporary file
echo [STEP 6] Removing temporary files...
del program.zip >nul 2>&1
echo          [OK] Temporary files removed
timeout /t 1 /nobreak >nul

:: Launch updated program
echo [STEP 7] Launching TreyPenguinDPI...
start TrayPenguinDPI.exe
echo          [OK] Program launched

:: Finalize
echo [FINISH] Update completed successfully!
echo          Self-destructing script...
timeout /t 2 /nobreak >nul
del "%~f0"
