@echo off
title TrayPenguinDPI Uninstaller
color 0C

:: Проверка на администратора
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

:: Завершение процессов
echo [STEP 1] Terminating processes...
echo          - TrayPenguinDPI.exe
taskkill /IM TrayPenguinDPI.exe /F >nul 2>&1
if %ERRORLEVEL% neq 0 (echo          [FAILED] Could not terminate TrayPenguinDPI.exe) else (echo          [OK] Terminated successfully)

echo          - winws.exe
taskkill /IM winws.exe /F >nul 2>&1
if %ERRORLEVEL% neq 0 (echo          [FAILED] Could not terminate winws.exe) else (echo          [OK] Terminated successfully)
timeout /t 1 /nobreak >nul

:: Остановка и удаление служб
echo [STEP 2] Stopping and removing services...
echo          - Zapret
net stop Zapret >nul 2>&1
sc delete Zapret >nul 2>&1

echo          - WinDivert
net stop "WinDivert" >nul 2>&1
sc delete "WinDivert" >nul 2>&1

echo          - WinDivert14
net stop "WinDivert14" >nul 2>&1
sc delete "WinDivert14" >nul 2>&1

echo          [OK] Services handled (if existed).
timeout /t 1 /nobreak >nul

:: Ждём, чтобы освободились файлы
echo [INFO] Waiting for processes to release files...
timeout /t 5 /nobreak >nul

:: Удаление всех файлов и папок, кроме этого .bat файла
echo [STEP 3] Cleaning current directory...

:: Удалить все папки
for /d %%i in (*) do rd /s /q "%%i" 2>nul

:: Удалить все файлы, кроме самого .bat
for %%i in (*) do if /i not "%%i"=="%~nx0" del /f /q "%%i" 2>nul

if %ERRORLEVEL% neq 0 (
    echo          [FAILED] Could not remove some files. They may be locked.
) else (
    echo          [OK] Directory cleaned successfully
)
timeout /t 1 /nobreak >nul

:: Самоуничтожение
echo [STEP 4] Self-destructing script...
del "%~f0"
exit
