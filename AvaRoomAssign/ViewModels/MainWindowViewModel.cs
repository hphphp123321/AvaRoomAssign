using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
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
    [ObservableProperty] private string _logText = string.Empty;

    [ObservableProperty] private int _selectedOperationMode = 1; // 默认选择Http发包

    [ObservableProperty] private int _selectedBrowserType = 1; // 默认选择Edge

    [ObservableProperty] private string _userAccount = string.Empty;

    [ObservableProperty] private string _userPassword = string.Empty;

    [ObservableProperty]
    private string _cookie = string.Empty;

    [ObservableProperty] private string _applierName = string.Empty;

    [ObservableProperty] private DateTime _startDate = DateTime.Today.AddDays(1);

    [ObservableProperty] private string _startHour = "09";

    [ObservableProperty] private string _startMinute = "00";

    [ObservableProperty] private string _startSecond = "00";

    [ObservableProperty] private string _clickInterval = "200";

    [ObservableProperty] private bool _autoConfirm = false;

    [ObservableProperty] private bool _isRunning = false;

    [ObservableProperty] private bool _isFetchingRoomIds = false;

    [ObservableProperty] private bool _isFetchingCookie = false;

    [ObservableProperty] private ObservableCollection<HouseCondition> _communityConditions = new();

    // 添加手动房间ID列表属性
    [ObservableProperty] private string _manualRoomIds = string.Empty;

    // 添加用于切换社区条件设置和手动房间ID的属性
    [ObservableProperty] private bool _useManualRoomIds = false;

    // 添加房源映射缓存数据绑定属性
    [ObservableProperty] private ObservableCollection<ConditionRoomIdMapping> _roomIdMappings = new();

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
            // 先加载现有配置以保留房间ID映射
            var existingConfig = await ConfigManager.LoadConfigAsync();
            
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
                UseManualRoomIds = UseManualRoomIds,
                // 保留现有的房间ID映射，避免被覆盖
                RoomIdMappings = existingConfig.RoomIdMappings
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
            var excludeProperties = new[] { nameof(LogText), nameof(IsRunning), nameof(IsFetchingRoomIds), nameof(IsFetchingCookie), nameof(RoomIdMappings) };
            if (excludeProperties.Contains(e.PropertyName)) return;

            await SaveConfigurationAsync();
        };

        // 监听集合变化（添加/删除项目）
        CommunityConditions.CollectionChanged += async (sender, e) =>
        {
            if (_isLoadingConfig) return;
            
            // 处理新增项目，为其添加属性变化监听
            if (e.NewItems != null)
            {
                foreach (HouseCondition item in e.NewItems)
                {
                    item.PropertyChanged += OnCommunityConditionPropertyChanged;
                }
            }
            
            // 处理移除项目，移除其属性变化监听
            if (e.OldItems != null)
            {
                foreach (HouseCondition item in e.OldItems)
                {
                    item.PropertyChanged -= OnCommunityConditionPropertyChanged;
                }
            }
            
            await SaveConfigurationAsync();
        };
        
        // 为现有的社区条件项目添加属性变化监听
        foreach (var condition in CommunityConditions)
        {
            condition.PropertyChanged += OnCommunityConditionPropertyChanged;
        }
    }
    
    /// <summary>
    /// 社区条件属性变化事件处理
    /// </summary>
    private async void OnCommunityConditionPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (_isLoadingConfig) return;
        
        // 当社区条件的任何属性发生变化时，自动保存配置
        await SaveConfigurationAsync();
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
            // 验证配置
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

            var validationResult = ConfigValidator.ValidateConfig(config);
            if (!validationResult.IsValid)
            {
                LogManager.Error("配置验证失败:");
                LogManager.Info(validationResult.GetFormattedMessages());
                return;
            }

            if (validationResult.Warnings.Count > 0)
            {
                LogManager.Warning("配置检查发现以下警告:");
                LogManager.Info(validationResult.GetFormattedMessages());
            }

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
                            if (driver == null)
                            {
                                LogManager.Error("浏览器启动失败，流程终止");
                                return;
                            }

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
    /// 打开配置文件所在文件夹
    /// </summary>
    [RelayCommand]
    private void OpenConfigFolder()
    {
        try
        {
            var configPath = ConfigManager.GetConfigPath();
            var configDir = Path.GetDirectoryName(configPath);
            
            if (string.IsNullOrEmpty(configDir))
            {
                LogManager.Error("无法获取配置文件路径");
                return;
            }

            // 确保目录存在
            if (!Directory.Exists(configDir))
            {
                Directory.CreateDirectory(configDir);
                LogManager.Info($"已创建配置文件目录: {configDir}");
            }

            var psi = new ProcessStartInfo
            {
                FileName = configDir,
                UseShellExecute = true,
                Verb = "open"
            };
            Process.Start(psi);
            LogManager.Success($"已打开配置文件夹: {configDir}");
        }
        catch (Exception ex)
        {
            LogManager.Error("打开配置文件夹失败", ex);
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

    /// <summary>
    /// 自动获取Cookie命令
    /// </summary>
    [RelayCommand]
    private async Task FetchCookieAsync()
    {
        if (IsFetchingCookie || IsRunning) return;

        IsFetchingCookie = true;

        try
        {
            // 检查必要参数
            if (string.IsNullOrWhiteSpace(UserAccount))
            {
                LogManager.Error("用户账号不能为空，请先填写用户账号");
                return;
            }

            if (string.IsNullOrWhiteSpace(UserPassword))
            {
                LogManager.Error("用户密码不能为空，请先填写用户密码");
                return;
            }

            LogManager.Info("开始自动获取Cookie...");

            var cancellationTokenSource = new CancellationTokenSource();
            
            try
            {
                // 启动浏览器用于获取Cookie
                LogManager.Info("正在启动浏览器...");
                var driverType = SelectedBrowserType == 0 ? DriverType.Chrome : DriverType.Edge;
                var driver = GetDriver(driverType);
                if (driver == null)
                {
                    LogManager.Error("无法启动浏览器，Cookie获取失败");
                    return;
                }
                
                try
                {
                    // 调用CookieManager获取Cookie
                    var cookieValue = await CookieManager.GetCookieWithSeleniumAsync(driver, UserAccount, UserPassword, cancellationTokenSource.Token);

                    if (!string.IsNullOrWhiteSpace(cookieValue))
                    {
                        // 更新Cookie字段
                        Cookie = cookieValue;
                        LogManager.Success("Cookie获取成功并已自动填入！现在可以使用HTTP发包模式进行抢房了");
                        
                        // 验证Cookie是否有效
                        LogManager.Info("正在验证Cookie有效性...");
                        var isValid = await CookieManager.ValidateCookieWithSeleniumAsync(driver, cookieValue, cancellationTokenSource.Token);
                        if (isValid)
                        {
                            LogManager.Success("Cookie验证通过，可以正常使用");
                        }
                        else
                        {
                            LogManager.Warning("Cookie验证失败，可能需要重新获取");
                        }
                    }
                    else
                    {
                        LogManager.Error("获取Cookie失败，请检查用户名和密码是否正确");
                    }
                }
                finally
                {
                    // 关闭浏览器
                    try
                    {
                        driver.Quit();
                        LogManager.Info("浏览器已关闭");
                    }
                    catch (Exception ex)
                    {
                        LogManager.Warning($"关闭浏览器时出现警告: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.Error("获取Cookie时出错", ex);
            }
        }
        catch (Exception ex)
        {
            LogManager.Error("启动获取Cookie过程中出现错误", ex);
        }
        finally
        {
            IsFetchingCookie = false;
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
            }
            catch (Exception ex)
            {
                await Dispatcher.UIThread.InvokeAsync(() => { LogManager.Error("获取房间ID时出错", ex); });
            }
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
    private async Task SaveRoomIdMappingsAsync(Dictionary<string, List<string>> roomIdMappings,
        List<HouseCondition> conditions)
    {
        try
        {
            var config = await ConfigManager.LoadConfigAsync();

            // 清空现有映射
            config.RoomIdMappings.Clear();

            // 在UI线程上清空ObservableCollection
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                RoomIdMappings.Clear();
            });

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

                // 在UI线程上添加到ObservableCollection
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    RoomIdMappings.Add(mapping);
                });
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

            // 在UI线程上更新ObservableCollection
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                RoomIdMappings.Clear();
            });

            foreach (var mapping in config.RoomIdMappings)
            {
                if (mapping.RoomIds.Count > 0)
                {
                    _preFetchedRoomIds[mapping.ConditionKey] = mapping.RoomIds;
                }

                // 在UI线程上添加到ObservableCollection，显示所有映射条件（包括没有房源ID的）
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    RoomIdMappings.Add(mapping);
                });
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

    private readonly System.Text.StringBuilder _logBuffer = new(8192);
    private readonly object _logLock = new object();
    private int _logLineCount = 0;
    private const int MaxLogLines = 50;
    private const int KeepLogLines = 45;

    private void AppendLog(string text)
    {
        lock (_logLock)
        {
            // 确保在UI线程上执行
            Dispatcher.UIThread.Post(() =>
            {
                // 使用StringBuilder提高性能
                _logBuffer.Append(text);
                _logLineCount++;

                // 当行数超过限制时，清理旧日志
                if (_logLineCount > MaxLogLines)
                {
                    var allText = _logBuffer.ToString();
                    var lines = allText.Split('\n');
                    
                    if (lines.Length > KeepLogLines)
                    {
                        _logBuffer.Clear();
                        var linesToKeep = lines.Skip(lines.Length - KeepLogLines);
                        _logBuffer.AppendJoin('\n', linesToKeep);
                        _logLineCount = KeepLogLines;
                    }
                }

                LogText = _logBuffer.ToString();
                OnPropertyChanged(nameof(LogText));

                // 触发日志更新事件，通知UI滚动到底部
                LogUpdated?.Invoke(this, EventArgs.Empty);
            });
        }
    }

    private IWebDriver? GetDriver(DriverType driverType)
    {
        try
        {
            return driverType switch
            {
                DriverType.Chrome => new ChromeDriver(new ChromeOptions()),
                DriverType.Edge => new EdgeDriver(new EdgeOptions().Apply(opt => 
                    opt.AddArgument("--edge-skip-compat-layer-relaunch"))),
                _ => throw new ArgumentOutOfRangeException(nameof(driverType), $"不支持的浏览器类型: {driverType}")
            };
        }
        catch (Exception ex)
        {
            LogManager.Error($"创建WebDriver失败: {ex.Message}", ex);
            return null;
        }
    }
}