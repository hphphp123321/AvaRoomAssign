# AvaRoomAssign 单文件AOT编译脚本
# 作者: Claude
# 用途: 自动化单文件AOT编译和打包流程

param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [switch]$OpenFolder = $false,
    [switch]$SkipClean = $false,
    [switch]$TestRun = $false
)

# 设置控制台编码
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

Write-Host "====================================" -ForegroundColor Cyan
Write-Host "  AvaRoomAssign 单文件AOT编译脚本" -ForegroundColor Cyan
Write-Host "====================================" -ForegroundColor Cyan
Write-Host ""

# 项目路径
$ProjectPath = "AvaRoomAssign/AvaRoomAssign.csproj"
$PublishPath = "AvaRoomAssign/bin/$Configuration/net9.0/$Runtime/publish"

try {
    # 步骤 1: 清理项目
    if (-not $SkipClean) {
        Write-Host "[1/4] 清理项目..." -ForegroundColor Yellow
        dotnet clean $ProjectPath
        if ($LASTEXITCODE -ne 0) {
            throw "清理项目失败"
        }
        Write-Host "✓ 清理完成" -ForegroundColor Green
    } else {
        Write-Host "[1/4] 跳过清理步骤" -ForegroundColor Gray
    }

    # 步骤 2: 终止进程
    Write-Host ""
    Write-Host "[2/4] 检查并终止运行中的进程..." -ForegroundColor Yellow
    $processes = Get-Process -Name "AvaRoomAssign" -ErrorAction SilentlyContinue
    if ($processes) {
        $processes | Stop-Process -Force
        Write-Host "✓ 已终止 $($processes.Count) 个进程" -ForegroundColor Green
    } else {
        Write-Host "✓ 没有运行中的进程" -ForegroundColor Green
    }

    # 步骤 3: 单文件AOT编译
    Write-Host ""
    Write-Host "[3/4] 开始单文件AOT编译..." -ForegroundColor Yellow
    Write-Host "配置: $Configuration" -ForegroundColor Gray
    Write-Host "运行时: $Runtime" -ForegroundColor Gray
    Write-Host "模式: 单文件 + AOT" -ForegroundColor Gray
    Write-Host "这可能需要5-10分钟，请耐心等待..." -ForegroundColor Gray
    Write-Host ""
    
    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    dotnet publish $ProjectPath -c $Configuration -r $Runtime --self-contained -p:PublishSingleFile=true -p:PublishAot=true
    $stopwatch.Stop()
    
    if ($LASTEXITCODE -ne 0) {
        throw "单文件AOT编译失败"
    }
    
    Write-Host "✓ 单文件AOT编译完成 (耗时: $($stopwatch.Elapsed.TotalMinutes.ToString('F1'))分钟)" -ForegroundColor Green

    # 步骤 4: 验证结果
    Write-Host ""
    Write-Host "[4/4] 验证编译结果..." -ForegroundColor Yellow
    
    $exePath = Join-Path $PublishPath "AvaRoomAssign.exe"
    if (Test-Path $exePath) {
        $fileInfo = Get-Item $exePath
        $fileSize = [math]::Round($fileInfo.Length / 1MB, 2)
        
        Write-Host "🎉 单文件编译成功！" -ForegroundColor Green
        Write-Host ""
        Write-Host "单文件信息:" -ForegroundColor Cyan
        Write-Host "  路径: $exePath" -ForegroundColor White
        Write-Host "  大小: $fileSize MB" -ForegroundColor White
        Write-Host "  创建时间: $($fileInfo.CreationTime)" -ForegroundColor White
        
        # 检查发布目录中的其他文件
        $allFiles = Get-ChildItem $PublishPath -File
        $otherFiles = $allFiles | Where-Object { $_.Name -ne "AvaRoomAssign.exe" -and $_.Name -ne "AvaRoomAssign.pdb" }
        
        Write-Host ""
        if ($otherFiles.Count -eq 0) {
            Write-Host "✅ 真正的单文件发布！只有一个可执行文件" -ForegroundColor Green
        } else {
            Write-Host "附加文件 ($($otherFiles.Count) 个):" -ForegroundColor Yellow
            $otherFiles | ForEach-Object {
                $size = [math]::Round($_.Length / 1KB, 1)
                Write-Host "  $($_.Name) ($size KB)" -ForegroundColor Gray
            }
        }
        
        Write-Host ""
        Write-Host "📋 部署说明:" -ForegroundColor Cyan
        Write-Host "  ✅ 只需复制 AvaRoomAssign.exe 到目标机器" -ForegroundColor White
        Write-Host "  ✅ 无需安装 .NET 运行时" -ForegroundColor White
        Write-Host "  ✅ 无需其他依赖文件" -ForegroundColor White
        Write-Host "  ✅ 双击即可运行" -ForegroundColor White
        
    } else {
        throw "未找到编译后的可执行文件: $exePath"
    }

    Write-Host ""
    Write-Host "====================================" -ForegroundColor Cyan
    Write-Host "单文件编译成功完成！" -ForegroundColor Green
    Write-Host "====================================" -ForegroundColor Cyan
    
    # 询问是否打开文件夹
    if ($OpenFolder -or (Read-Host "是否打开发布目录? (y/N)") -match '^[Yy]') {
        Invoke-Item $PublishPath
    }
    
    # 询问是否测试运行
    if ($TestRun -or (Read-Host "是否立即测试运行程序? (y/N)") -match '^[Yy]') {
        Write-Host "正在启动程序..." -ForegroundColor Yellow
        Start-Process $exePath
    }

} catch {
    Write-Host ""
    Write-Host "❌ 编译失败: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
    exit 1
}

Write-Host ""
Write-Host "按任意键退出..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown") 