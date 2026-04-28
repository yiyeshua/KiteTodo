@echo off
setlocal
chcp 65001 >nul

set "ROOT=%~dp0"
call "%ROOT%build-config.bat"

set "BUILD_TARGET=%~1"
if not defined BUILD_TARGET set "BUILD_TARGET=all"
set "PUBLISH_SHOULD_LAUNCH=%BUILD_AUTO_LAUNCH%"
set "PUBLISH_SHOULD_PAUSE=%BUILD_AUTO_PAUSE%"

if /I "%BUILD_TARGET%"=="test" shift
if /I "%BUILD_TARGET%"=="package" shift
if /I "%BUILD_TARGET%"=="all" shift

set "FORWARDED_ARGS="
:collect_args
if "%~1"=="" goto dispatch
set "FORWARDED_ARGS=%FORWARDED_ARGS% %1"
if /I "%~1"=="/no-launch" set "PUBLISH_SHOULD_LAUNCH=0"
if /I "%~1"=="/no-pause" set "PUBLISH_SHOULD_PAUSE=0"
shift
goto collect_args

:dispatch

if /I "%BUILD_TARGET%"=="test" goto run_test
if /I "%BUILD_TARGET%"=="package" goto run_package
if /I "%BUILD_TARGET%"=="all" goto run_all
if /I "%BUILD_TARGET%"=="/help" goto usage
if /I "%BUILD_TARGET%"=="-h" goto usage
if /I "%BUILD_TARGET%"=="--help" goto usage

echo [ERROR] Unknown build target: %BUILD_TARGET%
echo.
goto usage

echo ========================================
echo KiteTodo publish script
echo ========================================
echo.

:run_test
echo Target : test
call "%ROOT%build-singlefile.bat"%FORWARDED_ARGS%
exit /b %errorlevel%

:run_package
echo Target : package
call "%ROOT%build-installer.bat"%FORWARDED_ARGS%
exit /b %errorlevel%

:run_all
echo Target : all
call "%ROOT%build-singlefile.bat" /no-launch /no-pause%FORWARDED_ARGS%
if errorlevel 1 exit /b 1

call "%ROOT%build-installer.bat" /no-pause%FORWARDED_ARGS%
if errorlevel 1 exit /b 1

echo Full publish finished.
echo.
echo Output EXE : %BUILD_OUTPUT_EXE%
if exist "%BUILD_INSTALLER_OUTPUT_DIR%" echo Output installer dir : installer-output\
echo.

if "%PUBLISH_SHOULD_LAUNCH%"=="1" (
    echo Press any key to launch the latest build...
    pause >nul
)

if exist "%BUILD_OUTPUT_EXE%" if "%PUBLISH_SHOULD_LAUNCH%"=="1" (
    start "" "%BUILD_OUTPUT_EXE%"
    exit /b 0
)

if exist "%BUILD_OUTPUT_EXE%" exit /b 0

echo [ERROR] Release EXE not found: KiteTodo-Release.exe
if "%PUBLISH_SHOULD_PAUSE%"=="1" pause
exit /b 1

:usage
echo Usage:
echo   publish.bat test [options]
echo   publish.bat package [options]
echo   publish.bat all [options]
echo.
echo Shared config file: build-config.bat
echo   BUILD_APP_VERSION
echo   BUILD_OUTPUT_EXE
echo   BUILD_INSTALLER_OUTPUT_DIR
echo   BUILD_AUTO_LAUNCH
echo   BUILD_AUTO_PAUSE
exit /b 1
