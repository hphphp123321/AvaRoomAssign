using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace AvaRoomAssign.Models;

public enum HouseType
{
    [Description("一居室")] OneRoom,
    [Description("二居室")] TwoRoom,
    [Description("三居室")] ThreeRoom,
}

public class HouseCondition : INotifyPropertyChanged
{
    private string _communityName = string.Empty;
    private int _buildingNo;
    private string _floorRange = string.Empty;
    private int _maxPrice;
    private int _leastArea;
    private HouseType _houseType = HouseType.OneRoom;

    public HouseCondition()
    {
    }

    public HouseCondition(
        string communityName,
        int buildingNo,
        string floorRange,
        int maxPrice,
        int leastArea,
        HouseType houseType = HouseType.OneRoom)
    {
        _communityName = communityName;
        _buildingNo = buildingNo;
        _floorRange = floorRange;
        _maxPrice = maxPrice;
        _leastArea = leastArea;
        _houseType = houseType;
    }

    public string CommunityName
    {
        get => _communityName;
        set
        {
            if (_communityName != value)
            {
                _communityName = value;
                OnPropertyChanged(nameof(CommunityName));
            }
        }
    }

    public int BuildingNo
    {
        get => _buildingNo;
        set
        {
            if (_buildingNo != value)
            {
                _buildingNo = value;
                OnPropertyChanged(nameof(BuildingNo));
            }
        }
    }

    public string FloorRange
    {
        get => _floorRange;
        set
        {
            if (_floorRange != value)
            {
                _floorRange = value;
                OnPropertyChanged(nameof(FloorRange));
            }
        }
    }

    public int MaxPrice
    {
        get => _maxPrice;
        set
        {
            if (_maxPrice != value)
            {
                _maxPrice = value;
                OnPropertyChanged(nameof(MaxPrice));
            }
        }
    }

    public int LeastArea
    {
        get => _leastArea;
        set
        {
            if (_leastArea != value)
            {
                _leastArea = value;
                OnPropertyChanged(nameof(LeastArea));
            }
        }
    }

    public HouseType HouseType
    {
        get => _houseType;
        set
        {
            if (_houseType != value)
            {
                _houseType = value;
                OnPropertyChanged(nameof(HouseType));
                OnPropertyChanged(nameof(HouseTypeDescription));
            }
        }
    }

    public string HouseTypeDescription
    {
        get => GetHouseTypeDescription(_houseType);
        set
        {
            var newType = GetHouseTypeFromDescription(value);
            if (_houseType != newType)
            {
                _houseType = newType;
                OnPropertyChanged(nameof(HouseType));
                OnPropertyChanged(nameof(HouseTypeDescription));
            }
        }
    }

    /// <summary>
    /// AOT友好的枚举描述获取方法
    /// </summary>
    private static string GetHouseTypeDescription(HouseType houseType)
    {
        return houseType switch
        {
            HouseType.OneRoom => "一居室",
            HouseType.TwoRoom => "二居室",
            HouseType.ThreeRoom => "三居室",
            _ => houseType.ToString()
        };
    }

    /// <summary>
    /// AOT友好的枚举值获取方法
    /// </summary>
    private static HouseType GetHouseTypeFromDescription(string description)
    {
        return description switch
        {
            "一居室" => HouseType.OneRoom,
            "二居室" => HouseType.TwoRoom,
            "三居室" => HouseType.ThreeRoom,
            _ => HouseType.OneRoom
        };
    }

    public override string ToString()
    {
        return $"{CommunityName} (幢号:{BuildingNo}, 层号:{FloorRange}, 价格:{MaxPrice}, 面积:{LeastArea})";
    }

