using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using AvaRoomAssign.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Edge;
using Avalonia.Threading;
using System.Linq;
using System.Collections.Specialized;

namespace AvaRoomAssign.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _logText = string.Empty;

    [ObservableProperty]
    private int _selectedOperationMode = 1; // 默认选择Http发包

    [ObservableProperty]
    private int _selectedBrowserType = 1; // 默认选择Edge

    [ObservableProperty]
    private string _userAccount = "91310118832628001D";

    [ObservableProperty]
    private string _userPassword = string.Empty;

    [ObservableProperty]
    private string _cookie = "SYS_USER_COOKIE_KEY=DuAZwOAf/NLjgFDzEGln40YCqW1fow8gYHTd64HiogeCyqK3B2HgXg==";

    [ObservableProperty]
    private string _applierName = "高少炜";

    [ObservableProperty]
    private DateTime _startDate = DateTime.Today.AddDays(1);

    [ObservableProperty]
    private string _startHour = "09";

    [ObservableProperty]
    private string _startMinute = "00";

    [ObservableProperty]
    private string _startSecond = "00";

    [ObservableProperty]
    private string _clickInterval = "200";

    [ObservableProperty]
    private bool _autoConfirm = false;

    [ObservableProperty]
    private bool _isRunning = false;

    [ObservableProperty]
    private bool _isFetchingRoomIds = false;

    [ObservableProperty]
    private ObservableCollection<HouseCondition> _communityConditions = new();

    // 添加手动房间ID列表属性
    [ObservableProperty]
    private string _manualRoomIds = string.Empty;

    // 添加用于切换社区条件设置和手动房间ID的属性
    [ObservableProperty]
    private bool _useManualRoomIds = false;

    public List<string> OperationModes { get; } = new() { "模拟点击", "Http发包" };
    public List<string> BrowserTypes { get; } = new() { "Chrome", "Edge" };
    public List<string> HouseTypes { get; } = new() { "一居室", "二居室", "三居室" };

    private CancellationTokenSource? _cancellationTokenSource;
    private ISelector? _currentSelector;
    private bool _isLoadingConfig = false; // 防止加载配置时触发保存
    private Dictionary<string, List<string>> _preFetchedRoomIds = new(); // 预获取的房间ID
    
    /// <summary>
    /// 日志更新事件，用于通知UI自动滚动到底部
    /// </summary>
    public event EventHandler? LogUpdated;

    public MainWindowViewModel()
    {
        try
        {
            // 先初始化日志管理器，再加载配置
            Task.Run(async () =>
            {
                // 首先在UI线程上初始化日志管理器
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    try
                    {
                        // 先初始化日志管理器
                        LogManager.Initialize(AppendLog);
                        LogManager.Success("日志系统已初始化");
                    }
                    catch (Exception ex)
                    {
                        LogManager.Error($"初始化日志系统时出错: {ex.Message}");
                    }
                });
                
                // 然后加载配置
                await LoadConfigurationAsync();
                
                // 最后输出配置加载完成的消息
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    LogManager.Success($"配置加载完成，包含 {CommunityConditions.Count} 个社区条件");
                    LogManager.Info($"配置文件路径: {ConfigManager.GetConfigPath()}");
                });
            });
        }
        catch (Exception ex)
        {
            LogManager.Error($"❌ ViewModel初始化失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 加载配置
    /// </summary>
    private async Task LoadConfigurationAsync()
    {
        try
        {
            _isLoadingConfig = true;
            var config = await ConfigManager.LoadConfigAsync();
            
            // 在UI线程中更新属性
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                // 更新基本配置
                SelectedOperationMode = config.SelectedOperationMode;
                SelectedBrowserType = config.SelectedBrowserType;
                UserAccount = config.UserAccount;
                UserPassword = config.UserPassword;
                Cookie = config.Cookie;
                ApplierName = config.ApplierName;
                StartHour = config.StartHour;
                StartMinute = config.StartMinute;
                StartSecond = config.StartSecond;
                ClickInterval = config.ClickInterval;
                AutoConfirm = config.AutoConfirm;
                
                // 添加加载手动房间ID
                ManualRoomIds = config.ManualRoomIds;
                
                // 添加加载使用手动房间ID的开关状态
                UseManualRoomIds = config.UseManualRoomIds;
                
                // 更新社区条件
                CommunityConditions.Clear();
                foreach (var conditionData in config.CommunityConditions)
                {
                    CommunityConditions.Add(conditionData.ToHouseCondition());
                }
                
                // 如果没有社区条件，添加默认条件
                if (CommunityConditions.Count == 0)
                {
                    var defaultCondition = new HouseCondition("正荣景苑", 0, "3-4,6", 0, 0, HouseType.OneRoom);
                    CommunityConditions.Add(defaultCondition);
                }
                
                // 加载房间ID映射
                await LoadRoomIdMappingsAsync(config);
            });
        }
        catch (Exception ex)
        {
            LogManager.Error($"❌ 加载配置失败: {ex.Message}");
        }
        finally
        {
            _isLoadingConfig = false;
            
            // 加载完成后开始监听属性变化
            StartPropertyChangeMonitoring();
        }
    }

    /// <summary>
    /// 保存当前配置
    /// </summary>
    private async Task SaveConfigurationAsync()
    {
        if (_isLoadingConfig) return; // 加载配置时不保存
        
        try
        {
            var config = new AppConfig
            {
                SelectedOperationMode = SelectedOperationMode,
                SelectedBrowserType = SelectedBrowserType,
                UserAccount = UserAccount,
                UserPassword = UserPassword,
                Cookie = Cookie,
                ApplierName = ApplierName,
                StartHour = StartHour,
                StartMinute = StartMinute,
                StartSecond = StartSecond,
                ClickInterval = ClickInterval,
                AutoConfirm = AutoConfirm,
                CommunityConditions = CommunityConditions.Select(HouseConditionData.FromHouseCondition).ToList(),
                ManualRoomIds = ManualRoomIds,
                UseManualRoomIds = UseManualRoomIds
            };
            
            // 使用异步保存，避免阻塞UI
            await Task.Run(() => ConfigManager.SaveConfig(config));
        }
        catch (Exception ex)
        {
            LogManager.Error($"❌ 保存配置失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 开始监听属性变化
    /// </summary>
    private void StartPropertyChangeMonitoring()
    {
        // 监听普通属性变化
        PropertyChanged += async (sender, e) =>
        {
            if (_isLoadingConfig) return;
            
            // 排除不需要保存的属性
            var excludeProperties = new[] { nameof(LogText), nameof(IsRunning) };
            if (excludeProperties.Contains(e.PropertyName)) return;
            
            await SaveConfigurationAsync();
        };
        
        // 监听集合变化
        CommunityConditions.CollectionChanged += async (sender, e) =>
        {
            if (_isLoadingConfig) return;
            await SaveConfigurationAsync();
        };
    }

    [RelayCommand]
    private void AddCommunity()
    {
        try
        {
            var newCondition = new HouseCondition("新社区", 1, "1-3", 2000, 50, HouseType.OneRoom);
            CommunityConditions.Add(newCondition);
            LogManager.Success($"已添加新的社区条件: {newCondition.CommunityName}");
        }
        catch (Exception ex)
        {
            LogManager.Error("添加社区条件时出错", ex);
        }
    }

    [RelayCommand]
    private void DeleteCommunity(HouseCondition condition)
    {
        try
        {
            var removed = CommunityConditions.Remove(condition);
            if (removed)
            {
                LogManager.Success($"已删除社区条件: {condition.CommunityName}");
            }
        }
        catch (Exception ex)
        {
            LogManager.Error("删除社区条件时出错", ex);
        }
    }

    [RelayCommand]
    private async Task StartProcessAsync()
    {
        if (IsRunning) return;

        IsRunning = true;
        _cancellationTokenSource = new CancellationTokenSource();

        try
        {
            var operationMode = SelectedOperationMode == 0 ? OperationMode.Click : OperationMode.Http;
            var driverType = SelectedBrowserType == 0 ? DriverType.Chrome : DriverType.Edge;
            var startTime = $"{StartDate:yyyy-MM-dd} {StartHour}:{StartMinute}:{StartSecond}";

            if (!int.TryParse(ClickInterval, out int clickInterval))
            {
                clickInterval = 200;
            }

            var communityList = new List<HouseCondition>(CommunityConditions);

            await Task.Run(async () =>
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(ApplierName))
                    {
                        LogManager.Error("申请人名称不能为空");
                        return;
                    }

                    if (communityList.Count == 0)
                    {
                        LogManager.Error("社区条件不能为空");
                        return;
                    }

                    switch (operationMode)
                    {
                        case OperationMode.Click:
                            LogManager.Info("正在启动浏览器...");
                            var driver = GetDriver(driverType);
                            
                            _currentSelector = new DriverSelector(
                                driver: driver,
                                userAccount: UserAccount,
                                userPassword: UserPassword,
                                applyerName: ApplierName,
                                communityList: communityList,
                                startTime: startTime,
                                cancellationToken: _cancellationTokenSource.Token,
                                autoConfirm: AutoConfirm,
                                clickIntervalMs: clickInterval,
                                cookie: Cookie);
                            break;

                        case OperationMode.Http:
                            var httpSelector = new HttpSelector(
                                applierName: ApplierName,
                                communityList: communityList,
                                startTime: startTime,
                                cookie: Cookie,
                                requestIntervalMs: clickInterval,
                                cancellationToken: _cancellationTokenSource.Token);
                            
                            _currentSelector = httpSelector;
                            
                            // 检查是否启用了手动输入房间ID模式且有手动输入的房间ID
                            if (UseManualRoomIds && !string.IsNullOrWhiteSpace(ManualRoomIds))
                            {
                                LogManager.Info("使用手动输入的房间ID列表进行抢房");
                                await httpSelector.RunWithManualRoomIdsAsync(ManualRoomIds);
                                return; // 使用手动房间ID，直接返回
                            }
                            
                            // 检查是否有预获取的房间ID
                            if (_preFetchedRoomIds.Count > 0)
                            {
                                await httpSelector.RunWithPreFetchedRoomIdsAsync(_preFetchedRoomIds);
                                return; // 使用预获取的房间ID，直接返回
                            }
                            break;

                        default:
                            throw new ArgumentOutOfRangeException();
                    }

                    await _currentSelector.RunAsync();
                }
                catch (Exception ex)
                {
                    LogManager.Error("自动化过程中出现错误", ex);
                }
                finally
                {
                    Dispatcher.UIThread.Post(() => IsRunning = false);
                }
            }, _cancellationTokenSource.Token);
        }
        catch (Exception ex)
        {
            LogManager.Error("启动过程中出现错误", ex);
            IsRunning = false;
        }
    }

    [RelayCommand]
    private void StopProcess()
    {
        try
        {
            _cancellationTokenSource?.Cancel();
            _currentSelector?.Stop();
            LogManager.Warning("已停止抢房");
        }
        catch (Exception ex)
        {
            LogManager.Error("停止抢房时出现错误", ex);
        }
        finally
        {
            IsRunning = false;
        }
    }

    [RelayCommand]
    private void OpenGitHub()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "https://github.com/hphphp123321/AvaRoomAssign",
                UseShellExecute = true
            };
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            LogManager.Error("打开GitHub链接失败", ex);
        }
    }

    /// <summary>
    /// 重置配置到默认值
    /// </summary>
    [RelayCommand]
    private async Task ResetConfigurationAsync()
    {
        try
        {
            _isLoadingConfig = true;
            
            // 删除配置文件
            ConfigManager.DeleteConfig();
            
            // 重新加载默认配置
            await LoadConfigurationAsync();
            
            LogManager.Success("配置已重置为默认值");
        }
        catch (Exception ex)
        {
            LogManager.Error("重置配置失败", ex);
        }
    }

    /// <summary>
    /// 手动保存配置
    /// </summary>
    [RelayCommand]
    private async Task SaveConfigurationManuallyAsync()
    {
        try
        {
            await SaveConfigurationAsync();
            LogManager.Success("配置已手动保存");
        }
        catch (Exception ex)
        {
            LogManager.Error("手动保存配置失败", ex);
        }
    }

    [RelayCommand]
    private async Task FetchRoomIdsAsync()
    {
        if (IsFetchingRoomIds || IsRunning) return;

        IsFetchingRoomIds = true;

        try
        {
            // 检查HTTP发包模式
            if (SelectedOperationMode != 1)
            {
                LogManager.Warning("获取房间ID功能仅在HTTP发包模式下可用");
                return;
            }

            // 检查必要参数
            if (string.IsNullOrWhiteSpace(Cookie))
            {
                LogManager.Error("Cookie不能为空，请先配置Cookie");
                return;
            }

            if (string.IsNullOrWhiteSpace(ApplierName))
            {
                LogManager.Error("申请人名称不能为空");
                return;
            }

            if (CommunityConditions.Count == 0)
            {
                LogManager.Error("请先添加社区条件");
                return;
            }

            LogManager.Info("开始获取房间ID...");

            var cancellationTokenSource = new CancellationTokenSource();
            var communityList = new List<HouseCondition>(CommunityConditions);

            await Task.Run(async () =>
            {
                try
                {
                    // 创建HttpSelector用于预获取房间ID
                    var httpSelector = new HttpSelector(
                        applierName: ApplierName,
                        communityList: communityList,
                        startTime: DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                        cookie: Cookie,
                        requestIntervalMs: 1000,
                        cancellationToken: cancellationTokenSource.Token);

                    // 执行预获取
                    var roomIdMappings = await httpSelector.PreFetchRoomIdsAsync(communityList);
                    
                    // 保存到配置文件
                    await SaveRoomIdMappingsAsync(roomIdMappings, communityList);

                    // 缓存到内存
                    _preFetchedRoomIds = roomIdMappings;

                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        var totalRoomIds = roomIdMappings.Values.Sum(list => list.Count);
                        
                    
                        foreach (var (key, roomIds) in roomIdMappings)
                        {
                            var condition = communityList.FirstOrDefault(c => 
                                ConditionRoomIdMapping.GenerateConditionKey(c) == key);
                            if (condition != null)
                            {
                                LogManager.Info($"  {condition.CommunityName}: {roomIds.Count} 个房间");
                            }
                        }

                        if (totalRoomIds > 0)
                        {
                            LogManager.Success($"房间ID获取完成！共获取到 {totalRoomIds} 个房间ID，并已保存到配置文件");
                            LogManager.Info($"配置文件路径: {ConfigManager.GetConfigPath()}");
                            LogManager.Success($"下次抢房时会自动使用这些房间ID加速抢房，无需再次获取了");
                        }
                        else
                        {
                            LogManager.Warning("未获取到任何房间ID，请检查配置是否正确或者是否在抢房时间范围内");
                        }
                    });
                }
                catch (Exception ex)
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        LogManager.Error("获取房间ID时出错", ex);
                    });
                }
            }, cancellationTokenSource.Token);
        }
        catch (Exception ex)
        {
            LogManager.Error("启动获取房间ID过程中出现错误", ex);
        }
        finally
        {
            IsFetchingRoomIds = false;
        }
    }

    /// <summary>
    /// 保存房间ID映射到配置文件
    /// </summary>
    private async Task SaveRoomIdMappingsAsync(Dictionary<string, List<string>> roomIdMappings, List<HouseCondition> conditions)
    {
        try
        {
            var config = await ConfigManager.LoadConfigAsync();
            
            // 清空现有映射
            config.RoomIdMappings.Clear();
            
            // 添加新的映射
            foreach (var condition in conditions)
            {
                var conditionKey = ConditionRoomIdMapping.GenerateConditionKey(condition);
                var mapping = ConditionRoomIdMapping.FromHouseCondition(condition);
                
                if (roomIdMappings.TryGetValue(conditionKey, out var roomIds))
                {
                    mapping.RoomIds = roomIds;
                }
                
                config.RoomIdMappings.Add(mapping);
            }
            
            await ConfigManager.SaveConfigAsync(config);
        }
        catch (Exception ex)
        {
            LogManager.Error("保存房间ID映射失败", ex);
        }
    }

    /// <summary>
    /// 从配置文件加载房间ID映射
    /// </summary>
    private async Task LoadRoomIdMappingsAsync(AppConfig? config = null)
    {
        try
        {
            // 如果没有传入配置，则加载配置；否则使用传入的配置
            config ??= await ConfigManager.LoadConfigAsync();
            _preFetchedRoomIds.Clear();
            
            foreach (var mapping in config.RoomIdMappings)
            {
                if (mapping.RoomIds.Count > 0)
                {
                    _preFetchedRoomIds[mapping.ConditionKey] = mapping.RoomIds;
                }
            }
            
            if (_preFetchedRoomIds.Count > 0)
            {
                var totalRoomIds = _preFetchedRoomIds.Values.Sum(list => list.Count);
                LogManager.Success($"已加载 {totalRoomIds} 个预获取的房间ID");
            }
        }
        catch (Exception ex)
        {
            LogManager.Error("加载房间ID映射失败", ex);
        }
    }

    private void AppendLog(string text)
    {
        // 确保在UI线程上执行
        Dispatcher.UIThread.Post(() =>
        {
            // 限制日志行数，保持性能
            var lines = LogText.Split('\n');
            if (lines.Length > 50)
            {
                var newLines = new string[45];
                Array.Copy(lines, lines.Length - 45, newLines, 0, 45);
                LogText = string.Join('\n', newLines);
            }

            LogText += text;
            OnPropertyChanged(nameof(LogText));
            
            // 触发日志更新事件，通知UI滚动到底部
            LogUpdated?.Invoke(this, EventArgs.Empty);
        });
    }

    private IWebDriver GetDriver(DriverType driverType)
    {
        IWebDriver driver = null!;

        switch (driverType)
        {
            case DriverType.Chrome:
                var chromeOptions = new ChromeOptions();
                driver = new ChromeDriver(chromeOptions);
                break;
            case DriverType.Edge:
                var edgeOptions = new EdgeOptions();
                edgeOptions.AddArgument("--edge-skip-compat-layer-relaunch");
                driver = new EdgeDriver(edgeOptions);
                break;
            default:
                break;
        }

        return driver;
    }
}