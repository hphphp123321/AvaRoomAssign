@echo off
chcp 65001 >nul
echo ====================================
echo   AvaRoomAssign 单文件AOT编译脚本
echo ====================================
echo.

echo [1/4] 清理项目...
dotnet clean AvaRoomAssign/AvaRoomAssign.csproj
if %errorlevel% neq 0 (
    echo 清理失败！
    pause
    exit /b 1
)

echo.
echo [2/4] 终止可能运行的进程...
taskkill /F /IM AvaRoomAssign.exe 2>nul || echo 没有找到运行中的进程

echo.
echo [3/4] 开始单文件AOT编译... (这可能需要5-10分钟)
echo 正在编译为单个可执行文件，请耐心等待...
dotnet publish AvaRoomAssign/AvaRoomAssign.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:PublishAot=true
if %errorlevel% neq 0 (
    echo 单文件AOT编译失败！
    pause
    exit /b 1
)

echo.
echo [4/4] 编译完成！
echo.
echo 输出文件位置：
echo %~dp0AvaRoomAssign\bin\Release\net9.0\win-x64\publish\AvaRoomAssign.exe
echo.

set "publish_dir=%~dp0AvaRoomAssign\bin\Release\net9.0\win-x64\publish"
if exist "%publish_dir%\AvaRoomAssign.exe" (
    echo 🎉 单文件编译成功！
    echo.
    echo 文件信息：
    for %%I in ("%publish_dir%\AvaRoomAssign.exe") do (
        echo   文件大小: %%~zI 字节
        set /a sizeMB=%%~zI/1024/1024
        echo   文件大小: !sizeMB! MB
    )
    echo.
    echo 发布目录文件列表：
    dir "%publish_dir%" /b
    echo.
    echo ✅ 现在您只需要一个 AvaRoomAssign.exe 文件就能运行程序！
    echo ✅ 无需任何依赖，可以在任何Windows电脑上运行！
) else (
    echo 错误：未找到编译后的可执行文件！
)

echo.
echo ====================================
echo 是否要打开发布目录？(Y/N)
set /p choice=请选择: 
if /i "%choice%"=="Y" (
    explorer "%publish_dir%"
)

echo.
echo 是否要立即测试运行程序？(Y/N)
set /p testChoice=请选择: 
if /i "%testChoice%"=="Y" (
    echo 正在启动程序...
    start "" "%publish_dir%\AvaRoomAssign.exe"
)

pause 