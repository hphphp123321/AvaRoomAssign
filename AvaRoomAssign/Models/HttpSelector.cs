using System;
using System.Collections.Generic;
using System.Linq;
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

        private string _isApplyTalent = "1"; // 是否是人才公寓，1是，0不是

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
                        LogManager.Warning($"{operationName} 第 {attempt} 次尝试失败，{RetryDelayMs}ms后重试...");
                        try
                        {
                            await Task.Delay(RetryDelayMs, _cancellationToken);
                        }
                        catch (OperationCanceledException)
                        {
                            LogManager.Warning("重试等待被取消，流程终止");
                            return null;
                        }
                    }
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    
                    if (attempt < maxAttempts)
                    {
                        LogManager.Warning($"{operationName} 第 {attempt} 次尝试失败: {ex.Message}，{RetryDelayMs}ms后重试...");
                        try
                        {
                            await Task.Delay(RetryDelayMs, _cancellationToken);
                        }
                        catch (OperationCanceledException)
                        {
                            LogManager.Warning("重试等待被取消，流程终止");
                            return null;
                        }
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
                            LogManager.Success($"{operationName} 在第 {attempt} 次尝试后成功");
                        }
                        return true;
                    }
                    
                    // 结果为false，继续重试
                    if (attempt < maxAttempts)
                    {
                        LogManager.Warning($"{operationName} 第 {attempt} 次尝试返回false，{RetryDelayMs}ms后重试...");
                        try
                        {
                            await Task.Delay(RetryDelayMs, _cancellationToken);
                        }
                        catch (OperationCanceledException)
                        {
                            LogManager.Warning("重试等待被取消，流程终止");
                            return false;
                        }
                    }
                    else
                    {
                        LogManager.Error($"{operationName} 在 {maxAttempts} 次尝试后仍返回false");
                    }
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    
                    if (attempt < maxAttempts)
                    {
                        LogManager.Warning($"{operationName} 第 {attempt} 次尝试失败: {ex.Message}，{RetryDelayMs}ms后重试...");
                        try
                        {
                            await Task.Delay(RetryDelayMs, _cancellationToken);
                        }
                        catch (OperationCanceledException)
                        {
                            LogManager.Warning("重试等待被取消，流程终止");
                            return false;
                        }
                    }
                    else
                    {
                        LogManager.Error($"{operationName} 在 {maxAttempts} 次尝试后最终失败: {ex.Message}");
                    }
                }
            }
            
            return false;
        }

        public async Task RunAsync()
        {
            if (!ValidateCookie(_cookie))
            {
                LogManager.Error("cookie字符串为空，流程终止");
                return;
            }
            
            try
            {
                using var client = CreateHttpClient(_cookie);

                var applierId = await GetApplierIdAsync(client);
                if (applierId == null)
                {
                    LogManager.Error("获取申请人ID失败，流程终止");
                    return;
                }

                // 检查是否已被取消
                if (_cancellationToken.IsCancellationRequested)
                {
                    LogManager.Warning("操作已取消，流程终止");
                    return;
                }

                var waitResult = await WaitForStartTimeAsync();
                if (!waitResult)
                {
                    LogManager.Warning("等待开始时间被取消，流程终止");
                    return;
                }

                // 再次检查是否已被取消
                if (_cancellationToken.IsCancellationRequested)
                {
                    LogManager.Warning("操作已取消，流程终止");
                    return;
                }

                LogManager.Success("发包选房开始！");

                var anySuccess = false;
                foreach (var condition in _conditions)
                {
                    // 在每个志愿开始前检查取消状态
                    if (_cancellationToken.IsCancellationRequested)
                    {
                        LogManager.Warning("操作已取消，流程终止");
                        return;
                    }

                    LogManager.Info($"尝试志愿: {condition}");
                    var roomId = await FindMatchingRoomIdAsync(client, applierId, condition);
                    if (roomId == null)
                    {
                        LogManager.Warning($"未找到符合条件的房源: {condition.CommunityName}");
                        continue;
                    }

                    // 检查是否已被取消
                    if (_cancellationToken.IsCancellationRequested)
                    {
                        LogManager.Warning("操作已取消，流程终止");
                        return;
                    }

                    LogManager.Info($"找到房源ID: {roomId}，开始发包");
                    var success = await TrySelectRoomAsync(client, applierId, roomId);
                    if (success)
                    {
                        LogManager.Success($"志愿 {condition.CommunityName} 发包选房成功！");
                        anySuccess = true;
                        break;
                    }
                    
                    LogManager.Warning($"志愿 {condition.CommunityName} 发包未成功，继续下一个志愿");
                }

                if (!anySuccess)
                    LogManager.Info("所有志愿均未选中，流程结束");
            }
            catch (OperationCanceledException)
            {
                LogManager.Warning("操作已取消，流程终止");
            }
            catch (Exception ex)
            {
                LogManager.Error($"运行过程中发生错误: {ex.Message}");
            }
        }

        public void Stop()
        {
            // 用于取消运行中的 Task
        }

        private HttpClient CreateHttpClient(string cookie)
        {
            var client = new HttpClient();
            
            // 确保Cookie格式正确
            var formattedCookie = cookie;
            if (!formattedCookie.StartsWith("SYS_USER_COOKIE_KEY=SYS_USER_COOKIE_KEY="))
            {
                client.DefaultRequestHeaders.Add("Cookie", $"SYS_USER_COOKIE_KEY={formattedCookie}");
            }
            else
            {
                client.DefaultRequestHeaders.Add("Cookie", formattedCookie);
            }
            
            // 设置超时时间，避免长时间等待
            client.Timeout = TimeSpan.FromSeconds(30);
            return client;
        }

        private async Task<string?> GetApplierIdAsync(HttpClient client)
        {
            return await ExecuteWithRetryAsync(async () =>
            {
                var response = await client.GetAsync("https://ent.qpgzf.cn/RoomAssign/Index", _cancellationToken);
                var html = await response.Content.ReadAsStringAsync(_cancellationToken);
                
                // 检查是否被重定向到登录页面
                var finalUrl = response.RequestMessage?.RequestUri?.ToString() ?? "";
                if (finalUrl.Contains("/sysloginmanage/CompanyIndex") || 
                    finalUrl.Contains("CompanyIndex") || 
                    response.Headers.Location?.ToString().Contains("CompanyIndex") == true)
                {
                    LogManager.Error("Cookie无效，访问房源分配页面被重定向到登录页");
                    return null;
                }

                var doc = new HtmlDocument();
                doc.LoadHtml(html);
                // 查找 input[name=applierName] 元素
                var node = doc.DocumentNode.SelectSingleNode($"//input[@name='{_applierName}']");
                if (node?.Attributes["value"]?.Value is { } id && !string.IsNullOrEmpty(id))
                {
                    _isApplyTalent = node?.Attributes["isapplytalent"]?.Value ?? "1";
                    LogManager.Success($"成功获取申请人{_applierName}的ID: {id}，类型为{(_isApplyTalent == "1" ? "人才公寓" : "公租房")}");
                    return id;
                }

                // 检查页面内容是否包含登录相关信息
                if (html.Contains("用户登录") || html.Contains("login") || html.Contains("请登录"))
                {
                    LogManager.Error("Cookie无效，页面包含登录相关内容");
                    return null;
                }

                LogManager.Warning($"未找到申请人 {_applierName} 对应的 ID");
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
                        LogManager.Success($"当前时间 {now:yyyy-MM-dd HH:mm:ss}，开始抢！");
                        return true;
                    }

                    LogManager.Info($"当前时间 {now:yyyy-MM-dd HH:mm:ss}，距离选房开始还有 {diff}");
                    
                    // 使用异步延迟，支持取消令牌
                    await Task.Delay(1000, _cancellationToken);
                }
                
                LogManager.Warning("等待开始时间被用户取消");
                return false;
            }
            catch (OperationCanceledException)
            {
                LogManager.Warning("等待开始时间被用户取消");
                return false;
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
                    ["IsApplyTalent"] = "1",
                    ["type"] = "1",
                    ["SearchEntity._PageSize"] = "300",
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
                    return roomId;
                }

                return null;
            }, $"查找房源ID - {condition.CommunityName}");
        }

        /// <summary>
        /// 获取某个条件下的所有房间ID（不只是第一个匹配的）
        /// </summary>
        /// <param name="client">HTTP客户端</param>
        /// <param name="applyerId">申请人ID</param>
        /// <param name="condition">房屋条件</param>
        /// <returns>所有匹配的房间ID列表</returns>
        public async Task<List<string>> FindAllMatchingRoomIdsAsync(HttpClient client, string applyerId, HouseCondition condition)
        {
            return await ExecuteWithRetryAsync(async () =>
            {
                const string url = "https://ent.qpgzf.cn/RoomAssign/SelectRoom";
                var data = new Dictionary<string, string>
                {
                    ["ApplyIDs"] = "c1317b48-d7dc-4fdb-99c1-b03000f6dcb9", // 必须要是有公租房抢房资格才行
                    ["IsApplyTalent"] = _isApplyTalent,
                    ["type"] = "1",
                    ["SearchEntity._PageSize"] = "300",
                    ["SearchEntity._PageIndex"] = "1",
                    ["SearchEntity._CommonSearchCondition"] = condition.CommunityName
                };

                var response = await client.PostAsync(url, new FormUrlEncodedContent(data), _cancellationToken);
                var html = await response.Content.ReadAsStringAsync(_cancellationToken);

                var doc = new HtmlDocument();
                doc.LoadHtml(html);
                var rows = doc.DocumentNode.SelectNodes("//table[@id='common-table']/tbody/tr");
                if (rows == null)
                    return new List<string>();

                var roomIds = new List<string>();
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
                    roomIds.Add(roomId);
                }

                return roomIds;
            }, $"查找所有房源ID - {condition.CommunityName}") ?? new List<string>();
        }

        /// <summary>
        /// 执行房间ID预获取
        /// </summary>
        /// <param name="conditions">房屋条件列表</param>
        /// <returns>条件与房间ID的映射字典</returns>
        public async Task<Dictionary<string, List<string>>> PreFetchRoomIdsAsync(List<HouseCondition> conditions)
        {
            var result = new Dictionary<string, List<string>>();
            
            try
            {
                using var client = CreateHttpClient(_cookie);

                var applierId = await GetApplierIdAsync(client);
                if (applierId == null)
                {
                    return result;
                }

                foreach (var condition in conditions)
                {
                    if (_cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    var conditionKey = ConditionRoomIdMapping.GenerateConditionKey(condition);
                    var roomIds = await FindAllMatchingRoomIdsAsync(client, applierId, condition);
                    
                    if (roomIds.Count > 0)
                    {
                        result[conditionKey] = roomIds;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"预获取房间ID时出错: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// 使用预获取的房间ID运行选房流程
        /// </summary>
        /// <param name="roomIdMappings">预获取的房间ID映射</param>
        public async Task RunWithPreFetchedRoomIdsAsync(Dictionary<string, List<string>> roomIdMappings)
        {
            try
            {
                using var client = CreateHttpClient(_cookie);

                var applierId = await GetApplierIdAsync(client);
                if (applierId == null)
                {
                    return;
                }

                // 检查是否已被取消
                if (_cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                var waitResult = await WaitForStartTimeAsync();
                if (!waitResult)
                {
                    return;
                }

                // 再次检查是否已被取消
                if (_cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                // 收集所有预获取的房间ID
                var allRoomIds = new List<string>();
                var roomIdToCondition = new Dictionary<string, HouseCondition>();

                foreach (var condition in _conditions)
                {
                    if (_cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    var conditionKey = ConditionRoomIdMapping.GenerateConditionKey(condition);
                    if (roomIdMappings.TryGetValue(conditionKey, out var roomIds) && roomIds.Count > 0)
                    {
                        foreach (var roomId in roomIds)
                        {
                            allRoomIds.Add(roomId);
                            roomIdToCondition[roomId] = condition;
                        }
                    }
                    else
                    {
                        // 如果没有预获取的房间ID，使用原有的查找方法
                        var roomId = await FindMatchingRoomIdAsync(client, applierId, condition);
                        if (roomId != null)
                        {
                            allRoomIds.Add(roomId);
                            roomIdToCondition[roomId] = condition;
                        }
                    }
                }

                if (allRoomIds.Count == 0)
                {
                    return;
                }

                // 尝试所有房间ID
                var anySuccess = false;
                foreach (var roomId in allRoomIds)
                {
                    if (_cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    var condition = roomIdToCondition[roomId];
                    var success = await TrySelectRoomAsync(client, applierId, roomId);
                    if (success)
                    {
                        anySuccess = true;
                        break;
                    }
                }

                if (!anySuccess)
                {
                    // 日志输出会在TrySelectRoomAsync中处理
                }
            }
            catch (OperationCanceledException)
            {
                // 操作被取消
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"运行预获取选房流程时发生错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 使用手动输入的房间ID运行选房流程
        /// </summary>
        /// <param name="manualRoomIds">手动输入的房间ID字符串（支持换行符和逗号分隔）</param>
        public async Task RunWithManualRoomIdsAsync(string manualRoomIds)
        {
            // 检验手动输入的房间ID是否为空合法
            if (string.IsNullOrWhiteSpace(manualRoomIds))
            {
                LogManager.Error("未找到有效的房间ID，请检查输入格式");
                return;
            }

            try
            {
                using var client = CreateHttpClient(_cookie);

                var applierId = await GetApplierIdAsync(client);
                if (applierId == null)
                {
                    LogManager.Error("获取申请人ID失败，流程终止");
                    return;
                }

                // 解析手动输入的房间ID
                var roomIds = ParseManualRoomIds(manualRoomIds);
                if (roomIds.Count == 0)
                {
                    LogManager.Error("未找到有效的房间ID，请检查输入格式");
                    return;
                }

                LogManager.Success($"解析到 {roomIds.Count} 个房间ID: {string.Join(", ", roomIds)}");

                // 检查是否已被取消
                if (_cancellationToken.IsCancellationRequested)
                {
                    LogManager.Warning("操作已取消，流程终止");
                    return;
                }

                var waitResult = await WaitForStartTimeAsync();
                if (!waitResult)
                {
                    LogManager.Warning("等待开始时间被取消，流程终止");
                    return;
                }

                // 再次检查是否已被取消
                if (_cancellationToken.IsCancellationRequested)
                {
                    LogManager.Warning("操作已取消，流程终止");
                    return;
                }

                LogManager.Success("发包选房开始！使用手动输入的房间ID");

                // 尝试所有手动输入的房间ID
                var anySuccess = false;
                for (int i = 0; i < roomIds.Count; i++)
                {
                    var roomId = roomIds[i];
                    
                    if (_cancellationToken.IsCancellationRequested)
                    {
                        LogManager.Warning("操作已取消，流程终止");
                        return;
                    }

                    LogManager.Info($"正在尝试房间ID [{i + 1}/{roomIds.Count}]: {roomId}");
                    var success = await TrySelectRoomAsync(client, applierId, roomId);
                    if (success)
                    {
                        LogManager.Success($"房间ID {roomId} 发包选房成功！");
                        anySuccess = true;
                        break;
                    }
                    
                    LogManager.Warning($"房间ID {roomId} 发包未成功，继续下一个");
                }

                if (!anySuccess)
                {
                    LogManager.Info("所有手动输入的房间ID均未选中，流程结束");
                }
            }
            catch (OperationCanceledException)
            {
                LogManager.Warning("操作已取消，流程终止");
            }
            catch (Exception ex)
            {
                LogManager.Error($"运行手动房间ID选房流程时发生错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 解析手动输入的房间ID字符串
        /// </summary>
        /// <param name="manualRoomIds">输入的房间ID字符串</param>
        /// <returns>解析后的房间ID列表</returns>
        private List<string> ParseManualRoomIds(string manualRoomIds)
        {
            var roomIds = new List<string>();
            
            if (string.IsNullOrWhiteSpace(manualRoomIds))
            {
                return roomIds;
            }

            // 支持换行符和逗号分隔
            var separators = new[] { '\n', '\r', ',' };
            var parts = manualRoomIds.Split(separators, StringSplitOptions.RemoveEmptyEntries);

            foreach (var part in parts)
            {
                var trimmed = part.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                {
                    roomIds.Add(trimmed);
                }
            }

            return roomIds;
        }

        private async Task<bool> TrySelectRoomAsync(HttpClient client, string applyerId, string roomId)
        {
            const string url = "https://ent.qpgzf.cn/RoomAssign/AjaxSelectRoom";
            
            return await ExecuteWithRetryAsync(async () =>
            {
                return await SelectOnceAsync(client, applyerId, roomId, url);
            }, $"发包选房 - 房源ID:{roomId}");
        }

        private async Task<bool> SelectOnceAsync(HttpClient client, string applyerId, string roomId, string url)
        {
            var data = new Dictionary<string, string> { ["ApplyIDs"] = applyerId, ["roomID"] = roomId };
            var response = await client.PostAsync(url, new FormUrlEncodedContent(data), _cancellationToken);
            var result = await response.Content.ReadAsStringAsync(_cancellationToken);
            LogManager.Info(result);
            return result.Contains("成功");
        }

        /// <summary>
        /// 检验cookie字符串是否有效
        /// </summary>
        /// <param name="cookie">cookie字符串</param>
        /// <returns>是否有效</returns>
        private bool ValidateCookie(string cookie)
        {
            if (string.IsNullOrEmpty(cookie))
            {
                LogManager.Error("cookie字符串为空，流程终止");
                return false;
            }

            // 格式化Cookie值，确保格式正确
            // 如果不包含"SYS_USER_COOKIE_KEY="前缀，添加它
            if (!cookie.Contains("SYS_USER_COOKIE_KEY="))
            {
                // 如果cookie只是值部分，添加前缀
                if (!string.IsNullOrWhiteSpace(cookie))
                {
                    LogManager.Info("Cookie格式化：添加SYS_USER_COOKIE_KEY前缀");
                    return true; // 在CreateHttpClient中会正确格式化
                }
                else
                {
                    LogManager.Error("cookie字符串格式不正确，流程终止");
                    return false;
                }
            }

            // 正确格式是类似于SYS_USER_COOKIE_KEY=D6B3E5vZth20kjGPDFKouaBnr/SSRbEk1QAtd7dGwDbSCoVjpu/o5A==
            // 而不是SYS_USER_COOKIE_KEY=SYS_USER_COOKIE_KEY=D6B3E5vZth20kjGPDFKouaBnr/SSRbEk1QAtd7dGwDbSCoVjpu/o5A==
            // 如果检测到这种格式，则自动删除多余的SYS_USER_COOKIE_KEY=
            if (cookie.Contains("SYS_USER_COOKIE_KEY=SYS_USER_COOKIE_KEY="))
            {
                LogManager.Warning("检测到重复的Cookie前缀，自动修正");
            }
            return true;
        }
    }
} 