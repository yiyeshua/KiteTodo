@echo off

if not defined ROOT set "ROOT=%~dp0"

set "BUILD_PROJECT_FILE=%ROOT%KiteTodo.csproj"
set "BUILD_PUBLISH_DIR=%ROOT%bin\Release\net8.0-windows10.0.17763.0\win-x64\publish"
set "BUILD_OUTPUT_EXE=%ROOT%KiteTodo-Release.exe"
set "BUILD_INSTALLER_SCRIPT=%ROOT%installer\KiteTodo.iss"
set "BUILD_INSTALLER_OUTPUT_DIR=%ROOT%installer-output"
set "BUILD_LEGACY_DIR=%ROOT%KiteTodo-Release"
set "BUILD_LEGACY_VLC_DIR=%ROOT%libvlc"

if not defined BUILD_AUTO_LAUNCH set "BUILD_AUTO_LAUNCH=1"
if not defined BUILD_AUTO_PAUSE set "BUILD_AUTO_PAUSE=1"
if not defined BUILD_APP_VERSION set "BUILD_APP_VERSION="

if not defined BUILD_APP_VERSION (
    for /f "usebackq delims=" %%i in (`dotnet msbuild "%BUILD_PROJECT_FILE%" -nologo -getProperty:Version 2^>nul`) do (
        if not "%%i"=="" set "BUILD_APP_VERSION=%%i"
    )
)

if not defined BUILD_APP_VERSION set "BUILD_APP_VERSION=1.0.0"