    public static List<int> ParseFloorRange(string floorRange)
    {
        var floors = new List<int>();
        if (string.IsNullOrWhiteSpace(floorRange) || floorRange.Trim() == "0")
            return floors; // 空或"0"表示不进行楼层过滤

        // 按逗号分割，支持"3,4,6,9-11"格式
        var parts = floorRange.Split(',');
        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            if (trimmed.Contains("-"))
            {
                var bounds = trimmed.Split('-');
                if (bounds.Length == 2 &&
                    int.TryParse(bounds[0].Trim(), out int low) &&
                    int.TryParse(bounds[1].Trim(), out int high))
                {
                    for (var i = low; i <= high; i++)
                    {
                        floors.Add(i);
                    }
                }
            }
            else if (int.TryParse(trimmed, out int floor))
            {
                floors.Add(floor);
            }
        }

        return floors;
    }

    #region 过滤方法

    public static bool FilterEqual(int actual, int filter) => filter == 0 || actual == filter;
    public static bool FilterPrice(double price, int maxPrice) => maxPrice == 0 || price <= maxPrice;
    public static bool FilterArea(double area, int minArea) => minArea == 0 || area >= minArea;

    public static bool FilterFloor(int floor, string range)
    {
        if (string.IsNullOrWhiteSpace(range)) return true;
        var list = HouseCondition.ParseFloorRange(range);
        return list.Count == 0 || list.Contains(floor);
    }

    #endregion

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

// 枚举类型，用于选择运行模式
public enum OperationMode
{
    Click, // 模拟点击方式
    Http // 发包方式
}

public enum DriverType
{
    Chrome,
    Edge
}

/// <summary>
/// AOT友好的枚举帮助类，不使用反射
/// </summary>
public static class EnumHelper
{
    /// <summary>
    /// AOT友好的枚举描述获取方法
    /// </summary>
    public static string GetDescription<T>(this T enumValue) where T : Enum
    {
        if (enumValue is HouseType houseType)
        {
            return houseType switch
            {
                HouseType.OneRoom => "一居室",
                HouseType.TwoRoom => "二居室",
                HouseType.ThreeRoom => "三居室",
                _ => enumValue.ToString()
            };
        }
        
        return enumValue.ToString();
    }

    /// <summary>
    /// AOT友好的枚举值获取方法
    /// </summary>
    public static T GetEnumValueFromDescription<T>(string description) where T : struct, Enum
    {
        if (typeof(T) == typeof(HouseType))
        {
            var houseType = description switch
            {
                "一居室" => HouseType.OneRoom,
                "二居室" => HouseType.TwoRoom,
                "三居室" => HouseType.ThreeRoom,
                _ => HouseType.OneRoom
            };
            return (T)(object)houseType;
        }

        // 对于其他枚举类型，使用默认解析
        if (Enum.TryParse<T>(description, true, out var result))
        {
            return result;
        }
        
        throw new ArgumentException($"未能根据描述{description}找到对应的枚举值。");
    }

    /// <summary>
    /// AOT友好的枚举显示项列表获取方法
    /// </summary>
    public static List<EnumDisplayItem<T>> GetEnumDisplayItems<T>() where T : struct, Enum
    {
        var items = new List<EnumDisplayItem<T>>();
        
        if (typeof(T) == typeof(HouseType))
        {
            var houseTypeItems = new List<EnumDisplayItem<HouseType>>
            {
                new() { Value = HouseType.OneRoom, Description = "一居室" },
                new() { Value = HouseType.TwoRoom, Description = "二居室" },
                new() { Value = HouseType.ThreeRoom, Description = "三居室" }
            };
            
            return houseTypeItems.ConvertAll(item => new EnumDisplayItem<T>
            {
                Value = (T)(object)item.Value,
                Description = item.Description
            });
        }
        
        // 对于其他枚举类型，使用基本方法
        var values = Enum.GetValues<T>();
        foreach (var value in values)
        {
            items.Add(new EnumDisplayItem<T>
            {
                Value = value,
                Description = value.GetDescription()
            });
        }
        
        return items;
    }
}

public class EnumDisplayItem<T> where T : struct, Enum
{
    public T Value { get; set; }
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// 扩展方法帮助类
/// </summary>
public static class Extensions
{
    /// <summary>
    /// 为对象提供链式调用支持
    /// </summary>
    public static T Apply<T>(this T obj, Action<T> action)
    {
        action(obj);
        return obj;
    }
} 