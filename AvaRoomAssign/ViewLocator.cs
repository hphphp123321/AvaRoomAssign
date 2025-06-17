using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using AvaRoomAssign.ViewModels;
using AvaRoomAssign.Views;

namespace AvaRoomAssign;

/// <summary>
/// AOT友好的视图定位器，避免使用反射
/// </summary>
public class ViewLocator : IDataTemplate
{
    public Control? Build(object? param)
    {
        if (param is null)
            return null;
        
        // AOT友好：避免使用反射，直接映射已知的ViewModel到View
        return param switch
        {
            MainWindowViewModel => new MainWindow(),
            _ => new TextBlock { Text = $"未找到视图: {param.GetType().Name}" }
        };
    }

    public bool Match(object? data)
    {
        return data is ViewModelBase;
    }
}
