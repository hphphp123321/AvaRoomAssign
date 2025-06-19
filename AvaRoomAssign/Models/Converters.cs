using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;

namespace AvaRoomAssign.Models;

public class BoolToInverseBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return !boolValue;
        }
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return !boolValue;
        }
        return false;
    }
}

public class IntToBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int intValue && parameter is string param)
        {
            if (int.TryParse(param, out int target))
            {
                return intValue == target;
            }
        }
        else if (value is int intValue2)
        {
            return intValue2 == 0;
        }
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class MultiBoolConverter : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values == null) return false;
        
        foreach (var value in values)
        {
            if (value is bool boolValue && !boolValue)
            {
                return false;
            }
        }
        
        return true;
    }
}

public class BoolToTextConverter : IValueConverter
{
    public string TrueText { get; set; } = "True";
    public string FalseText { get; set; } = "False";

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return boolValue ? TrueText : FalseText;
        }
        return FalseText;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
} 

/// <summary>
/// 房型数字转换为中文描述的转换器
/// </summary>
public class HouseTypeConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int houseType)
        {
            return houseType switch
            {
                0 => "一居室",
                1 => "二居室", 
                2 => "三居室",
                _ => $"房型{houseType}"
            };
        }
        return "未知房型";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// 房源数量转换器
/// </summary>
public class RoomCountConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int count)
        {
            return $"{count} 个房源";
        }
        return "0 个房源";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// 条件数量转换器
/// </summary>
public class ConditionCountConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int count)
        {
            return $"({count} 个条件)";
        }
        return "(0 个条件)";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// 层数显示转换器
/// </summary>
public class FloorRangeConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string floorRange && !string.IsNullOrEmpty(floorRange))
        {
            return $"层数:{floorRange}";
        }
        return "层数:不限";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// 更新时间显示转换器
/// </summary>
public class UpdateTimeConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is DateTime dateTime)
        {
            return $"更新时间: {dateTime:MM-dd HH:mm}";
        }
        return "更新时间: 未知";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// 数值大于0转布尔转换器 - 用于显示展开按钮
/// </summary>
public class IntGreaterThanZeroConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int intValue)
        {
            return intValue > 0;
        }
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
} 