using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using HtmlAgilityPack;

namespace AvaRoomAssign.Models
{
    public class HttpSelector : ISelector
    {
        private readonly string _applierName;
        private readonly List<HouseCondition> _conditions;
        private readonly DateTime _startTime;
        private readonly CancellationToken _cancellationToken;
        private readonly string _cookie;
        private readonly int _requestIntervalMs;
        private const int SelectionWindowSeconds = 10;
        private const int MaxRetryAttempts = 3; // 重试次数
        private const int RetryDelayMs = 200; // 重试间隔（毫秒）

        public HttpSelector(
            string applierName,
            List<HouseCondition> communityList,
            string startTime,
            string cookie,
            int requestIntervalMs,
            CancellationToken cancellationToken)
        {
            _applierName = applierName;
            _conditions = communityList ?? throw new ArgumentNullException(nameof(communityList));
            if (!DateTime.TryParseExact(startTime, "yyyy-MM-dd HH:mm:ss", null,
                    System.Globalization.DateTimeStyles.None, out _startTime))
                throw new ArgumentException("开始时间格式必须为 yyyy-MM-dd HH:mm:ss", nameof(startTime));
            _cancellationToken = cancellationToken;
            _cookie = cookie ?? throw new ArgumentNullException(nameof(cookie));
            _requestIntervalMs = requestIntervalMs;
        }

        /// <summary>
        /// 通用重试机制，用于处理网络波动
        /// </summary>
        /// <typeparam name="T">返回值类型</typeparam>
        /// <param name="operation">要执行的异步操作</param>
        /// <param name="operationName">操作名称，用于日志输出</param>
        /// <param name="maxAttempts">最大重试次数</param>
        /// <returns>操作结果</returns>
        private async Task<T?> ExecuteWithRetryAsync<T>(
            Func<Task<T?>> operation, 
            string operationName, 
            int maxAttempts = MaxRetryAttempts) where T : class
        {
            Exception? lastException = null;
            
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    if (_cancellationToken.IsCancellationRequested)
                        return null;

                    var result = await operation();
                    if (result != null)
                    {
                        if (attempt > 1)
                        {
                            Console.WriteLine($"✅ {operationName} 在第 {attempt} 次尝试后成功");
                        }
                        return result;
                    }
                    
                    // 结果为空但没有异常，代表请求失败，继续重试
                    if (attempt == maxAttempts)
                    {
                        Console.WriteLine($"❌ {operationName} 在 {maxAttempts} 次尝试后仍无结果");
                    }
                    else
                    {
                        Console.WriteLine($"⚠️ {operationName} 第 {attempt} 次尝试失败，{RetryDelayMs}ms后重试...");
                        try
                        {
                            await Task.Delay(RetryDelayMs, _cancellationToken);
                        }
                        catch (OperationCanceledException)
                        {
                            Console.WriteLine("重试等待被取消，流程终止");
                            return null;
                        }
                    }
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    
                    if (attempt < maxAttempts)
                    {
                        Console.WriteLine($"⚠️ {operationName} 第 {attempt} 次尝试失败: {ex.Message}，{RetryDelayMs}ms后重试...");
                        try
                        {
                            await Task.Delay(RetryDelayMs, _cancellationToken);
                        }
                        catch (OperationCanceledException)
                        {
                            Console.WriteLine("重试等待被取消，流程终止");
                            return null;
                        }
                    }
                    else
                    {
                        Console.WriteLine($"❌ {operationName} 在 {maxAttempts} 次尝试后最终失败: {ex.Message}");
                    }
                }
            }
            
