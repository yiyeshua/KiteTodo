@echo off
setlocal
chcp 65001 >nul

set "ROOT=%~dp0"
call "%ROOT%build-config.bat"

set "SHOULD_LAUNCH=%BUILD_AUTO_LAUNCH%"
set "SHOULD_PAUSE=%BUILD_AUTO_PAUSE%"

for %%a in (%*) do (
    if /I "%%~a"=="/no-launch" set "SHOULD_LAUNCH=0"
    if /I "%%~a"=="/no-pause" set "SHOULD_PAUSE=0"
)

echo ========================================
echo KiteTodo single-file build script
echo ========================================
echo.

echo [1/4] Stopping running processes...
taskkill /IM KiteTodo.exe /F >nul 2>&1
taskkill /IM KiteTodo-Release.exe /F >nul 2>&1
timeout /t 2 /nobreak >nul

echo [2/4] Cleaning old Release output...
if exist "%ROOT%bin\Release" rd /s /q "%ROOT%bin\Release" 2>nul
if exist "%ROOT%obj\Release" rd /s /q "%ROOT%obj\Release" 2>nul
if exist "%ROOT%obj\embedded\Release" rd /s /q "%ROOT%obj\embedded\Release" 2>nul
if exist "%BUILD_OUTPUT_EXE%" del /f /q "%BUILD_OUTPUT_EXE%" 2>nul
if exist "%BUILD_LEGACY_DIR%" rd /s /q "%BUILD_LEGACY_DIR%" 2>nul
if exist "%BUILD_LEGACY_VLC_DIR%" rd /s /q "%BUILD_LEGACY_VLC_DIR%" 2>nul

echo [3/4] Publishing single-file application...
dotnet publish "%BUILD_PROJECT_FILE%" ^
  -c Release ^
  -r win-x64 ^
  --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:EnableCompressionInSingleFile=true ^
  -p:DebugType=none ^
  -p:DebugSymbols=false

if errorlevel 1 (
    echo.
    echo [ERROR] Single-file publish failed.
    echo Close any running KiteTodo process and try again.
    if "%SHOULD_PAUSE%"=="1" pause
    exit /b 1
)

if not exist "%BUILD_PUBLISH_DIR%\KiteTodo.exe" (
    echo.
    echo [ERROR] Publish output not found: %BUILD_PUBLISH_DIR%\KiteTodo.exe
    if "%SHOULD_PAUSE%"=="1" pause
    exit /b 1
)

echo [4/4] Copying single-file executable...
copy /y "%BUILD_PUBLISH_DIR%\KiteTodo.exe" "%BUILD_OUTPUT_EXE%" >nul

echo.
echo Single-file build finished.
echo Output EXE : %BUILD_OUTPUT_EXE%
echo Network radio runtime will self-extract on first use.
echo.

if "%SHOULD_LAUNCH%"=="1" (
    echo Press any key to launch the latest build...
    pause >nul
    if exist "%BUILD_OUTPUT_EXE%" start "" "%BUILD_OUTPUT_EXE%"
)

if "%SHOULD_LAUNCH%"=="0" if "%SHOULD_PAUSE%"=="1" (
    pause
)

exit /b 0
