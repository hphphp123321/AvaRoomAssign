using System;
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
                
                // 安全地输出缓冲的日志 - 直接在当前线程输出，不使用Dispatcher
                if (_logBuffer.Length > 0)
                {
                    var bufferedLogs = _logBuffer.ToString();
                    _logBuffer.Clear();
                    
                    try
                    {
                        _logAction(bufferedLogs);
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
                if (!_isInitialized || _logAction == null)
                {
                    // 如果还没初始化，先缓存日志
                    _logBuffer.AppendLine($"[{DateTime.Now:HH:mm:ss}] {message}");
                    return;
                }

                var formattedMessage = $"[{DateTime.Now:HH:mm:ss}] {message}\n";
                
                try
                {
                    // 简化逻辑：优先尝试直接调用
                    _logAction(formattedMessage);
                }
                catch (Exception ex)
                {
                    // 如果直接调用失败，尝试使用Dispatcher
                    try
                    {
                        if (Dispatcher.UIThread.CheckAccess())
                        {
                            _logAction(formattedMessage);
                        }
                        else
                        {
                            // 使用Post而不是Invoke，避免死锁
                            Dispatcher.UIThread.Post(() =>
                            {
                                try
                                {
                                    _logAction(formattedMessage);
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
            }
        }
    }
} 