using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Animation;
using Avalonia.Input;
using AvaRoomAssign.ViewModels;
using AvaRoomAssign.Models;
using System;
using System.ComponentModel;

namespace AvaRoomAssign.Views;

public partial class MainWindow : Window
{
    private bool _isDarkTheme = false; // 默认为浅色主题
    private bool _isLogExpanded = false; // 日志区域是否展开
    private const double LOG_NORMAL_HEIGHT = 260; // 正常状态下日志区域高度
    private const double LOG_MIN_HEIGHT = 120; // 日志区域最小高度
    private const double LOG_MAX_HEIGHT = 600; // 日志区域最大高度
    private const double HEADER_HEIGHT = 100; // 标题栏高度
    private const double FOOTER_HEIGHT = 80; // 按钮区域高度
    private const double RESPONSIVE_BREAKPOINT = 1200; // 响应式布局切换阈值
    
    // 拖拽相关变量
    private bool _isResizing = false; // 是否正在调整大小
    private double _initialHeight = 0; // 开始拖拽时的高度
    private Avalonia.Point _initialMousePosition; // 开始拖拽时的鼠标位置
    
    private AppConfig? _cachedConfig = null; // 缓存的配置对象
    
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
        
        // 窗口加载完成后初始化
        this.Loaded += OnWindowLoaded;
        
