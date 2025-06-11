using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;

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
        get => _houseType.GetDescription();
        set
        {
            var newType = EnumHelper.GetEnumValueFromDescription<HouseType>(value);
            if (_houseType != newType)
            {
                _houseType = newType;
                OnPropertyChanged(nameof(HouseType));
                OnPropertyChanged(nameof(HouseTypeDescription));
            }
        }
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

public static class EnumHelper
{
    // 该扩展方法要求 T 必须是枚举类型（C# 7.3 及以上版本支持该约束）
    public static string GetDescription<T>(this T enumValue) where T : Enum
    {
        // 获取类型信息
        Type type = typeof(T);
        // 获取枚举值的名称
        string name = enumValue.ToString();
        // 获取对应的字段信息
        FieldInfo? field = type.GetField(name);
        if (field != null)
        {
            // 获取所有 DescriptionAttribute 属性
            var attributes = (DescriptionAttribute[])field.GetCustomAttributes(typeof(DescriptionAttribute), false);
            if (attributes != null && attributes.Length > 0)
            {
                return attributes[0].Description;
            }
        }

        return name;
    }

    public static T GetEnumValueFromDescription<T>(string description) where T : struct, Enum
    {
        Type type = typeof(T);
        foreach (FieldInfo field in type.GetFields())
        {
            if (!field.IsSpecialName)
            {
                var attribute =
                    Attribute.GetCustomAttribute(field, typeof(DescriptionAttribute)) as DescriptionAttribute;
                if (attribute != null)
                {
                    if (attribute.Description == description)
                        return (T)field.GetValue(null)!;
                }
                else
                {
                    if (field.Name.Equals(description, StringComparison.OrdinalIgnoreCase))
                        return (T)field.GetValue(null)!;
                }
            }
        }

        throw new ArgumentException($"未能根据描述{description}找到对应的枚举值。");
    }

    public static List<EnumDisplayItem<T>> GetEnumDisplayItems<T>() where T : struct, Enum
    {
        var items = new List<EnumDisplayItem<T>>();
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