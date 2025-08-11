using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace AvaRoomAssign.Models
{
    /// <summary>
    /// 配置验证器
    /// </summary>
    public static class ConfigValidator
    {
        /// <summary>
        /// 验证配置的完整性和有效性
        /// </summary>
        /// <param name="config">要验证的配置</param>
        /// <returns>验证结果和错误消息</returns>
        public static ValidationResult ValidateConfig(AppConfig config)
        {
            var errors = new List<string>();
            var warnings = new List<string>();

            // 验证基本配置
            ValidateBasicSettings(config, errors, warnings);

            // 验证认证信息
            ValidateAuthenticationSettings(config, errors, warnings);

            // 验证时间设置
            ValidateTimeSettings(config, errors, warnings);

            // 验证社区条件
            ValidateCommunityConditions(config, errors, warnings);

            // 验证手动房间ID
            ValidateManualRoomIds(config, warnings);

            return new ValidationResult
            {
                IsValid = errors.Count == 0,
                Errors = errors,
                Warnings = warnings
            };
        }

        private static void ValidateBasicSettings(AppConfig config, List<string> errors, List<string> warnings)
        {
            // 验证操作模式
            if (config.SelectedOperationMode < 0 || config.SelectedOperationMode > 1)
            {
                errors.Add("操作模式选择无效，必须为0（模拟点击）或1（Http发包）");
            }

            // 验证浏览器类型
            if (config.SelectedBrowserType < 0 || config.SelectedBrowserType > 1)
            {
                errors.Add("浏览器类型选择无效，必须为0（Chrome）或1（Edge）");
            }

            // 验证点击间隔
            if (!int.TryParse(config.ClickInterval, out int interval) || interval < 50)
            {
                warnings.Add("点击间隔设置过小，建议不小于50毫秒以避免服务器限制");
            }
            else if (interval > 5000)
            {
                warnings.Add("点击间隔设置过大，可能会影响抢房效率");
            }
        }

        private static void ValidateAuthenticationSettings(AppConfig config, List<string> errors, List<string> warnings)
        {
            // 验证用户账号
            if (string.IsNullOrWhiteSpace(config.UserAccount))
            {
                errors.Add("用户账号不能为空");
            }
            else if (config.UserAccount.Length < 6)
            {
                warnings.Add("用户账号格式可能不正确，通常应为身份证号或其他标识");
            }

            // 验证申请人姓名
            if (string.IsNullOrWhiteSpace(config.ApplierName))
            {
                errors.Add("申请人姓名不能为空");
            }
            else if (config.ApplierName.Length < 2 || config.ApplierName.Length > 10)
            {
                warnings.Add("申请人姓名长度异常，请检查是否正确");
            }

            // 验证Cookie（仅在HTTP模式下）
            if (config.SelectedOperationMode == 1)
            {
                if (string.IsNullOrWhiteSpace(config.Cookie))
                {
                    errors.Add("HTTP发包模式下Cookie不能为空");
                }
                else if (!IsValidCookie(config.Cookie))
                {
                    warnings.Add("Cookie格式可能不正确，请确保包含有效的会话信息");
                }
            }

            // 验证密码（仅在模拟点击模式下）
            if (config.SelectedOperationMode == 0 && string.IsNullOrWhiteSpace(config.UserPassword))
            {
                errors.Add("模拟点击模式下用户密码不能为空");
            }
        }

        private static void ValidateTimeSettings(AppConfig config, List<string> errors, List<string> warnings)
        {
            // 验证时间格式
            if (!int.TryParse(config.StartHour, out int hour) || hour < 0 || hour > 23)
            {
                errors.Add("开始小时设置无效，必须为0-23之间的数字");
            }

            if (!int.TryParse(config.StartMinute, out int minute) || minute < 0 || minute > 59)
            {
                errors.Add("开始分钟设置无效，必须为0-59之间的数字");
            }

            if (!int.TryParse(config.StartSecond, out int second) || second < 0 || second > 59)
            {
                errors.Add("开始秒数设置无效，必须为0-59之间的数字");
            }

            // 检查是否设置为合理的抢房时间
            if (hour >= 0 && hour < 6)
            {
                warnings.Add("开始时间设置在凌晨，请确认这是预期的抢房时间");
            }
        }

        private static void ValidateCommunityConditions(AppConfig config, List<string> errors, List<string> warnings)
        {
            if (!config.UseManualRoomIds)
            {
                if (config.CommunityConditions == null || config.CommunityConditions.Count == 0)
                {
                    errors.Add("社区条件列表不能为空");
                    return;
                }

                for (int i = 0; i < config.CommunityConditions.Count; i++)
                {
                    var condition = config.CommunityConditions[i];
                    var prefix = $"社区条件[{i + 1}]";

                    if (string.IsNullOrWhiteSpace(condition.CommunityName))
                    {
                        errors.Add($"{prefix}: 社区名称不能为空");
                    }

                    if (condition.BuildingNo < 0)
                    {
                        warnings.Add($"{prefix}: 幢号设置为负数，可能不正确");
                    }

                    if (!string.IsNullOrWhiteSpace(condition.FloorRange))
                    {
                        if (!IsValidFloorRange(condition.FloorRange))
                        {
                            errors.Add($"{prefix}: 楼层范围格式不正确，正确格式如：'3-5' 或 '3,5,7'");
                        }
                    }

                    if (condition.MaxPrice < 0)
                    {
                        warnings.Add($"{prefix}: 最高价格设置为负数，可能不正确");
                    }

                    if (condition.LeastArea < 0)
                    {
                        warnings.Add($"{prefix}: 最小面积设置为负数，可能不正确");
                    }

                    if (condition.HouseType < 0 || condition.HouseType > 2)
                    {
                        errors.Add($"{prefix}: 房屋类型设置无效");
                    }
                }
            }
        }

        private static void ValidateManualRoomIds(AppConfig config, List<string> warnings)
        {
            if (config.UseManualRoomIds && !string.IsNullOrWhiteSpace(config.ManualRoomIds))
            {
                var roomIds = ParseRoomIds(config.ManualRoomIds);
                if (roomIds.Count == 0)
                {
                    warnings.Add("手动房间ID列表格式不正确，未能解析到有效的房间ID");
                }
                else if (roomIds.Count > 50)
                {
                    warnings.Add("手动房间ID数量过多，可能影响处理效率");
                }

                // 检查重复的房间ID
                var duplicates = roomIds.GroupBy(x => x).Where(g => g.Count() > 1).Select(g => g.Key);
                if (duplicates.Any())
                {
                    warnings.Add($"发现重复的房间ID: {string.Join(", ", duplicates)}");
                }
            }
        }

        private static bool IsValidCookie(string cookie)
        {
            // 基本的Cookie格式验证
            if (string.IsNullOrWhiteSpace(cookie))
                return false;

            // 检查是否包含必要的Cookie键
            return cookie.Contains("SYS_USER_COOKIE_KEY") && cookie.Length > 20;
        }

        private static bool IsValidFloorRange(string floorRange)
        {
            if (string.IsNullOrWhiteSpace(floorRange))
                return true;

            // 支持格式：3-5, 3,5,7, 3-5,7,9-11
            var pattern = @"^(\d+(-\d+)?)(,\d+(-\d+)?)*$";
            return Regex.IsMatch(floorRange.Trim(), pattern);
        }

        private static List<string> ParseRoomIds(string manualRoomIds)
        {
            var roomIds = new List<string>();
            if (string.IsNullOrWhiteSpace(manualRoomIds))
                return roomIds;

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
    }

    /// <summary>
    /// 验证结果
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();

        /// <summary>
        /// 获取格式化的错误和警告消息
        /// </summary>
        public string GetFormattedMessages()
        {
            var messages = new List<string>();

            if (Errors.Count > 0)
            {
                messages.Add("❌ 配置错误:");
                messages.AddRange(Errors.Select(e => $"  • {e}"));
            }

            if (Warnings.Count > 0)
            {
                if (messages.Count > 0) messages.Add("");
                messages.Add("⚠️ 配置警告:");
                messages.AddRange(Warnings.Select(w => $"  • {w}"));
            }

            return string.Join("\n", messages);
        }
    }
}