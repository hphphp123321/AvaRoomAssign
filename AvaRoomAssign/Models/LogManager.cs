using System;
using System.IO;
using Avalonia.Threading;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace AvaRoomAssign.Models
{
    /// <summary>
    /// 基于Serilog的日志管理器 - AOT友好
    /// </summary>
    public static class LogManager
    {
        private static ILogger? _logger;
        private static Action<string>? _uiLogAction;
        private static readonly object _lockObject = new();
        private static bool _isInitialized = false;
        private static string? _logFilePath;

        /// <summary>
        /// 初始化日志管理器
        /// </summary>
        /// <param name="uiLogAction">UI日志输出动作</param>
        public static void Initialize(Action<string> uiLogAction)
        {
            lock (_lockObject)
            {
                if (_isInitialized)
                {
                    WriteLog("⚠️ 日志管理器已经初始化，忽略重复初始化");
                    return;
                }

                _uiLogAction = uiLogAction;

                try
                {
                    // 获取日志文件路径
                    var configPath = ConfigManager.GetConfigPath();
                    var logDir = Path.GetDirectoryName(configPath);
                    
                    if (string.IsNullOrEmpty(logDir))
                    {
                        logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AvaRoomAssign");
                    }

                    // 确保目录存在
                    if (!Directory.Exists(logDir))
                    {
                        Directory.CreateDirectory(logDir);
                    }

                    // 创建唯一的日志文件名
                    var processId = System.Diagnostics.Process.GetCurrentProcess().Id;
                    var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    var fileName = $"log_{timestamp}_{processId}.txt";
                    _logFilePath = Path.Combine(logDir, fileName);

                    // 配置Serilog
                    var loggerConfig = new LoggerConfiguration()
                        .MinimumLevel.Debug()
                        .WriteTo.File(
                            path: _logFilePath,
                            outputTemplate: "[{Timestamp:HH:mm:ss}] {Level:u3} {Message:lj}{NewLine}",
                            rollingInterval: RollingInterval.Day,
                            retainedFileCountLimit: 3,
                            shared: true, // 允许多个进程共享同一个日志文件
                            flushToDiskInterval: TimeSpan.FromSeconds(1))
                        .WriteTo.Sink(new UISink(_uiLogAction));

                    _logger = loggerConfig.CreateLogger();
                    _isInitialized = true;

                    // 写入启动信息
                    _logger.Information("=== 日志系统启动 (PID:{ProcessId}) ===", processId);
                    
                    System.Diagnostics.Debug.WriteLine($"Serilog日志已初始化: {_logFilePath}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"初始化Serilog失败: {ex.Message}");
                    // 创建一个最小的控制台日志器作为后备
                    _logger = new LoggerConfiguration()
                        .WriteTo.Console()
                        .WriteTo.Sink(new UISink(_uiLogAction))
                        .CreateLogger();
                    _isInitialized = true;
                }
            }
        }

        /// <summary>
        /// 写入日志
        /// </summary>
        /// <param name="message">日志消息</param>
        public static void WriteLog(string message)
        {
            if (!_isInitialized || _logger == null)
            {
                // 如果还没初始化，输出到调试控制台
                System.Diagnostics.Debug.WriteLine($"[未初始化] {message}");
                return;
            }

            try
            {
                _logger.Information(message);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"写入日志失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 写入信息日志
        /// </summary>
        public static void Info(string message) => WriteLog($"ℹ️ {message}");

        /// <summary>
        /// 写入成功日志
        /// </summary>
        public static void Success(string message) => WriteLog($"✅ {message}");

        /// <summary>
        /// 写入警告日志
        /// </summary>
        public static void Warning(string message)
        {
            if (!_isInitialized || _logger == null)
            {
                System.Diagnostics.Debug.WriteLine($"[未初始化] ⚠️ {message}");
                return;
            }

            try
            {
                _logger.Warning("⚠️ {Message}", message);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"写入警告日志失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 写入错误日志
        /// </summary>
        public static void Error(string message)
        {
            if (!_isInitialized || _logger == null)
            {
                System.Diagnostics.Debug.WriteLine($"[未初始化] ❌ {message}");
                return;
            }

            try
            {
                _logger.Error("❌ {Message}", message);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"写入错误日志失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 写入错误日志（带异常信息）
        /// </summary>
        public static void Error(string message, Exception ex)
        {
            if (!_isInitialized || _logger == null)
            {
                System.Diagnostics.Debug.WriteLine($"[未初始化] ❌ {message}: {ex.Message}");
                return;
            }

            try
            {
                _logger.Error(ex, "❌ {Message}", message);
            }
            catch (Exception logEx)
            {
                System.Diagnostics.Debug.WriteLine($"写入错误日志失败: {logEx.Message}");
            }
        }

        /// <summary>
        /// 写入调试日志
        /// </summary>
        public static void Debug(string message)
        {
            if (!_isInitialized || _logger == null)
            {
                System.Diagnostics.Debug.WriteLine($"[未初始化] 🔧 {message}");
                return;
            }

            try
            {
                _logger.Debug("🔧 {Message}", message);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"写入调试日志失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 清理资源
        /// </summary>
        public static void Cleanup()
        {
            lock (_lockObject)
            {
                if (_isInitialized && _logger != null)
                {
                    try
                    {
                        var processId = System.Diagnostics.Process.GetCurrentProcess().Id;
                        _logger.Information("=== 日志系统关闭 (PID:{ProcessId}) ===", processId);
                        
                        if (_logger is Logger serilogLogger)
                        {
                            serilogLogger.Dispose();
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"清理日志资源失败: {ex.Message}");
                    }
                }

                _logger = null;
                _uiLogAction = null;
                _isInitialized = false;
                _logFilePath = null;
            }
        }

        /// <summary>
        /// 获取当前日志文件路径
        /// </summary>
        /// <returns>当前日志文件路径，如果未初始化则返回null</returns>
        public static string? GetCurrentLogFilePath()
        {
            return _logFilePath;
        }

        /// <summary>
        /// 检查是否已初始化
        /// </summary>
        public static bool IsInitialized => _isInitialized;
    }

    /// <summary>
    /// 自定义UI日志输出Sink
    /// </summary>
    internal class UISink : ILogEventSink
    {
        private readonly Action<string> _uiLogAction;

        public UISink(Action<string> uiLogAction)
        {
            _uiLogAction = uiLogAction;
        }

        public void Emit(LogEvent logEvent)
        {
            try
            {
                var formattedMessage = logEvent.RenderMessage() + "\n";
                
                // 尝试在UI线程上输出
                if (Dispatcher.UIThread.CheckAccess())
                {
                    _uiLogAction(formattedMessage);
                }
                else
                {
                    // 使用Post避免死锁
                    Dispatcher.UIThread.Post(() =>
                    {
                        try
                        {
                            _uiLogAction(formattedMessage);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"UI日志输出失败: {ex.Message}");
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UI Sink错误: {ex.Message}");
            }
        }
    }
} 