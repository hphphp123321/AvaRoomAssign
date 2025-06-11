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

    public MainWindowViewModel()
    {
        try
        {
            // 添加默认测试数据
            var defaultCondition = new HouseCondition("正荣景苑", 0, "3-4,6", 0, 0, HouseType.OneRoom);
            CommunityConditions.Add(defaultCondition);
            
            // 延迟设置控制台重定向
            Task.Run(async () =>
            {
                await Task.Delay(2000);
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    try
                    {
                        var consoleWriter = new ConsoleTextWriter(AppendLog);
                        Console.SetOut(consoleWriter);
                        Console.WriteLine("✅ 控制台重定向已设置");
                        Console.WriteLine($"✅ 当前社区条件数量: {CommunityConditions.Count}");
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
                FileName = "https://github.com/hphphp123321/RoomAssign/tree/simple",
                UseShellExecute = true
            };
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"打开GitHub链接失败: {ex.Message}");
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
        return driverType switch
        {
            DriverType.Chrome => new ChromeDriver(new ChromeOptions()),
            DriverType.Edge => new EdgeDriver(new EdgeOptions()),
            _ => throw new ArgumentOutOfRangeException(nameof(driverType), driverType, null)
        };
    }
}