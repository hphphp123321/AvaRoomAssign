# AvaRoomAssign AOT 编译说明

## 🚀 什么是AOT编译？

AOT（Ahead-of-Time）编译将您的.NET应用程序编译为原生代码，生成：
- **单个可执行文件**：无需安装.NET运行时
- **更快的启动速度**：原生代码执行
- **更小的部署包**：去除未使用的代码
- **更好的性能**：优化的原生代码

## 📁 当前编译结果

✅ **AOT编译已完成！**

**发布位置：** `AvaRoomAssign\bin\Release\net9.0\win-x64\publish\`

**文件清单：**
- 🔥 **AvaRoomAssign.exe** (84.58 MB) - 主程序
- 📊 AvaRoomAssign.pdb (549.98 MB) - 调试符号
- 🎨 av_libglesv2.dll (4.23 MB) - OpenGL渲染
- 📝 libHarfBuzzSharp.dll (1.53 MB) - 文本渲染
- 🖼️ libSkiaSharp.dll (8.98 MB) - 图形引擎
- 📁 selenium-manager/ - Selenium管理器

**总大小：** 约 650 MB（包含调试符号）

## 🛠️ 使用方法

### 方法一：直接运行（推荐）
直接双击 `AvaRoomAssign.exe` 即可运行，无需安装任何依赖！

### 方法二：使用批处理脚本
```bash
# 双击运行
build-aot.bat
```

### 方法三：使用PowerShell脚本（需要管理员权限）
```powershell
# 设置执行策略（首次使用）
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser

# 运行编译脚本
.\Build-AOT.ps1

# 其他选项
.\Build-AOT.ps1 -SkipClean          # 跳过清理
.\Build-AOT.ps1 -OpenFolder         # 自动打开输出目录
.\Build-AOT.ps1 -Configuration Debug  # 调试模式
```

### 方法四：手动命令行
```bash
# 清理项目
dotnet clean AvaRoomAssign/AvaRoomAssign.csproj

# AOT编译
dotnet publish AvaRoomAssign/AvaRoomAssign.csproj -c Release -r win-x64 --self-contained
```

## 📦 部署建议

### 最小部署（推荐）
只需要这些文件即可运行：
```
AvaRoomAssign.exe          (84.58 MB)
av_libglesv2.dll          (4.23 MB)
libHarfBuzzSharp.dll      (1.53 MB)
libSkiaSharp.dll          (8.98 MB)
selenium-manager/         (如果使用浏览器自动化)
```

**总大小：** 约 99 MB

### 完整部署
包含所有文件：
```
所有文件                   (约 650 MB)
```
- 包含调试符号，便于问题排查
- 适用于开发测试环境

## ⚡ 性能对比

| 特性 | 普通发布 | AOT发布 |
|------|----------|---------|
| 启动速度 | 较慢 | **快速** |
| 文件大小 | 小 | **中等** |
| 依赖需求 | 需要.NET运行时 | **无需依赖** |
| 兼容性 | 高 | **原生** |
| 调试支持 | 完整 | 有限 |

## 🎯 优化建议

### 1. 减小文件大小
```xml
<!-- 在 .csproj 中添加 -->
<PropertyGroup>
    <IlcOptimizationPreference>Size</IlcOptimizationPreference>
    <IlcGenerateStackTraceData>false</IlcGenerateStackTraceData>
</PropertyGroup>
```

### 2. 移除调试符号
发布时删除 `.pdb` 文件可节省 550MB 空间。

### 3. 自定义运行时
```bash
# 编译为其他平台
dotnet publish -r win-arm64 --self-contained  # ARM64
dotnet publish -r linux-x64 --self-contained  # Linux
dotnet publish -r osx-x64 --self-contained    # macOS
```

## 🐛 常见问题

### Q: 编译时间很长？
A: AOT编译需要2-5分钟，这是正常的。首次编译会更久。

### Q: 文件太大？
A: 可以删除 `.pdb` 调试文件，节省 550MB 空间。

### Q: 无法运行？
A: 确保所有 `.dll` 文件和主程序在同一目录。

### Q: PowerShell脚本无法执行？
A: 以管理员身份运行 PowerShell，执行：
```powershell
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
```

## 📋 项目配置

当前项目已配置的AOT设置：
```xml
<!-- AOT 配置 -->
<PublishAot>true</PublishAot>
<PublishTrimmed>true</PublishTrimmed>
<PublishSingleFile>true</PublishSingleFile>
<SelfContained>true</SelfContained>

<!-- 优化设置 -->
<IlcOptimizationPreference>Size</IlcOptimizationPreference>
<IlcGenerateStackTraceData>false</IlcGenerateStackTraceData>
```

## 🎉 总结

✅ **AOT编译成功！** 您现在拥有：
- 无需.NET运行时的独立可执行文件
- 更快的启动速度
- 更好的用户体验
- 简化的部署流程

🚀 **立即体验：** 直接运行 `AvaRoomAssign\bin\Release\net9.0\win-x64\publish\AvaRoomAssign.exe` 