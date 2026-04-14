@echo off
chcp 65001 >nul
echo ========================================
echo   KiteTodo 打包脚本
echo ========================================
echo.

echo 正在清理进程...
taskkill /IM KiteTodo.exe /F >nul 2>&1
taskkill /IM KiteTodo-Release.exe /F >nul 2>&1

:: 等待进程释放文件
timeout /t 2 /nobreak >nul

:: 清理旧的发布输出
if exist "%~dp0bin\Release" rd /s /q "%~dp0bin\Release" 2>nul
if exist "%~dp0obj\Release" rd /s /q "%~dp0obj\Release" 2>nul

echo 正在编译打包，请稍候...
echo.

dotnet publish "%~dp0KiteTodo.csproj" ^
  -c Release ^
  -r win-x64 ^
  --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:EnableCompressionInSingleFile=true ^
  -p:DebugType=none ^
  -p:DebugSymbols=false

if not %errorlevel%==0 (
    echo.
    echo [错误] 打包失败！请确保没有其他程序占用项目文件。
    echo 提示：关闭 Visual Studio 或其他编辑器后重试。
    pause
    exit /b 1
)

:: 复制到项目根目录
copy /y "%~dp0bin\Release\net8.0-windows10.0.17763.0\win-x64\publish\KiteTodo.exe" "%~dp0KiteTodo-Release.exe" >nul

echo.
echo ========================================
echo   打包完成！
echo   输出文件: KiteTodo-Release.exe
echo ========================================
echo.
pause
