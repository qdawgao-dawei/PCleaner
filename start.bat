@echo off
chcp 65001 > nul
setlocal enabledelayedexpansion

echo 正在编译 PCleaner...
dotnet build -c Release

if %errorlevel% neq 0 (
    echo [错误] 编译失败，请检查代码。
    pause
    exit /b %errorlevel%
)

echo 编译成功，正在启动...
start "" "bin\Release\net8.0-windows\PCleaner.exe"
exit
