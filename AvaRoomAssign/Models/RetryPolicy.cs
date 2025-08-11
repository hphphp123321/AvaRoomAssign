using System;
using System.Threading;
using System.Threading.Tasks;

namespace AvaRoomAssign.Models
{
    /// <summary>
    /// 通用重试策略类
    /// </summary>
    public static class RetryPolicy
    {
        /// <summary>
        /// 对返回值类型的操作执行重试
        /// </summary>
        /// <typeparam name="T">返回值类型</typeparam>
        /// <param name="operation">要执行的异步操作</param>
        /// <param name="operationName">操作名称，用于日志输出</param>
        /// <param name="maxAttempts">最大重试次数</param>
        /// <param name="retryDelayMs">重试间隔（毫秒）</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>操作结果</returns>
        public static async Task<T?> ExecuteAsync<T>(
            Func<Task<T?>> operation,
            string operationName,
            int maxAttempts = 3,
            int retryDelayMs = 200,
            CancellationToken cancellationToken = default) where T : class
        {
            Exception? lastException = null;

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    if (cancellationToken.IsCancellationRequested)
                        return null;

                    var result = await operation();
                    if (result != null)
                    {
                        if (attempt > 1)
                        {
                            LogManager.Success($"{operationName} 在第 {attempt} 次尝试后成功");
                        }
                        return result;
                    }

                    // 结果为空但没有异常，代表请求失败，继续重试
                    if (attempt == maxAttempts)
                    {
                        LogManager.Error($"{operationName} 在 {maxAttempts} 次尝试后仍无结果");
                    }
                    else
                    {
                        LogManager.Warning($"{operationName} 第 {attempt} 次尝试失败，{retryDelayMs}ms后重试...");
                        await DelayWithCancellation(retryDelayMs, cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    lastException = ex;

                    if (attempt < maxAttempts)
                    {
                        LogManager.Warning($"{operationName} 第 {attempt} 次尝试失败: {ex.Message}，{retryDelayMs}ms后重试...");
                        await DelayWithCancellation(retryDelayMs, cancellationToken);
                    }
                    else
                    {
                        LogManager.Error($"{operationName} 在 {maxAttempts} 次尝试后最终失败: {ex.Message}");
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// 对布尔返回值的操作执行重试
        /// </summary>
        /// <param name="operation">要执行的异步操作</param>
        /// <param name="operationName">操作名称，用于日志输出</param>
        /// <param name="maxAttempts">最大重试次数</param>
        /// <param name="retryDelayMs">重试间隔（毫秒）</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>操作结果</returns>
        public static async Task<bool> ExecuteBoolAsync(
            Func<Task<bool>> operation,
            string operationName,
            int maxAttempts = 3,
            int retryDelayMs = 200,
            CancellationToken cancellationToken = default)
        {
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    if (cancellationToken.IsCancellationRequested)
                        return false;

                    var result = await operation();
                    if (result)
                    {
                        if (attempt > 1)
                        {
                            LogManager.Success($"{operationName} 在第 {attempt} 次尝试后成功");
                        }
                        return true;
                    }

                    // 结果为false，继续重试
                    if (attempt < maxAttempts)
                    {
                        LogManager.Warning($"{operationName} 第 {attempt} 次尝试返回false，{retryDelayMs}ms后重试...");
                        await DelayWithCancellation(retryDelayMs, cancellationToken);
                    }
                    else
                    {
                        LogManager.Error($"{operationName} 在 {maxAttempts} 次尝试后仍返回false");
                    }
                }
                catch (Exception ex)
                {
                    if (attempt < maxAttempts)
                    {
                        LogManager.Warning($"{operationName} 第 {attempt} 次尝试失败: {ex.Message}，{retryDelayMs}ms后重试...");
                        await DelayWithCancellation(retryDelayMs, cancellationToken);
                    }
                    else
                    {
                        LogManager.Error($"{operationName} 在 {maxAttempts} 次尝试后最终失败: {ex.Message}");
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// 带取消令牌的延迟方法
        /// </summary>
        private static async Task DelayWithCancellation(int delayMs, CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(delayMs, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                LogManager.Warning("重试等待被取消，流程终止");
                throw;
            }
        }
    }
}