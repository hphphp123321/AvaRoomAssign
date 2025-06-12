using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace AvaRoomAssign.Models
{
    /// <summary>
    /// JSON序列化上下文，用于AOT支持
    /// </summary>
    [JsonSerializable(typeof(AppConfig))]
    [JsonSerializable(typeof(HouseConditionData))]
    [JsonSerializable(typeof(List<HouseConditionData>))]
    [JsonSourceGenerationOptions(
        WriteIndented = true,
        PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
        GenerationMode = JsonSourceGenerationMode.Default)]
    public partial class ConfigJsonContext : JsonSerializerContext
    {
    }

    /// <summary>
    /// 应用程序配置
    /// </summary>
    public class AppConfig
    {
        /// <summary>
        /// 运行模式选择 (0: 模拟点击, 1: Http发包)
        /// </summary>
        public int SelectedOperationMode { get; set; } = 1;

        /// <summary>
        /// 浏览器类型选择 (0: Chrome, 1: Edge)
        /// </summary>
        public int SelectedBrowserType { get; set; } = 1;

        /// <summary>
        /// 用户账号
        /// </summary>
        public string UserAccount { get; set; } = string.Empty;

        /// <summary>
        /// 用户密码（出于安全考虑，可以选择不保存）
        /// </summary>
        public string UserPassword { get; set; } = string.Empty;

        /// <summary>
        /// Cookie值
        /// </summary>
        public string Cookie { get; set; } = string.Empty;

        /// <summary>
        /// 申请人姓名
        /// </summary>
        public string ApplierName { get; set; } = string.Empty;

        /// <summary>
        /// 开始小时
        /// </summary>
        public string StartHour { get; set; } = "09";

        /// <summary>
        /// 开始分钟
        /// </summary>
        public string StartMinute { get; set; } = "00";

        /// <summary>
        /// 开始秒数
        /// </summary>
        public string StartSecond { get; set; } = "00";

        /// <summary>
        /// 点击间隔（毫秒）
        /// </summary>
        public string ClickInterval { get; set; } = "200";

        /// <summary>
        /// 是否自动确认最后一步
        /// </summary>
        public bool AutoConfirm { get; set; } = false;

        /// <summary>
        /// 社区条件列表
        /// </summary>
        public List<HouseConditionData> CommunityConditions { get; set; } = new();

        /// <summary>
        /// 主题设置 (true: 深色主题, false: 浅色主题)
        /// </summary>
        public bool IsDarkTheme { get; set; } = false;

        /// <summary>
        /// 日志区域高度
        /// </summary>
        public double LogAreaHeight { get; set; } = 260;
    }

    /// <summary>
    /// 房屋条件数据类（用于序列化）
    /// </summary>
    public class HouseConditionData
    {
        public string CommunityName { get; set; } = string.Empty;
        public int BuildingNo { get; set; } = 0;
        public string FloorRange { get; set; } = string.Empty;
        public int MaxPrice { get; set; } = 0;
        public int LeastArea { get; set; } = 0;
        public int HouseType { get; set; } = 0; // 对应HouseType枚举值

        /// <summary>
        /// 从HouseCondition转换
        /// </summary>
        public static HouseConditionData FromHouseCondition(HouseCondition condition)
        {
            return new HouseConditionData
            {
                CommunityName = condition.CommunityName,
                BuildingNo = condition.BuildingNo,
                FloorRange = condition.FloorRange,
                MaxPrice = condition.MaxPrice,
                LeastArea = condition.LeastArea,
                HouseType = (int)condition.HouseType
            };
        }

        /// <summary>
        /// 转换为HouseCondition
        /// </summary>
        public HouseCondition ToHouseCondition()
        {
            return new HouseCondition(
                CommunityName,
                BuildingNo,
                FloorRange,
                MaxPrice,
                LeastArea,
                (HouseType)HouseType
            );
        }
    }

    /// <summary>
    /// 配置管理器
    /// </summary>
    public static class ConfigManager
    {
        private static readonly string ConfigFileName = "config.json";
        private static readonly string ConfigPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AvaRoomAssign",
            ConfigFileName
        );

        // 为AOT优化的JSON选项
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            TypeInfoResolver = ConfigJsonContext.Default
        };

        /// <summary>
        /// 保存配置到文件
        /// </summary>
        /// <param name="config">要保存的配置</param>
        public static async Task SaveConfigAsync(AppConfig config)
        {
            try
            {
                // 确保目录存在
                var directory = Path.GetDirectoryName(ConfigPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // 使用源生成器序列化
                var json = JsonSerializer.Serialize(config, ConfigJsonContext.Default.AppConfig);
                await File.WriteAllTextAsync(ConfigPath, json);
                
                Console.WriteLine("✅ 配置已保存");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 保存配置失败: {ex.Message}");
                
                // 尝试写入到备用位置（应用程序目录）
                try
                {
                    var fallbackPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigFileName);
                    var json = JsonSerializer.Serialize(config, ConfigJsonContext.Default.AppConfig);
                    await File.WriteAllTextAsync(fallbackPath, json);
                    Console.WriteLine($"✅ 配置已保存到备用位置: {fallbackPath}");
                }
                catch (Exception fallbackEx)
                {
                    Console.WriteLine($"❌ 备用保存也失败: {fallbackEx.Message}");
                }
            }
        }

        /// <summary>
        /// 从文件加载配置
        /// </summary>
        /// <returns>加载的配置，如果失败则返回默认配置</returns>
        public static async Task<AppConfig> LoadConfigAsync()
        {
            try
            {
                // 首先尝试从用户目录加载
                if (File.Exists(ConfigPath))
                {
                    var json = await File.ReadAllTextAsync(ConfigPath);
                    var config = JsonSerializer.Deserialize(json, ConfigJsonContext.Default.AppConfig);
                    
                    if (config != null)
                    {
                        Console.WriteLine($"✅ 配置已从用户目录加载，包含 {config.CommunityConditions.Count} 个社区条件");
                        return config;
                    }
                }
                
                // 尝试从应用程序目录加载（备用位置）
                var fallbackPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigFileName);
                if (File.Exists(fallbackPath))
                {
                    var json = await File.ReadAllTextAsync(fallbackPath);
                    var config = JsonSerializer.Deserialize(json, ConfigJsonContext.Default.AppConfig);
                    
                    if (config != null)
                    {
                        Console.WriteLine($"✅ 配置已从应用程序目录加载，包含 {config.CommunityConditions.Count} 个社区条件");
                        return config;
                    }
                }

                Console.WriteLine("🔍 配置文件不存在，将创建默认配置");
                return CreateDefaultConfig();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 加载配置失败: {ex.Message}，使用默认配置");
                return CreateDefaultConfig();
            }
        }

        /// <summary>
        /// 同步版本的保存配置（用于快速保存）
        /// </summary>
        /// <param name="config">要保存的配置</param>
        public static void SaveConfig(AppConfig config)
        {
            try
            {
                // 确保目录存在
                var directory = Path.GetDirectoryName(ConfigPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // 使用源生成器序列化
                var json = JsonSerializer.Serialize(config, ConfigJsonContext.Default.AppConfig);
                File.WriteAllText(ConfigPath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 快速保存配置失败: {ex.Message}");
                
                // 尝试写入到备用位置
                try
                {
                    var fallbackPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigFileName);
                    var json = JsonSerializer.Serialize(config, ConfigJsonContext.Default.AppConfig);
                    File.WriteAllText(fallbackPath, json);
                    Console.WriteLine($"✅ 配置已保存到备用位置: {fallbackPath}");
                }
                catch (Exception fallbackEx)
                {
                    Console.WriteLine($"❌ 备用保存也失败: {fallbackEx.Message}");
                }
            }
        }

        /// <summary>
        /// 创建默认配置
        /// </summary>
        /// <returns>默认配置对象</returns>
        private static AppConfig CreateDefaultConfig()
        {
            var config = new AppConfig();
            
            // 添加一个默认的社区条件作为示例
            config.CommunityConditions.Add(new HouseConditionData
            {
                CommunityName = "正荣景苑",
                BuildingNo = 0,
                FloorRange = "3-4,6",
                MaxPrice = 0,
                LeastArea = 0,
                HouseType = (int)HouseType.OneRoom
            });

            return config;
        }

        /// <summary>
        /// 获取配置文件路径（用于调试）
        /// </summary>
        public static string GetConfigPath() => ConfigPath;

        /// <summary>
        /// 删除配置文件
        /// </summary>
        public static void DeleteConfig()
        {
            try
            {
                // 删除主配置文件
                if (File.Exists(ConfigPath))
                {
                    File.Delete(ConfigPath);
                    Console.WriteLine("✅ 配置文件已删除");
                }
                
                // 删除备用配置文件
                var fallbackPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigFileName);
                if (File.Exists(fallbackPath))
                {
                    File.Delete(fallbackPath);
                    Console.WriteLine("✅ 备用配置文件已删除");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 删除配置文件失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 检查配置文件是否存在
        /// </summary>
        public static bool ConfigExists() => File.Exists(ConfigPath) || File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigFileName));

        /// <summary>
        /// 获取实际使用的配置文件路径
        /// </summary>
        public static string GetActualConfigPath()
        {
            if (File.Exists(ConfigPath))
                return ConfigPath;
            
            var fallbackPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigFileName);
            if (File.Exists(fallbackPath))
                return fallbackPath;
            
            return ConfigPath; // 返回默认路径
        }
    }
} 