# 🏠 AvaRoomAssign - 智能抢租房软件

[![.NET](https://img.shields.io/badge/.NET-9.0-purple.svg)](https://dotnet.microsoft.com/download)
[![Avalonia](https://img.shields.io/badge/UI-Avalonia-blue.svg)](https://avaloniaui.net/)
[![Platform](https://img.shields.io/badge/Platform-Windows-lightgrey.svg)](https://www.microsoft.com/windows)

> 🚀 基于 Avalonia UI 框架的现代化自动抢租房软件，采用最新的 .NET 9.0 技术栈开发

## ✨ 功能特性

### 🎯 核心功能
- **🤖 智能抢房** - 支持多种房型、多个社区的智能匹配和自动选择
- **⚡ 双模式运行** - 提供模拟点击和HTTP发包两种运行模式
- **🎨 现代化界面** - 基于 Avalonia UI 的跨平台现代界面设计
- **📊 实时监控** - 实时日志显示，操作过程可视化
- **💾 配置保存** - 自动保存用户配置，支持配置导入导出

### 🔧 技术特性
- **🚀 AOT 编译** - 支持原生 AOT 编译，启动速度快，内存占用低
- **📦 单文件发布** - 无需安装运行时，一键部署
- **🔄 异步架构** - 基于 async/await 的高性能异步处理
- **🛡️ 异常处理** - 完善的错误处理和重试机制
- **🔧 可配置性** - 灵活的参数配置，满足不同使用场景

## 🏗️ 技术架构

### 技术栈
- **UI框架**: Avalonia 11.3.0 (跨平台现代UI)
- **开发语言**: C# (.NET 9.0)
- **设计模式**: MVVM (Model-View-ViewModel)
- **数据绑定**: CommunityToolkit.Mvvm
- **Web自动化**: Selenium WebDriver
- **HTTP客户端**: HttpClient + HtmlAgilityPack
- **配置管理**: System.Text.Json

### 项目结构
```
AvaRoomAssign/
├── Models/                 # 数据模型和业务逻辑
│   ├── ConfigManager.cs    # 配置管理器
│   ├── HouseCondition.cs   # 房屋条件模型
│   ├── DriverSelector.cs   # 浏览器模拟模式
│   ├── HttpSelector.cs     # HTTP发包模式
│   └── ISelector.cs        # 选择器接口
├── ViewModels/             # 视图模型 (MVVM)
│   └── MainWindowViewModel.cs
├── Views/                  # 用户界面
│   └── MainWindow.axaml
└── Assets/                 # 静态资源
```

### 运行模式对比

| 特性 | 模拟点击模式 | HTTP发包模式 |
|------|-------------|-------------|
| **速度** | 较慢 | 极快 |
| **稳定性** | 高 | 中等 |
| **可视化** | 可以看到操作过程 | 后台运行 |
| **配置要求** | 用户名+密码 或 Cookie | 必须需要Cookie |
| **成功率** | 高 | 中等 |
| **资源占用** | 高 (浏览器) | 低 |

## 🚀 快速开始

### 系统要求
- **操作系统**: Windows 10/11 (x64)
- **运行时**: 无需安装 .NET 运行时 (AOT版本)
- **浏览器**: Chrome 或 Edge (仅模拟点击模式需要)
- **内存**: 最低 512MB，推荐 1GB+

### 安装方式

#### 方式一：下载编译版本 (推荐)
1. 下载最新的 [Release](https://github.com/hphphp123321/AvaRoomAssign/releases) 版本
2. 解压到任意目录
3. 双击 `AvaRoomAssign.exe` 运行

#### 方式二：源码编译
```bash
# 克隆项目
git clone https://github.com/hphphp123321/AvaRoomAssign.git
cd AvaRoomAssign

# 标准编译 (需要.NET运行时)
dotnet build -c Release

# AOT单文件编译 (推荐)
powershell -ExecutionPolicy Bypass -File Build-AOT.ps1
```

## 🔧 配置说明

### 基础配置
- **运行模式**: 选择模拟点击或HTTP发包模式
- **浏览器类型**: Chrome 或 Edge (仅模拟点击模式)
- **申请人姓名**: 与房屋申请系统中的姓名保持一致
- **开始时间**: 抢房开始的精确时间

### 社区条件配置
每个社区条件包含以下参数：
- **社区名称**: 完整的社区名称
- **幢号**: 指定幢号，0表示不限制
- **楼层**: 支持范围格式，如 "3-5,8,10"
- **最高价格**: 0表示不限制价格
- **最小面积**: 0表示不限制面积
- **房型**: 一居室/二居室/三居室

### 配置文件位置
配置文件自动保存在：
```
%APPDATA%\AvaRoomAssign\config.json
```

## 🍪 Cookie获取详细教程

Cookie是HTTP发包模式的关键认证信息，以下是详细的获取步骤：

### 🌐 浏览器获取方法

#### 方法一：Chrome浏览器
1. **登录系统**
   - 打开 Chrome 浏览器
   - 访问 `https://ent.qpgzf.cn/CompanyHome/Main`
   - 使用您的账号密码正常登录

2. **打开开发者工具**
   - 按 `F12` 键，或右键选择"检查"
   - 切换到 `Network`（网络） 标签页
   - 如果没有看到，点击 `>>` 查看更多标签

3. **定位Cookie**
   - 刷新页面
   - 在左侧名称中找到最上方的`Main`
   - 点击请求，在 `Headers` (请求头) 标签页中找到 `Cookie` 字段（类似于SYS_USER_COOKIE_KEY=SYS_USER_COOKIE_KEY=ZH8ddQR5KVbxxpyo7dTQ6MgWrMjJEvbogRQ+XWAAr46pHD5gwRrCmg==）
   - 复制 `Cookie` 字段中的值（SYS_USER_COOKIE_KEY=ZH8ddQR5KVbxxpyo7dTQ6MgWrMjJEvbogRQ+XWAAr46pHD5gwRrCmg==）

4. **复制Cookie值**
   - 双击 `SYS_USER_COOKIE_KEY` 对应的 `Value` 值
   - 全选并复制 (Ctrl+A, Ctrl+C)
   - 粘贴到软件的Cookie输入框中

#### 方法二：Edge浏览器
1. **登录系统** (同Chrome方法)
2. **打开开发者工具**
   - 按 `F12` 键
   - 切换到 `Network`（网络） 标签页
3. **其余步骤与Chrome相同**

#### 方法三：设置页面里获取（推荐，较为简单）
1. **登录系统**
2. **点击浏览器右上角头像，选择设置**
3. **点击类似于“查看所有 Cookie 和站点数据”选项**
4. **在搜索Cookie中搜索qpgzf.cn，找到 ent.qpgzf.cn 本地存储的数据Cookie值，复制**

### 🔍 Cookie格式示例
正确的Cookie值应该类似于：
```
SYS_USER_COOKIE_KEY=ZH8ddQR5KVbxxpyo7dTQ6MgWrMjJEvbogRQ+XWAAr46pHD5gwRrCmg==
```

### ⚠️ 重要提示
- **安全性**: Cookie包含您的登录信息，请妥善保管
- **时效性**: Cookie有过期时间，过期后需要重新获取
- **唯一性**: 每次登录的Cookie可能不同
- **测试**: 获取Cookie后建议先测试HTTP发包模式是否正常工作

### 🔧 Cookie故障排除
如果使用Cookie后仍无法正常工作：
1. **检查格式**: 确保复制的是完整的Value值
2. **重新获取**: 清除浏览器缓存后重新登录获取
3. **检查过期**: Cookie可能已过期，需要重新获取
4. **使用备用方案**: 改用模拟点击模式 + 用户名密码

## 📖 使用指南

### 基本操作流程

1. **启动软件**
   ```
   双击 AvaRoomAssign.exe
   ```

2. **配置基本信息**
   - 选择运行模式 (推荐HTTP发包模式)
   - 填写申请人姓名
   - 设置开始时间
   - 配置Cookie (HTTP模式) 或 用户名密码 (模拟点击模式)

3. **设置社区条件**
   - 点击 "添加社区" 按钮
   - 填写社区名称、楼层、价格等筛选条件
   - 可以添加多个社区作为备选志愿

4. **开始抢房**
   - 点击 "开始" 按钮
   - 软件会在指定时间自动开始抢房
   - 实时查看日志了解运行状态

5. **停止操作**
   - 如需中途停止，点击 "停止" 按钮

### 高级功能

#### 多志愿策略
- 支持设置多个社区作为志愿
- 按照添加顺序优先尝试
- 第一志愿失败后自动尝试下一个

#### 楼层范围语法
```
"3-5"     # 3到5层
"1,3,5"   # 1层、3层、5层
"2-4,8"   # 2到4层和8层
"0"       # 不限制楼层
```

#### 自动确认
- 勾选"自动确认"可以自动完成最后的确认步骤
- 提高抢房成功率，但需要谨慎使用

## 🐛 故障排除

### 常见问题

#### 1. 无法启动程序
**症状**: 双击exe文件无反应或报错
**解决方案**:
```bash
# 检查是否缺少运行时 (非AOT版本)
dotnet --version

# 或下载AOT版本，无需运行时
```

#### 2. 模拟点击模式浏览器启动失败
**症状**: 提示找不到浏览器驱动
**解决方案**:
- 确保系统已安装 Chrome 或 Edge 浏览器
- 浏览器版本需要与驱动版本兼容
- 尝试以管理员权限运行软件

#### 3. HTTP发包模式认证失败
**症状**: 提示Cookie无效或获取申请人ID失败
**解决方案**:
- 重新获取最新的Cookie
- 检查Cookie格式是否正确
- 确认申请人姓名与系统中完全一致

#### 4. 抢房失败
**症状**: 软件运行正常但未能成功选到房
**解决方案**:
- 调整开始时间，提前几秒开始
- 降低请求间隔时间
- 增加更多备选社区志愿
- 放宽筛选条件 (楼层、价格等)

### 日志分析
软件界面下方会显示详细的运行日志：
- `✅` 表示操作成功
- `⚠️` 表示警告信息
- `❌` 表示错误信息
- `🔍` 表示查找操作

根据日志信息可以快速定位问题所在。

## 🔐 安全说明

### 数据安全
- **本地存储**: 所有配置信息存储在本地，不会上传到任何服务器
- **密码处理**: 建议使用Cookie方式，避免明文保存密码
- **网络通信**: 仅与目标房屋系统通信，不会连接其他服务器

### 使用建议
- **合法使用**: 仅用于个人合法的房屋申请，不得用于商业目的
- **频率控制**: 合理设置请求间隔，避免对系统造成过大压力
- **及时更新**: 保持软件版本更新，获得最佳兼容性

## 📋 更新日志

### v2.0.0 (当前版本)
- ✨ 全新的 Avalonia UI 界面设计
- 🚀 支持 .NET 9.0 和 AOT 编译
- 🔧 完善的配置管理系统
- 📊 实时日志监控
- 🌐 HTTP发包模式优化
- 🛡️ 增强的错误处理机制

### v1.x.x (历史版本)
- 基础的房屋选择功能
- 简单的界面设计
- 基本的配置保存

## 🤝 贡献指南

欢迎参与项目改进！

### 开发环境搭建
```bash
# 克隆项目
git clone https://github.com/hphphp123321/AvaRoomAssign.git

# 安装 .NET 9.0 SDK
# https://dotnet.microsoft.com/download

# 恢复依赖包
dotnet restore

# 运行项目
dotnet run --project AvaRoomAssign
```

### 贡献方式
1. Fork 本项目
2. 创建您的特性分支 (`git checkout -b feature/AmazingFeature`)
3. 提交您的更改 (`git commit -m 'Add some AmazingFeature'`)
4. 推送到分支 (`git push origin feature/AmazingFeature`)
5. 打开一个 Pull Request

## 🙏 致谢

- [Avalonia UI](https://avaloniaui.net/) - 优秀的跨平台UI框架
- [Selenium](https://selenium.dev/) - 强大的Web自动化工具
- [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) - MVVM开发工具包

---

<div align="center">

**如果这个项目对您有帮助，请考虑给它一个 ⭐**

Made with ❤️ by Hp

</div> 