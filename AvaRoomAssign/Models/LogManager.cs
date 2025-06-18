using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Avalonia.Threading;

namespace AvaRoomAssign.Models
{
    /// <summary>
    /// AOT友好的日志管理器
    /// </summary>
    public static class LogManager
    {
        private static Action<string>? _logAction;
        private static readonly StringBuilder _logBuffer = new();
        private static readonly object _lockObject = new();
        private static bool _isInitialized = false;
        
        // 文件日志相关
        private static string? _logFilePath;
        private static StreamWriter? _logFileWriter;
        private static readonly string LogFileNameFormat = "log_{0:yyyyMMdd_HHmmss}_{1}.txt";
        private const int MaxLogFiles = 3;

        /// <summary>
        /// 初始化日志管理器
        /// </summary>
        /// <param name="logAction">日志输出动作</param>
        public static void Initialize(Action<string> logAction)
        {
            lock (_lockObject)
            {
                _logAction = logAction;
                _isInitialized = true;
                
                // 初始化文件日志
                InitializeFileLogging();
                
                // 安全地输出缓冲的日志 - 直接在当前线程输出，不使用Dispatcher
                if (_logBuffer.Length > 0)
                {
                    var bufferedLogs = _logBuffer.ToString();
                    _logBuffer.Clear();
                    
                    try
                    {
                        _logAction(bufferedLogs);
                        
                        // 同时将缓冲的日志写入文件（因为之前WriteToFile时文件还没初始化）
                        var lines = bufferedLogs.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                        foreach (var line in lines)
                        {
                            if (!string.IsNullOrWhiteSpace(line))
                            {
                                WriteToFileDirectly(line.Trim());
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"输出缓冲日志失败: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// 写入日志
        /// </summary>
        /// <param name="message">日志消息</param>
        public static void WriteLog(string message)
        {
            lock (_lockObject)
            {
                var formattedMessage = $"[{DateTime.Now:HH:mm:ss}] {message}";
                
                // 无论是否初始化，都先尝试写入文件
                WriteToFile(formattedMessage);
                
                if (!_isInitialized || _logAction == null)
                {
                    // 如果还没初始化，先缓存日志用于UI显示
                    _logBuffer.AppendLine(formattedMessage);
                    return;
                }

                var uiFormattedMessage = formattedMessage + "\n";
                
                try
                {
                    // 简化逻辑：优先尝试直接调用
                    _logAction(uiFormattedMessage);
                }
                catch (Exception ex)
                {
                    // 如果直接调用失败，尝试使用Dispatcher
                    System.Diagnostics.Debug.WriteLine($"直接调用日志失败: {ex.Message}");
                    try
                    {
                        if (Dispatcher.UIThread.CheckAccess())
                        {
                            _logAction(uiFormattedMessage);
                        }
                        else
                        {
                            // 使用Post而不是Invoke，避免死锁
                            Dispatcher.UIThread.Post(() =>
                            {
                                try
                                {
                                    _logAction(uiFormattedMessage);
                                }
                                catch (Exception postEx)
                                {
                                    System.Diagnostics.Debug.WriteLine($"Post调用失败: {postEx.Message}");
                                }
                            });
                        }
                    }
                    catch (Exception dispatcherEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"Dispatcher调用失败: {dispatcherEx.Message}");
                    }
                }
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
        public static void Warning(string message) => WriteLog($"⚠️ {message}");

        /// <summary>
        /// 写入错误日志
        /// </summary>
        public static void Error(string message) => WriteLog($"❌ {message}");

        /// <summary>
        /// 写入错误日志（带异常信息）
        /// </summary>
        public static void Error(string message, Exception ex) => WriteLog($"❌ {message}: {ex.Message}");

        /// <summary>
        /// 写入调试日志
        /// </summary>
        public static void Debug(string message) => WriteLog($"🔧 {message}");

        /// <summary>
        /// 清理资源
        /// </summary>
        public static void Cleanup()
        {
            lock (_lockObject)
            {
                _logAction = null;
                _isInitialized = false;
                _logBuffer.Clear();
                
                // 清理文件日志资源
                CleanupFileLogging();
            }
        }
        
        /// <summary>
        /// 初始化文件日志
        /// </summary>
        private static void InitializeFileLogging()
        {
            try
            {
                // 获取配置文件所在的目录
                var configPath = ConfigManager.GetConfigPath();
                var logDir = Path.GetDirectoryName(configPath);
                
                if (string.IsNullOrEmpty(logDir))
                {
                    System.Diagnostics.Debug.WriteLine("无法获取日志目录路径");
                    return;
                }
                
                // 确保目录存在
                if (!Directory.Exists(logDir))
                {
                    Directory.CreateDirectory(logDir);
                }
                
                // 清理旧的日志文件（保留最新的3个）
                CleanupOldLogFiles(logDir);
                
                // 创建新的日志文件，使用进程ID避免冲突
                var processId = System.Diagnostics.Process.GetCurrentProcess().Id;
                var fileName = string.Format(LogFileNameFormat, DateTime.Now, processId);
                _logFilePath = Path.Combine(logDir, fileName);
                
                // 尝试创建文件写入器，如果失败则尝试其他文件名
                var attempts = 0;
                while (attempts < 5) // 最多尝试5次
                {
                    try
                    {
                        _logFileWriter = new StreamWriter(_logFilePath, append: true, encoding: Encoding.UTF8)
                        {
                            AutoFlush = true // 自动刷新，确保实时写入
                        };
                        break; // 成功创建，跳出循环
                    }
                    catch (IOException ex) when (ex.Message.Contains("being used by another process"))
                    {
                        // 文件被占用，尝试新的文件名
                        attempts++;
                        var suffix = processId + attempts;
                        fileName = string.Format(LogFileNameFormat, DateTime.Now, suffix);
                        _logFilePath = Path.Combine(logDir, fileName);
                        System.Diagnostics.Debug.WriteLine($"文件被占用，尝试新文件名: {fileName}");
                        
                        if (attempts >= 5)
                        {
                            throw; // 超过重试次数，抛出异常
                        }
                    }
                }
                
                if (_logFileWriter != null)
                {
                    // 写入启动信息
                    WriteToFileDirectly($"=== 日志开始 {DateTime.Now:yyyy-MM-dd HH:mm:ss} (PID:{processId}) ===");
                    System.Diagnostics.Debug.WriteLine($"文件日志已初始化: {_logFilePath}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("无法创建日志文件写入器");
                    _logFilePath = null;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"初始化文件日志失败: {ex.Message}");
                _logFilePath = null;
                _logFileWriter = null;
            }
        }
        
        /// <summary>
        /// 写入日志到文件
        /// </summary>
        /// <param name="message">要写入的消息</param>
        private static void WriteToFile(string message)
        {
            try
            {
                if (_logFileWriter != null && !string.IsNullOrEmpty(_logFilePath))
                {
                    _logFileWriter.WriteLine(message);
                    _logFileWriter.Flush(); // 显式刷新，确保立即写入
                }
                else
                {
                    // 调试信息：文件日志还未初始化
                    if (_logFileWriter == null)
                    {
                        System.Diagnostics.Debug.WriteLine($"WriteToFile: _logFileWriter is null for message: {message}");
                    }
                    if (string.IsNullOrEmpty(_logFilePath))
                    {
                        System.Diagnostics.Debug.WriteLine($"WriteToFile: _logFilePath is null or empty for message: {message}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"写入文件日志失败: {ex.Message}");
                // 如果写入失败，尝试重新初始化
                CleanupFileLogging();
                InitializeFileLogging();
                
                // 重新尝试写入
                try
                {
                    if (_logFileWriter != null)
                    {
                        _logFileWriter.WriteLine(message);
                        _logFileWriter.Flush(); // 显式刷新，确保立即写入
                    }
                }
                catch (Exception retryEx)
                {
                    System.Diagnostics.Debug.WriteLine($"重新写入文件日志也失败: {retryEx.Message}");
                }
            }
        }
        
        /// <summary>
        /// 直接写入文件（不加锁，用于内部调用）
        /// </summary>
        /// <param name="message">要写入的消息</param>
        private static void WriteToFileDirectly(string message)
        {
            try
            {
                if (_logFileWriter != null)
                {
                    _logFileWriter.WriteLine(message);
                    _logFileWriter.Flush(); // 显式刷新，确保立即写入
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"直接写入文件日志失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 清理旧的日志文件，保留最新的指定数量
        /// </summary>
        /// <param name="logDir">日志目录</param>
        private static void CleanupOldLogFiles(string logDir)
        {
            try
            {
                var logFiles = Directory.GetFiles(logDir, "log_*.txt")
                    .Select(f => new FileInfo(f))
                    .OrderByDescending(f => f.CreationTime)
                    .ToArray();
                
                // 如果文件数量超过限制，删除旧文件
                if (logFiles.Length >= MaxLogFiles)
                {
                    var filesToDelete = logFiles.Skip(MaxLogFiles - 1); // 保留最新的 MaxLogFiles-1 个，为新文件留位置
                    
                    foreach (var fileToDelete in filesToDelete)
                    {
                        try
                        {
                            // 检查文件是否可以访问（没有被其他进程占用）
                            using (var stream = File.Open(fileToDelete.FullName, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                            {
                                // 如果能打开文件，说明没有被占用，可以安全删除
                            }
                            
                            fileToDelete.Delete();
                            System.Diagnostics.Debug.WriteLine($"已删除旧日志文件: {fileToDelete.Name}");
                        }
                        catch (IOException)
                        {
                            // 文件被占用，跳过删除
                            System.Diagnostics.Debug.WriteLine($"跳过删除正在使用的日志文件: {fileToDelete.Name}");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"删除旧日志文件失败 {fileToDelete.Name}: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"清理旧日志文件失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 清理文件日志资源
        /// </summary>
        private static void CleanupFileLogging()
        {
            try
            {
                if (_logFileWriter != null)
                {
                    var processId = System.Diagnostics.Process.GetCurrentProcess().Id;
                    WriteToFileDirectly($"=== 日志结束 {DateTime.Now:yyyy-MM-dd HH:mm:ss} (PID:{processId}) ===");
                    _logFileWriter.Dispose();
                    _logFileWriter = null;
                }
                _logFilePath = null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"清理文件日志资源失败: {ex.Message}");
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
    }
} 