            return null;
        }

        /// <summary>
        /// 布尔值类型的重试机制
        /// </summary>
        /// <param name="operation">要执行的异步操作</param>
        /// <param name="operationName">操作名称，用于日志输出</param>
        /// <param name="maxAttempts">最大重试次数</param>
        /// <returns>操作结果</returns>
        private async Task<bool> ExecuteWithRetryAsync(
            Func<Task<bool>> operation, 
            string operationName, 
            int maxAttempts = MaxRetryAttempts)
        {
            Exception? lastException = null;
            
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    if (_cancellationToken.IsCancellationRequested)
                        return false;

                    var result = await operation();
                    if (result)
                    {
                        if (attempt > 1)
                        {
                            Console.WriteLine($"✅ {operationName} 在第 {attempt} 次尝试后成功");
                        }
                        return true;
                    }
                    
                    // 结果为false，继续重试
                    if (attempt < maxAttempts)
                    {
                        Console.WriteLine($"⚠️ {operationName} 第 {attempt} 次尝试返回false，{RetryDelayMs}ms后重试...");
                        try
                        {
                            await Task.Delay(RetryDelayMs, _cancellationToken);
                        }
                        catch (OperationCanceledException)
                        {
                            Console.WriteLine("重试等待被取消，流程终止");
                            return false;
                        }
                    }
                    else
                    {
                        Console.WriteLine($"❌ {operationName} 在 {maxAttempts} 次尝试后仍返回false");
                    }
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    
                    if (attempt < maxAttempts)
                    {
                        Console.WriteLine($"⚠️ {operationName} 第 {attempt} 次尝试失败: {ex.Message}，{RetryDelayMs}ms后重试...");
                        try
                        {
                            await Task.Delay(RetryDelayMs, _cancellationToken);
                        }
                        catch (OperationCanceledException)
                        {
                            Console.WriteLine("重试等待被取消，流程终止");
                            return false;
                        }
                    }
                    else
                    {
                        Console.WriteLine($"❌ {operationName} 在 {maxAttempts} 次尝试后最终失败: {ex.Message}");
                    }
                }
            }
            
            return false;
        }

        public async Task RunAsync()
        {
            try
            {
                using var client = CreateHttpClient(_cookie);

                var applierId = await GetApplierIdAsync(client);
                if (applierId == null)
                {
                    Console.WriteLine("获取申请人ID失败，流程终止");
                    return;
                }

                // 检查是否已被取消
                if (_cancellationToken.IsCancellationRequested)
                {
                    Console.WriteLine("操作已取消，流程终止");
                    return;
                }

                var waitResult = await WaitForStartTimeAsync();
                if (!waitResult)
                {
                    Console.WriteLine("等待开始时间被取消，流程终止");
                    return;
                }

                // 再次检查是否已被取消
                if (_cancellationToken.IsCancellationRequested)
                {
                    Console.WriteLine("操作已取消，流程终止");
                    return;
                }

                Console.WriteLine("发包选房开始！");

                var anySuccess = false;
                foreach (var condition in _conditions)
                {
                    // 在每个志愿开始前检查取消状态
                    if (_cancellationToken.IsCancellationRequested)
                    {
                        Console.WriteLine("操作已取消，流程终止");
                        return;
                    }

                    Console.WriteLine($"尝试志愿: {condition}");
                    var roomId = await FindMatchingRoomIdAsync(client, applierId, condition);
                    if (roomId == null)
                    {
                        Console.WriteLine($"未找到符合条件的房源: {condition.CommunityName}");
                        continue;
                    }

                    // 检查是否已被取消
                    if (_cancellationToken.IsCancellationRequested)
                    {
                        Console.WriteLine("操作已取消，流程终止");
                        return;
                    }

                    Console.WriteLine($"找到房源ID: {roomId}，开始发包");
                    var success = await TrySelectRoomAsync(client, applierId, roomId);
                    if (success)
                    {
                        Console.WriteLine($"志愿 {condition.CommunityName} 发包选房成功！");
                        anySuccess = true;
                        break;
                    }
                    
                    Console.WriteLine($"志愿 {condition.CommunityName} 发包未成功，继续下一个志愿");
                }

                if (!anySuccess)
                    Console.WriteLine("所有志愿均未选中，流程结束");
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("操作已取消，流程终止");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"运行过程中发生错误: {ex.Message}");
            }
        }

        public void Stop()
        {
            // 用于取消运行中的 Task
        }

        private HttpClient CreateHttpClient(string cookie)
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Cookie", $"SYS_USER_COOKIE_KEY={cookie}");
            // 设置超时时间，避免长时间等待
            client.Timeout = TimeSpan.FromSeconds(30);
            return client;
        }

        private async Task<string?> GetApplierIdAsync(HttpClient client)
        {
            return await ExecuteWithRetryAsync(async () =>
            {
                var html = await client.GetStringAsync("https://ent.qpgzf.cn/RoomAssign/Index", _cancellationToken);
                var doc = new HtmlDocument();
                doc.LoadHtml(html);
                // 查找 input[name=applierName] 元素
                var node = doc.DocumentNode.SelectSingleNode($"//input[@name='{_applierName}']");
                if (node?.Attributes["value"]?.Value is { } id && !string.IsNullOrEmpty(id))
                {
                    Console.WriteLine($"成功获取申请人ID: {id}");
                    return id;
                }

                Console.WriteLine($"未找到申请人 {_applierName} 对应的 ID");
                return null;
            }, "获取申请人ID");
        }

        /// <summary>
        /// 异步等待开始时间，支持取消操作
        /// </summary>
        /// <returns>true表示正常到达开始时间，false表示被取消</returns>
        private async Task<bool> WaitForStartTimeAsync()
        {
            try
            {
                while (!_cancellationToken.IsCancellationRequested)
                {
                    var now = DateTime.Now;
                    var diff = _startTime - now;
                    if (diff.TotalSeconds <= 1)
                    {
                        Console.WriteLine($"当前时间 {now:yyyy-MM-dd HH:mm:ss}，开始抢！");
                        return true;
                    }

                    Console.WriteLine($"当前时间 {now:yyyy-MM-dd HH:mm:ss}，距离选房开始还有 {diff}");
                    
                    // 使用异步延迟，支持取消令牌
                    await Task.Delay(1000, _cancellationToken);
                }
                
                Console.WriteLine("等待开始时间被用户取消");
                return false;
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("等待开始时间被用户取消");
                return false;
            }
        }

        /// <summary>
        /// 同步版本的等待方法（保留兼容性，但已废弃）
        /// </summary>
        [Obsolete("请使用WaitForStartTimeAsync方法")]
        private void WaitForStartTime()
        {
            while (!_cancellationToken.IsCancellationRequested)
            {
                var now = DateTime.Now;
                var diff = _startTime - now;
                if (diff.TotalSeconds <= 1)
                {
                    Console.WriteLine($"当前时间 {now:yyyy-MM-dd HH:mm:ss}，开始抢！");
                    return;
                }

                Console.WriteLine($"当前时间 {now:yyyy-MM-dd HH:mm:ss}，距离选房开始还有 {diff}");
                Thread.Sleep(1000);
            }
        }

        private async Task<string?> FindMatchingRoomIdAsync(HttpClient client, string applyerId,
            HouseCondition condition)
        {
            return await ExecuteWithRetryAsync(async () =>
            {
                const string url = "https://ent.qpgzf.cn/RoomAssign/SelectRoom";
                var data = new Dictionary<string, string>
                {
                    ["ApplyIDs"] = applyerId,
                    ["IsApplyTalent"] = "0",
                    ["type"] = "1",
                    ["SearchEntity._PageSize"] = "500",
                    ["SearchEntity._PageIndex"] = "1",
                    ["SearchEntity._CommonSearchCondition"] = condition.CommunityName
                };

                var response = await client.PostAsync(url, new FormUrlEncodedContent(data), _cancellationToken);
                var html = await response.Content.ReadAsStringAsync(_cancellationToken);

                var doc = new HtmlDocument();
                doc.LoadHtml(html);
                var rows = doc.DocumentNode.SelectNodes("//table[@id='common-table']/tbody/tr");
                if (rows == null)
                    return null;

                foreach (var row in rows)
                {
                    var cells = row.SelectNodes("./td");
                    if (cells == null || cells.Count < 9)
                        continue;

                    // 解析各列
                    var commName = cells[1].InnerText.Trim();
                    if (commName != condition.CommunityName)
                        continue;

                    if (!int.TryParse(cells[2].InnerText.Trim(), out var buildingNo) ||
                        !HouseCondition.FilterEqual(buildingNo, condition.BuildingNo))
                        continue;

                    var floorText = cells[3].InnerText.Trim();
                    if (!int.TryParse(floorText.Length >= 2 ? floorText[..2] : floorText,
                            out var floorNo) ||
                        !HouseCondition.FilterFloor(floorNo, condition.FloorRange))
                        continue;

                    if (!double.TryParse(cells[5].InnerText.Trim(), out var price) ||
                        !HouseCondition.FilterPrice(price, condition.MaxPrice))
                        continue;

                    if (!double.TryParse(cells[7].InnerText.Trim(), out var area) ||
                        !HouseCondition.FilterArea(area, condition.LeastArea))
                        continue;
                    
                    // 提取 onclick 中的 roomId
                    var actionNode = row.SelectSingleNode(".//a[contains(@onclick, 'selectRooms')]");
                    var onclick = actionNode?.GetAttributeValue("onclick", string.Empty);
                    if (string.IsNullOrEmpty(onclick))
                        continue;

                    const string token = "selectRooms('";
                    var start = onclick.IndexOf(token, StringComparison.Ordinal) + token.Length;
                    var end = onclick.IndexOf('\'', start);
                    if (start < token.Length || end < 0)
                        continue;

                    var roomId = onclick[start..end];
                    Console.WriteLine($"获取到 {condition.CommunityName} 房型 {condition.HouseType} 幢号 {buildingNo} " +
                                      $"楼层 {floorNo}的房源ID: {roomId}");
                    return roomId;
                }

                return null;
            }, $"查找房源ID - {condition.CommunityName}");
        }

        private async Task<bool> TrySelectRoomAsync(HttpClient client, string applyerId, string roomId)
        {
            const string url = "https://ent.qpgzf.cn/RoomAssign/AjaxSelectRoom";
            var endTime = _startTime.AddSeconds(SelectionWindowSeconds);

            while (DateTime.Now < endTime && !_cancellationToken.IsCancellationRequested)
            {
                var success = await ExecuteWithRetryAsync(async () =>
                {
                    return await SelectOnceAsync(client, applyerId, roomId, url);
                }, $"发包选房 - 房源ID:{roomId}");

                if (success)
                    return true;
                
                try
                {
                    await Task.Delay(_requestIntervalMs, _cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine("请求间隔等待被取消，流程终止");
                    return false;
                }
            }

            return false;
        }

        private async Task<bool> SelectOnceAsync(HttpClient client, string applyerId, string roomId, string url)
        {
            var data = new Dictionary<string, string> { ["ApplyIDs"] = applyerId, ["roomID"] = roomId };
            var response = await client.PostAsync(url, new FormUrlEncodedContent(data), _cancellationToken);
            var result = await response.Content.ReadAsStringAsync(_cancellationToken);
            Console.WriteLine(result);
            return result.Contains("成功");
        }
    }
} 