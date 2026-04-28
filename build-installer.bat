@echo off
setlocal
chcp 65001 >nul

set "ROOT=%~dp0"
call "%ROOT%build-config.bat"

set "ISCC="
set "SHOULD_PAUSE=%BUILD_AUTO_PAUSE%"

for %%a in (%*) do (
    if /I "%%~a"=="/no-pause" set "SHOULD_PAUSE=0"
)

if exist "%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe" set "ISCC=%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe"
if exist "%ProgramFiles%\Inno Setup 6\ISCC.exe" set "ISCC=%ProgramFiles%\Inno Setup 6\ISCC.exe"
if exist "D:\Program Files (x86)\Inno Setup 6\ISCC.exe" set "ISCC=D:\Program Files (x86)\Inno Setup 6\ISCC.exe"
if exist "D:\Program Files\Inno Setup 6\ISCC.exe" set "ISCC=D:\Program Files\Inno Setup 6\ISCC.exe"
if not defined ISCC for /f "delims=" %%i in ('where ISCC.exe 2^>nul') do if not defined ISCC set "ISCC=%%i"

echo ========================================
echo KiteTodo installer build script
echo ========================================
echo.

if not exist "%BUILD_OUTPUT_EXE%" (
    echo [ERROR] Single-file EXE not found: KiteTodo-Release.exe
    echo Run build-singlefile.bat first, then retry installer build.
    if "%SHOULD_PAUSE%"=="1" pause
    exit /b 1
)

if not exist "%BUILD_INSTALLER_SCRIPT%" (
    echo [ERROR] Installer script not found: installer\KiteTodo.iss
    if "%SHOULD_PAUSE%"=="1" pause
    exit /b 1
)

if not defined ISCC (
    echo [ERROR] Inno Setup compiler not found.
    echo Install Inno Setup 6 or add ISCC.exe to PATH, then retry.
    if "%SHOULD_PAUSE%"=="1" pause
    exit /b 1
)

echo [1/2] Cleaning installer output...
if exist "%BUILD_INSTALLER_OUTPUT_DIR%" rd /s /q "%BUILD_INSTALLER_OUTPUT_DIR%" 2>nul
if not exist "%BUILD_INSTALLER_OUTPUT_DIR%" mkdir "%BUILD_INSTALLER_OUTPUT_DIR%"

echo [2/2] Building installer...
"%ISCC%" /DMyAppVersion=%BUILD_APP_VERSION% /DMySourceExe="%BUILD_OUTPUT_EXE%" /DMyOutputDir="%BUILD_INSTALLER_OUTPUT_DIR%" "%BUILD_INSTALLER_SCRIPT%"
if errorlevel 1 (
    echo [ERROR] Installer build failed.
    if "%SHOULD_PAUSE%"=="1" pause
    exit /b 1
)

echo.
echo Installer build finished.
echo Output installer dir : installer-output\

if "%SHOULD_PAUSE%"=="1" pause
exit /b 0
