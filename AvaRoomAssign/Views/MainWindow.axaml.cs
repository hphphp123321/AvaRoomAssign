using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Animation;
using AvaRoomAssign.ViewModels;
using System;

namespace AvaRoomAssign.Views;

public partial class MainWindow : Window
{
    private bool _isDarkTheme = false; // 默认为浅色主题
    private bool _isLogExpanded = false; // 日志区域是否展开
    private const double LOG_NORMAL_HEIGHT = 150; // 正常状态下日志区域高度
    private const double HEADER_HEIGHT = 120; // 标题栏高度
    private const double FOOTER_HEIGHT = 100; // 按钮区域高度
    
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
        
        // 初始化主题
        InitializeTheme();
        
        // 初始化折叠状态
        InitializeCollapsiblePanels();
        
        // 初始化日志区域
        InitializeLogArea();
        
        // 监听窗口大小变化
        this.SizeChanged += OnWindowSizeChanged;
        
        // 订阅ViewModel的运行状态变化
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.PropertyChanged += ViewModel_PropertyChanged;
        }
    }

    /// <summary>
    /// 窗口大小变化事件
    /// </summary>
    private void OnWindowSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (!_isLogExpanded)
        {
            UpdateLogAreaHeight();
        }
    }

    /// <summary>
    /// ViewModel属性变化事件处理
    /// </summary>
    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.IsRunning))
        {
            if (DataContext is MainWindowViewModel viewModel)
            {
                if (viewModel.IsRunning && !_isLogExpanded)
                {
                    ExpandLogAreaToTop();
                }
                else if (!viewModel.IsRunning && _isLogExpanded)
                {
                    CollapseLogAreaToBottom();
                }
            }
        }
    }

    /// <summary>
    /// 初始化主题设置
    /// </summary>
    private void InitializeTheme()
    {
        try
        {
            // 检测系统主题
            var themeVariant = ActualThemeVariant;
            _isDarkTheme = themeVariant == ThemeVariant.Dark;
        }
        catch
        {
            // 如果检测失败，默认使用浅色主题
            _isDarkTheme = false;
        }
        
        ApplyTheme();
        UpdateThemeToggleIcon();
    }
    
    /// <summary>
    /// 应用主题
    /// </summary>
    private void ApplyTheme()
    {
        if (_isDarkTheme)
        {
            // 应用深色主题
            this.Resources["ThemeBackground"] = new SolidColorBrush(Color.Parse("#0D1117"));
            this.Resources["ThemeCardBackground"] = new SolidColorBrush(Color.Parse("#161B22"));
            this.Resources["ThemeTextPrimary"] = new SolidColorBrush(Color.Parse("#F0F6FC"));
            this.Resources["ThemeTextSecondary"] = new SolidColorBrush(Color.Parse("#8B949E"));
            this.Resources["ThemeBorder"] = new SolidColorBrush(Color.Parse("#30363D"));
            this.Resources["ThemeAccent"] = new SolidColorBrush(Color.Parse("#58A6FF"));
            this.Resources["ThemeAccentHover"] = new SolidColorBrush(Color.Parse("#79C0FF"));
            this.Resources["ThemeSuccess"] = new SolidColorBrush(Color.Parse("#3FB950"));
            this.Resources["ThemeWarning"] = new SolidColorBrush(Color.Parse("#D29922"));
            this.Resources["ThemeDanger"] = new SolidColorBrush(Color.Parse("#F85149"));
            this.Resources["ThemeLogBackground"] = new SolidColorBrush(Color.Parse("#0D1117"));
            this.Resources["ThemeLogText"] = new SolidColorBrush(Color.Parse("#7EE787"));
            // 深色主题表格相关颜色
            this.Resources["ThemeTableAlternating"] = new SolidColorBrush(Color.Parse("#21262D"));
            this.Resources["ThemeTableHover"] = new SolidColorBrush(Color.Parse("#30363D"));
            this.Resources["ThemeTableSelected"] = new SolidColorBrush(Color.Parse("#1F6FEB"));
        }
        else
        {
            // 应用浅色主题
            this.Resources["ThemeBackground"] = new SolidColorBrush(Color.Parse("#F8F9FA"));
            this.Resources["ThemeCardBackground"] = new SolidColorBrush(Color.Parse("#FFFFFF"));
            this.Resources["ThemeTextPrimary"] = new SolidColorBrush(Color.Parse("#212529"));
            this.Resources["ThemeTextSecondary"] = new SolidColorBrush(Color.Parse("#6C757D"));
            this.Resources["ThemeBorder"] = new SolidColorBrush(Color.Parse("#DEE2E6"));
            this.Resources["ThemeAccent"] = new SolidColorBrush(Color.Parse("#0D6EFD"));
            this.Resources["ThemeAccentHover"] = new SolidColorBrush(Color.Parse("#0B5ED7"));
            this.Resources["ThemeSuccess"] = new SolidColorBrush(Color.Parse("#198754"));
            this.Resources["ThemeWarning"] = new SolidColorBrush(Color.Parse("#FFC107"));
            this.Resources["ThemeDanger"] = new SolidColorBrush(Color.Parse("#DC3545"));
            this.Resources["ThemeLogBackground"] = new SolidColorBrush(Color.Parse("#F8F9FA"));
            this.Resources["ThemeLogText"] = new SolidColorBrush(Color.Parse("#198754"));
            // 浅色主题表格相关颜色
            this.Resources["ThemeTableAlternating"] = new SolidColorBrush(Color.Parse("#F8F9FA"));
            this.Resources["ThemeTableHover"] = new SolidColorBrush(Color.Parse("#E3F2FD"));
            this.Resources["ThemeTableSelected"] = new SolidColorBrush(Color.Parse("#BBDEFB"));
        }
        
        // 更新渐变背景
        UpdateGradientBackgrounds();
    }
    
    /// <summary>
    /// 更新渐变背景
    /// </summary>
    private void UpdateGradientBackgrounds()
    {
        // 更新顶部标题栏背景
        var headerBorder = this.FindControl<Border>("HeaderBorder");
        if (headerBorder != null)
        {
            var headerGradient = new LinearGradientBrush();
            
            if (_isDarkTheme)
            {
                headerGradient.GradientStops.Add(new GradientStop(Color.Parse("#161B22"), 0));
                headerGradient.GradientStops.Add(new GradientStop(Color.Parse("#0D1117"), 1));
            }
            else
            {
                headerGradient.GradientStops.Add(new GradientStop(Color.Parse("#FFFFFF"), 0));
                headerGradient.GradientStops.Add(new GradientStop(Color.Parse("#F8F9FA"), 1));
            }
            
            headerBorder.Background = headerGradient;
        }
        
        // 更新底部按钮区域背景
        var footerBorder = this.FindControl<Border>("FooterBorder");
        if (footerBorder != null)
        {
            var footerGradient = new LinearGradientBrush();
            
            if (_isDarkTheme)
            {
                footerGradient.GradientStops.Add(new GradientStop(Color.Parse("#161B22"), 0));
                footerGradient.GradientStops.Add(new GradientStop(Color.Parse("#0D1117"), 1));
            }
            else
            {
                footerGradient.GradientStops.Add(new GradientStop(Color.Parse("#FFFFFF"), 0));
                footerGradient.GradientStops.Add(new GradientStop(Color.Parse("#F8F9FA"), 1));
            }
            
            footerBorder.Background = footerGradient;
        }
    }
    
    /// <summary>
    /// 更新主题切换按钮图标
    /// </summary>
    private void UpdateThemeToggleIcon()
    {
        var themeToggle = this.FindControl<Button>("ThemeToggleButton");
        if (themeToggle != null)
        {
            // 使用文字而不是emoji来避免显示问题
            themeToggle.Content = _isDarkTheme ? "☀" : "🌙";
            ToolTip.SetTip(themeToggle, _isDarkTheme ? "切换到浅色主题" : "切换到深色主题");
        }
    }
    
    /// <summary>
    /// 初始化折叠面板状态
    /// </summary>
    private void InitializeCollapsiblePanels()
    {
        // 默认展开运行模式配置面板，其他面板折叠
        SetPanelCollapsed("OperationMode", false);
        SetPanelCollapsed("Account", true);
        SetPanelCollapsed("Execution", true);
        SetPanelCollapsed("Community", true);
    }
    
    /// <summary>
    /// 初始化日志区域
    /// </summary>
    private void InitializeLogArea()
    {
        var logArea = this.FindControl<Border>("LogArea");
        var logTextBox = this.FindControl<TextBox>("LogTextBox");
        var statusText = this.FindControl<TextBlock>("LogStatusText");
        
        if (logArea != null)
        {
            logArea.Height = LOG_NORMAL_HEIGHT;
        }
        if (logTextBox != null)
        {
            logTextBox.Height = LOG_NORMAL_HEIGHT - 80; // 减去标题和边距
        }
        if (statusText != null)
        {
            statusText.Text = "💤 准备就绪";
        }
        
        _isLogExpanded = false;
    }
    
    /// <summary>
    /// 更新日志区域高度（仅在正常状态下）
    /// </summary>
    private void UpdateLogAreaHeight()
    {
        if (_isLogExpanded) return; // 展开状态下不更新
        
        var logArea = this.FindControl<Border>("LogArea");
        var logTextBox = this.FindControl<TextBox>("LogTextBox");
        
        if (logArea != null && logTextBox != null)
        {
            // 计算可用高度
            double windowHeight = this.Height;
            double availableHeight = windowHeight - HEADER_HEIGHT - FOOTER_HEIGHT - 100; // 100为边距和配置项预留
            
            double logHeight = Math.Max(LOG_NORMAL_HEIGHT, Math.Min(300, availableHeight * 0.3)); // 最多占30%
            
            logArea.Height = logHeight;
            logTextBox.Height = logHeight - 80;
        }
    }
    
    /// <summary>
    /// 展开日志区域到顶部覆盖配置项
    /// </summary>
    private void ExpandLogAreaToTop()
    {
        var mainGrid = this.FindControl<Grid>("MainGrid");
        var logArea = this.FindControl<Border>("LogArea");
        var logTextBox = this.FindControl<TextBox>("LogTextBox");
        var statusText = this.FindControl<TextBlock>("LogStatusText");
        
        if (mainGrid != null && logArea != null && logTextBox != null)
        {
            _isLogExpanded = true;
            
            // 将日志区域移动到第1行（配置区域的位置），并占据该行的全部空间
            Grid.SetRow(logArea, 1);
            Grid.SetRowSpan(logArea, 1);
            
            // 隐藏配置区域
            var configScrollViewer = this.FindControl<ScrollViewer>("ConfigScrollViewer");
            if (configScrollViewer != null)
            {
                configScrollViewer.IsVisible = false;
            }
            
            // 设置日志区域为自动高度，占据大部分空间
            logArea.Height = double.NaN; // 自动高度
            logTextBox.Height = double.NaN; // 自动高度
            
            // 更新状态文本
            if (statusText != null)
            {
                statusText.Text = "🔥 运行中...";
            }
        }
    }
    
    /// <summary>
    /// 收缩日志区域回到底部
    /// </summary>
    private void CollapseLogAreaToBottom()
    {
        var logArea = this.FindControl<Border>("LogArea");
        var logTextBox = this.FindControl<TextBox>("LogTextBox");
        var statusText = this.FindControl<TextBlock>("LogStatusText");
        
        if (logArea != null && logTextBox != null)
        {
            _isLogExpanded = false;
            
            // 将日志区域移动回第2行
            Grid.SetRow(logArea, 2);
            Grid.SetRowSpan(logArea, 1);
            
            // 显示配置区域
            var configScrollViewer = this.FindControl<ScrollViewer>("ConfigScrollViewer");
            if (configScrollViewer != null)
            {
                configScrollViewer.IsVisible = true;
            }
            
            // 恢复日志区域的固定高度
            UpdateLogAreaHeight();
            
            // 更新状态文本
            if (statusText != null)
            {
                statusText.Text = "💤 准备就绪";
            }
        }
    }
    
    /// <summary>
    /// 设置面板折叠状态
    /// </summary>
    /// <param name="panelName">面板名称</param>
    /// <param name="isCollapsed">是否折叠</param>
    private void SetPanelCollapsed(string panelName, bool isCollapsed)
    {
        var content = this.FindControl<StackPanel>($"{panelName}Content");
        var icon = this.FindControl<TextBlock>($"{panelName}Icon");
        
        if (content != null)
        {
            content.IsVisible = !isCollapsed;
        }
        
        if (icon != null)
        {
            icon.Text = isCollapsed ? "▶" : "▼";
        }
        
        // 更新日志区域高度（如果未展开）
        if (!_isLogExpanded)
        {
            // 延迟更新以等待布局完成
            Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
            {
                await System.Threading.Tasks.Task.Delay(50);
                UpdateLogAreaHeight();
            });
        }
    }
    
    /// <summary>
    /// 主题切换按钮点击事件
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void OnThemeToggleClick(object? sender, RoutedEventArgs e)
    {
        _isDarkTheme = !_isDarkTheme;
        ApplyTheme();
        UpdateThemeToggleIcon();
    }
    
    /// <summary>
    /// 运行模式配置面板切换
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void OnOperationModeToggle(object? sender, RoutedEventArgs e)
    {
        TogglePanel("OperationMode");
    }
    
    /// <summary>
    /// 账户信息配置面板切换
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void OnAccountToggle(object? sender, RoutedEventArgs e)
    {
        TogglePanel("Account");
    }
    
    /// <summary>
    /// 执行参数配置面板切换
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void OnExecutionToggle(object? sender, RoutedEventArgs e)
    {
        TogglePanel("Execution");
    }
    
    /// <summary>
    /// 社区条件设置面板切换
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void OnCommunityToggle(object? sender, RoutedEventArgs e)
    {
        TogglePanel("Community");
    }
    
    /// <summary>
    /// 切换面板折叠状态
    /// </summary>
    /// <param name="panelName">面板名称</param>
    private void TogglePanel(string panelName)
    {
        var content = this.FindControl<StackPanel>($"{panelName}Content");
        var icon = this.FindControl<TextBlock>($"{panelName}Icon");
        
        if (content != null && icon != null)
        {
            bool isCurrentlyVisible = content.IsVisible;
            content.IsVisible = !isCurrentlyVisible;
            icon.Text = isCurrentlyVisible ? "▶" : "▼";
            
            // 更新日志区域高度（如果未展开）
            if (!_isLogExpanded)
            {
                // 延迟更新以等待布局完成
                Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    await System.Threading.Tasks.Task.Delay(50);
                    UpdateLogAreaHeight();
                });
            }
        }
    }
}