        // 订阅ViewModel的运行状态变化
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.PropertyChanged += ViewModel_PropertyChanged;
        }
    }

    /// <summary>
    /// 窗口加载完成事件
    /// </summary>
    private void OnWindowLoaded(object? sender, RoutedEventArgs e)
    {
        // 设置拖拽功能
        SetupResizeHandle();
        
        // 初始化响应式布局
        UpdateResponsiveLayout();
    }
    
    /// <summary>
    /// 设置拖拽手柄功能
    /// </summary>
    private void SetupResizeHandle()
    {
        var resizeHandle = this.FindControl<Border>("ResizeHandle");
        if (resizeHandle != null)
        {
            resizeHandle.PointerPressed += OnResizeHandlePressed;
            resizeHandle.PointerMoved += OnResizeHandleMoved;
            resizeHandle.PointerReleased += OnResizeHandleReleased;
        }
    }
    
    /// <summary>
    /// 拖拽手柄按下事件
    /// </summary>
    private void OnResizeHandlePressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            var logArea = this.FindControl<Border>("LogArea");
            if (logArea != null)
            {
                _isResizing = true;
                _initialHeight = logArea.Height;
                _initialMousePosition = e.GetPosition(this);
                
                // 开始拖拽操作
            }
        }
    }
    
    /// <summary>
    /// 拖拽手柄移动事件
    /// </summary>
    private void OnResizeHandleMoved(object? sender, PointerEventArgs e)
    {
        if (_isResizing)
        {
            var currentPosition = e.GetPosition(this);
            var deltaY = currentPosition.Y - _initialMousePosition.Y;
            // 反转拖动方向：向上拖动减少高度，向下拖动增加高度
            var newHeight = _initialHeight - deltaY;
            
            // 限制在最小和最大高度之间
            newHeight = Math.Max(LOG_MIN_HEIGHT, Math.Min(LOG_MAX_HEIGHT, newHeight));
            
            var logArea = this.FindControl<Border>("LogArea");
            if (logArea != null)
            {
                logArea.Height = newHeight;
            }
        }
    }
    
    /// <summary>
    /// 拖拽手柄释放事件
    /// </summary>
    private void OnResizeHandleReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_isResizing)
        {
            _isResizing = false;
            // 结束拖拽操作
        }
    }
    
    /// <summary>
    /// 窗口大小变化事件
    /// </summary>
    private void OnWindowSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        // 更新响应式布局
        UpdateResponsiveLayout();
        
        // 手动拖拽模式下，窗口大小变化时不自动调整日志区域高度
        // 用户可以通过拖拽手柄自行调整
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
    private async void InitializeTheme()
    {
        try
        {
            // 先尝试从配置文件加载主题设置
            if (ConfigManager.ConfigExists())
            {
                // 使用缓存的配置，避免重复加载
                _cachedConfig ??= await ConfigManager.LoadConfigAsync();
                _isDarkTheme = _cachedConfig.IsDarkTheme;
            }
            else
            {
                // 如果配置文件不存在，检测系统主题
                var themeVariant = ActualThemeVariant;
                _isDarkTheme = themeVariant == ThemeVariant.Dark;
            }
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
            this.Resources["ThemeBackground"] = new SolidColorBrush(Color.Parse("#0F1419"));
            this.Resources["ThemeCardBackground"] = new SolidColorBrush(Color.Parse("#1A202C"));
            this.Resources["ThemeTextPrimary"] = new SolidColorBrush(Color.Parse("#F7FAFC"));
            this.Resources["ThemeTextSecondary"] = new SolidColorBrush(Color.Parse("#A0AEC0"));
            this.Resources["ThemeBorder"] = new SolidColorBrush(Color.Parse("#2D3748"));
            this.Resources["ThemeAccent"] = new SolidColorBrush(Color.Parse("#63B3ED"));
            this.Resources["ThemeAccentHover"] = new SolidColorBrush(Color.Parse("#90CDF4"));
            this.Resources["ThemeSuccess"] = new SolidColorBrush(Color.Parse("#68D391"));
            this.Resources["ThemeWarning"] = new SolidColorBrush(Color.Parse("#F6AD55"));
            this.Resources["ThemeDanger"] = new SolidColorBrush(Color.Parse("#FC8181"));
            this.Resources["ThemeLogBackground"] = new SolidColorBrush(Color.Parse("#171923"));
            this.Resources["ThemeLogText"] = new SolidColorBrush(Color.Parse("#9AE6B4"));
            // 深色主题表格相关颜色
            this.Resources["ThemeTableAlternating"] = new SolidColorBrush(Color.Parse("#2D3748"));
            this.Resources["ThemeTableHover"] = new SolidColorBrush(Color.Parse("#4A5568"));
            this.Resources["ThemeTableSelected"] = new SolidColorBrush(Color.Parse("#3182CE"));
        }
        else
        {
            // 应用浅色主题
            this.Resources["ThemeBackground"] = new SolidColorBrush(Color.Parse("#F7F9FC"));
            this.Resources["ThemeCardBackground"] = new SolidColorBrush(Color.Parse("#FFFFFF"));
            this.Resources["ThemeTextPrimary"] = new SolidColorBrush(Color.Parse("#1A202C"));
            this.Resources["ThemeTextSecondary"] = new SolidColorBrush(Color.Parse("#718096"));
            this.Resources["ThemeBorder"] = new SolidColorBrush(Color.Parse("#E2E8F0"));
            this.Resources["ThemeAccent"] = new SolidColorBrush(Color.Parse("#4299E1"));
            this.Resources["ThemeAccentHover"] = new SolidColorBrush(Color.Parse("#3182CE"));
            this.Resources["ThemeSuccess"] = new SolidColorBrush(Color.Parse("#48BB78"));
            this.Resources["ThemeWarning"] = new SolidColorBrush(Color.Parse("#ED8936"));
            this.Resources["ThemeDanger"] = new SolidColorBrush(Color.Parse("#F56565"));
            this.Resources["ThemeLogBackground"] = new SolidColorBrush(Color.Parse("#F7FAFC"));
            this.Resources["ThemeLogText"] = new SolidColorBrush(Color.Parse("#38A169"));
            // 浅色主题表格相关颜色
            this.Resources["ThemeTableAlternating"] = new SolidColorBrush(Color.Parse("#F7FAFC"));
            this.Resources["ThemeTableHover"] = new SolidColorBrush(Color.Parse("#EBF8FF"));
            this.Resources["ThemeTableSelected"] = new SolidColorBrush(Color.Parse("#BEE3F8"));
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
                headerGradient.GradientStops.Add(new GradientStop(Color.Parse("#1A202C"), 0));
                headerGradient.GradientStops.Add(new GradientStop(Color.Parse("#0F1419"), 1));
            }
            else
            {
                headerGradient.GradientStops.Add(new GradientStop(Color.Parse("#FFFFFF"), 0));
                headerGradient.GradientStops.Add(new GradientStop(Color.Parse("#F7F9FC"), 1));
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
                footerGradient.GradientStops.Add(new GradientStop(Color.Parse("#1A202C"), 0));
                footerGradient.GradientStops.Add(new GradientStop(Color.Parse("#0F1419"), 1));
            }
            else
            {
                footerGradient.GradientStops.Add(new GradientStop(Color.Parse("#FFFFFF"), 0));
                footerGradient.GradientStops.Add(new GradientStop(Color.Parse("#F7F9FC"), 1));
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
        var statusText = this.FindControl<TextBlock>("LogStatusText");
        
        if (logArea != null)
        {
            logArea.Height = LOG_NORMAL_HEIGHT;
        }
        if (statusText != null)
        {
            statusText.Text = "💤 准备就绪";
        }
        
        _isLogExpanded = false;
    }
    
    /// <summary>
    /// 获取配置区域的实际高度
    /// </summary>
    /// <returns>配置区域高度</returns>
    private double GetConfigAreaHeight()
    {
        double totalHeight = 0;
        
        // 计算每个面板的高度
        var panelNames = new[] { "OperationMode", "Account", "Execution", "Community" };
        
        foreach (var panelName in panelNames)
        {
            var content = this.FindControl<StackPanel>($"{panelName}Content");
            if (content != null && content.IsVisible)
            {
                // 展开面板的估计高度
                switch (panelName)
                {
                    case "OperationMode":
                        totalHeight += 120; // 运行模式配置面板高度
                        break;
                    case "Account":
                        totalHeight += 200; // 账户信息配置面板高度
                        break;
                    case "Execution":
                        totalHeight += 180; // 执行参数配置面板高度
                        break;
                    case "Community":
                        totalHeight += 350; // 社区条件设置面板高度（包含表格）
                        break;
                }
            }
            else
            {
                totalHeight += 60; // 折叠状态下的头部高度
            }
        }
        
        return totalHeight;
    }
    
    /// <summary>
    /// 展开日志区域到顶部覆盖配置项
    /// </summary>
    private void ExpandLogAreaToTop()
    {
        var mainGrid = this.FindControl<Grid>("MainGrid");
        var logArea = this.FindControl<Border>("LogArea");
        var statusText = this.FindControl<TextBlock>("LogStatusText");
        
        if (mainGrid != null && logArea != null)
        {
            _isLogExpanded = true;
            
            // 直接将日志区域移动到第1行（配置区域的位置），覆盖所有配置项
            Grid.SetRow(logArea, 1);
            Grid.SetRowSpan(logArea, 1);
            
            // 完全隐藏配置区域
            var configScrollViewer = this.FindControl<ScrollViewer>("ConfigScrollViewer");
            if (configScrollViewer != null)
            {
                configScrollViewer.IsVisible = false;
            }
            
            // 计算窗口可用高度，让日志区域占据配置项的全部空间
            double windowHeight = this.Height;
            double headerHeight = HEADER_HEIGHT; // 顶部标题栏高度
            double footerHeight = FOOTER_HEIGHT; // 底部按钮区域高度
            double availableHeight = windowHeight - headerHeight - footerHeight - 48; // 48为边距
            
            double logHeight = Math.Max(400, availableHeight); // 最小400像素
            
            // 只需要设置LogArea的高度，TextBox会自动适应
            logArea.Height = logHeight;
            
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
            
            // 恢复日志区域的默认高度
            if (logArea != null)
            {
                logArea.Height = LOG_NORMAL_HEIGHT;
            }
            
            // 更新状态文本
            if (statusText != null)
            {
                statusText.Text = "💤 准备就绪";
            }
        }
    }
    
    /// <summary>
    /// 切换面板折叠状态
    /// </summary>
    /// <param name="panelName">面板名称</param>
    private void TogglePanel(string panelName)
    {
        // 获取当前显示的布局
        var singleColumnLayout = this.FindControl<StackPanel>("SingleColumnLayout");
        var doubleColumnLayout = this.FindControl<Grid>("DoubleColumnLayout");
        
        bool isDoubleLayoutVisible = doubleColumnLayout?.IsVisible == true;
        
        if (isDoubleLayoutVisible)
        {
            // 双列布局
            var content = this.FindControl<StackPanel>($"{panelName}Content");
            var icon = this.FindControl<TextBlock>($"{panelName}Icon");
            
            if (content != null && icon != null)
            {
                bool isCurrentlyVisible = content.IsVisible;
                content.IsVisible = !isCurrentlyVisible;
                icon.Text = isCurrentlyVisible ? "▶" : "▼";
                
                // 同步到单列布局
                var singleContent = this.FindControl<StackPanel>($"{panelName}ContentSingle");
                var singleIcon = this.FindControl<TextBlock>($"{panelName}IconSingle");
                if (singleContent != null) singleContent.IsVisible = content.IsVisible;
                if (singleIcon != null) singleIcon.Text = icon.Text;
            }
        }
        else
        {
            // 单列布局
            var content = this.FindControl<StackPanel>($"{panelName}ContentSingle");
            var icon = this.FindControl<TextBlock>($"{panelName}IconSingle");
            
            if (content != null && icon != null)
            {
                bool isCurrentlyVisible = content.IsVisible;
                content.IsVisible = !isCurrentlyVisible;
                icon.Text = isCurrentlyVisible ? "▶" : "▼";
                
                // 同步到双列布局
                var doubleContent = this.FindControl<StackPanel>($"{panelName}Content");
                var doubleIcon = this.FindControl<TextBlock>($"{panelName}Icon");
                if (doubleContent != null) doubleContent.IsVisible = content.IsVisible;
                if (doubleIcon != null) doubleIcon.Text = icon.Text;
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
        // 双列布局
        var doubleContent = this.FindControl<StackPanel>($"{panelName}Content");
        var doubleIcon = this.FindControl<TextBlock>($"{panelName}Icon");
        
        if (doubleContent != null)
        {
            doubleContent.IsVisible = !isCollapsed;
        }
        
        if (doubleIcon != null)
        {
            doubleIcon.Text = isCollapsed ? "▶" : "▼";
        }
        
        // 单列布局
        var singleContent = this.FindControl<StackPanel>($"{panelName}ContentSingle");
        var singleIcon = this.FindControl<TextBlock>($"{panelName}IconSingle");
        
        if (singleContent != null)
        {
            singleContent.IsVisible = !isCollapsed;
        }
        
        if (singleIcon != null)
        {
            singleIcon.Text = isCollapsed ? "▶" : "▼";
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
        
        // 保存主题设置
        SaveThemeConfiguration();
    }
    
    /// <summary>
    /// 保存主题配置
    /// </summary>
    private async void SaveThemeConfiguration()
    {
        try
        {
            // 使用缓存的配置，避免重复加载
            _cachedConfig ??= await ConfigManager.LoadConfigAsync();
            _cachedConfig.IsDarkTheme = _isDarkTheme;
            await ConfigManager.SaveConfigAsync(_cachedConfig);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"保存主题配置失败: {ex.Message}");
        }
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
    /// 更新响应式布局
    /// </summary>
    private void UpdateResponsiveLayout()
    {
        var currentWidth = this.Width;
        var singleColumnLayout = this.FindControl<StackPanel>("SingleColumnLayout");
        var doubleColumnLayout = this.FindControl<Grid>("DoubleColumnLayout");
        
        if (currentWidth >= RESPONSIVE_BREAKPOINT)
        {
            // 使用双列布局
            if (singleColumnLayout != null) singleColumnLayout.IsVisible = false;
            if (doubleColumnLayout != null) doubleColumnLayout.IsVisible = true;
            
            // 同步折叠状态从单列到双列
            SyncCollapsibleStates(fromSingle: true);
        }
        else
        {
            // 使用单列布局
            if (singleColumnLayout != null) singleColumnLayout.IsVisible = true;
            if (doubleColumnLayout != null) doubleColumnLayout.IsVisible = false;
            
            // 同步折叠状态从双列到单列
            SyncCollapsibleStates(fromSingle: false);
        }
    }
    
    /// <summary>
    /// 同步单列和双列布局的折叠状态
    /// </summary>
    /// <param name="fromSingle">是否从单列同步到双列</param>
    private void SyncCollapsibleStates(bool fromSingle)
    {
        var panels = new[] { "OperationMode", "Account", "Execution", "Community" };
        
        foreach (var panelName in panels)
        {
            if (fromSingle)
            {
                // 从单列同步到双列
                var singleContent = this.FindControl<StackPanel>($"{panelName}ContentSingle");
                var singleIcon = this.FindControl<TextBlock>($"{panelName}IconSingle");
                var doubleContent = this.FindControl<StackPanel>($"{panelName}Content");
                var doubleIcon = this.FindControl<TextBlock>($"{panelName}Icon");
                
                if (singleContent != null && doubleContent != null)
                {
                    doubleContent.IsVisible = singleContent.IsVisible;
                }
                if (singleIcon != null && doubleIcon != null)
                {
                    doubleIcon.Text = singleIcon.Text;
                }
            }
            else
            {
                // 从双列同步到单列
                var doubleContent = this.FindControl<StackPanel>($"{panelName}Content");
                var doubleIcon = this.FindControl<TextBlock>($"{panelName}Icon");
                var singleContent = this.FindControl<StackPanel>($"{panelName}ContentSingle");
                var singleIcon = this.FindControl<TextBlock>($"{panelName}IconSingle");
                
                if (doubleContent != null && singleContent != null)
                {
                    singleContent.IsVisible = doubleContent.IsVisible;
                }
                if (doubleIcon != null && singleIcon != null)
                {
                    singleIcon.Text = doubleIcon.Text;
                }
            }
        }
    }
}