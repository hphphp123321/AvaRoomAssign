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
    private ObservableCollection<HouseCondition> _communityConditions = new();

    public List<string> OperationModes { get; } = new() { "模拟点击", "Http发包" };
    public List<string> BrowserTypes { get; } = new() { "Chrome", "Edge" };
    public List<string> HouseTypes { get; } = new() { "一居室", "二居室", "三居室" };

    private CancellationTokenSource? _cancellationTokenSource;
    private ISelector? _currentSelector;
    private bool _isLoadingConfig = false; // 防止加载配置时触发保存

    public MainWindowViewModel()
    {
        try
        {
            // 延迟初始化，先加载配置再设置控制台重定向
            Task.Run(async () =>
            {
                await LoadConfigurationAsync();
                
                await Task.Delay(1000);
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    try
                    {
                        var consoleWriter = new ConsoleTextWriter(AppendLog);
                        Console.SetOut(consoleWriter);
                        Console.WriteLine("✅ 控制台重定向已设置");
                        Console.WriteLine($"✅ 配置加载完成，包含 {CommunityConditions.Count} 个社区条件");
                        Console.WriteLine($"📁 配置文件路径: {ConfigManager.GetConfigPath()}");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"设置控制台重定向时出错: {ex.Message}");
                    }
                });
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ ViewModel初始化失败: {ex.Message}");
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
            await Dispatcher.UIThread.InvokeAsync(() =>
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
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ 加载配置失败: {ex.Message}");
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
                CommunityConditions = CommunityConditions.Select(HouseConditionData.FromHouseCondition).ToList()
            };
            
            // 使用异步保存，避免阻塞UI
            await Task.Run(() => ConfigManager.SaveConfig(config));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ 保存配置失败: {ex.Message}");
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
            Console.WriteLine($"已添加新的社区条件: {newCondition.CommunityName}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"添加社区条件时出错: {ex.Message}");
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
                Console.WriteLine($"已删除社区条件: {condition.CommunityName}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"删除社区条件时出错: {ex.Message}");
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
                        Console.WriteLine("申请人名称不能为空");
                        return;
                    }

                    if (communityList.Count == 0)
                    {
                        Console.WriteLine("社区条件不能为空");
                        return;
                    }

                    switch (operationMode)
                    {
                        case OperationMode.Click:
                            Console.WriteLine("正在启动浏览器...");
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
                            _currentSelector = new HttpSelector(
                                applierName: ApplierName,
                                communityList: communityList,
                                startTime: startTime,
                                cookie: Cookie,
                                requestIntervalMs: clickInterval,
                                cancellationToken: _cancellationTokenSource.Token);
                            break;

                        default:
                            throw new ArgumentOutOfRangeException();
                    }

                    await _currentSelector.RunAsync();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("自动化过程中出现错误: " + ex.Message);
                }
                finally
                {
                    Dispatcher.UIThread.Post(() => IsRunning = false);
                }
            }, _cancellationTokenSource.Token);
        }
        catch (Exception ex)
        {
            Console.WriteLine("启动过程中出现错误: " + ex.Message);
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
            Console.WriteLine("已停止抢房。");
        }
        catch (Exception ex)
        {
            Console.WriteLine("停止抢房时出现错误: " + ex.Message);
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
            Console.WriteLine($"打开GitHub链接失败: {ex.Message}");
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
            
            Console.WriteLine("✅ 配置已重置为默认值");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 重置配置失败: {ex.Message}");
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
            Console.WriteLine("✅ 配置已手动保存");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 手动保存配置失败: {ex.Message}");
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