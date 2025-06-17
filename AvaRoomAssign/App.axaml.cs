using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System;
using System.Collections.Generic;
using Avalonia.Markup.Xaml;
using AvaRoomAssign.ViewModels;
using AvaRoomAssign.Views;

namespace AvaRoomAssign;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
            // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            DisableAvaloniaDataAnnotationValidation();
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// AOT友好的禁用Avalonia数据注解验证
    /// </summary>
    private void DisableAvaloniaDataAnnotationValidation()
    {
        try
        {
            // AOT友好的方式：直接清空所有验证器，避免使用LINQ和反射
            var validators = BindingPlugins.DataValidators;
            if (validators.Count > 0)
            {
                // 创建一个副本以避免修改集合时的问题
                var pluginsToRemove = new List<IDataValidationPlugin>();
                foreach (var plugin in validators)
                {
                    // 检查类型名而不是使用泛型类型检查
                    if (plugin.GetType().Name == "DataAnnotationsValidationPlugin")
                    {
                        pluginsToRemove.Add(plugin);
                    }
                }
                
                // 移除找到的插件
                foreach (var plugin in pluginsToRemove)
                {
                    validators.Remove(plugin);
                }
            }
        }
        catch (Exception ex)
        {
            // 如果禁用验证失败，记录但不影响程序启动
            System.Diagnostics.Debug.WriteLine($"禁用数据验证插件失败: {ex.Message}");
        }
    }
}