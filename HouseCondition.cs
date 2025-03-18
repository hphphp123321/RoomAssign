using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Windows.Data;
using System.Windows.Markup;

namespace RoomAssign;

public enum HouseType
{
    [Description("一居室")] OneRoom,
    [Description("二居室")] TwoRoom,
    [Description("三居室")] ThreeRoom,
}

public class HouseCondition(
    string communityName,
    int buildingNo,
    int floorNo,
    int maxPrice,
    int leastArea,
    HouseType houseType = HouseType.OneRoom)
{
    public string CommunityName { get; set; } = communityName;
    public int BuildingNo { get; set; } = buildingNo;
    public int FloorNo { get; set; } = floorNo;
    public int MaxPrice { get; set; } = maxPrice;
    public int LeastArea { get; set; } = leastArea;
    public HouseType HouseType { get; set; } = houseType; // 默认一居室


    public override string ToString()
    {
        return $"{CommunityName} (幢号:{BuildingNo}, 层号:{FloorNo}, 价格:{MaxPrice}, 面积:{LeastArea})";
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

public class EnumBindingSourceExtension : MarkupExtension
{
    private Type _enumType;

    public Type EnumType
    {
        get => _enumType;
        set
        {
            if (value != _enumType)
            {
                // 处理 Nullable 情况
                Type enumType = Nullable.GetUnderlyingType(value) ?? value;
                if (!enumType.IsEnum)
                    throw new ArgumentException("Type must be for an Enum.");
                _enumType = value;
            }
        }
    }

    public EnumBindingSourceExtension()
    {
    }

    public EnumBindingSourceExtension(Type enumType)
    {
        EnumType = enumType;
    }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        if (_enumType == null)
            throw new InvalidOperationException("The EnumType must be specified.");

        Array enumValues = Enum.GetValues(_enumType);
        var list = new List<object>();
        foreach (var enumValue in enumValues)
        {
            list.Add(new
            {
                Value = enumValue,
                Description = GetEnumDescription((Enum)enumValue)
            });
        }

        return list;
    }

    private string GetEnumDescription(Enum enumValue)
    {
        FieldInfo fi = enumValue.GetType().GetField(enumValue.ToString());
        var attributes = (DescriptionAttribute[])fi.GetCustomAttributes(typeof(DescriptionAttribute), false);
        if (attributes != null && attributes.Length > 0)
            return attributes[0].Description;
        return enumValue.ToString();
    }
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
        FieldInfo field = type.GetField(name);
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
                var attribute = Attribute.GetCustomAttribute(field, typeof(DescriptionAttribute)) as DescriptionAttribute;
                if (attribute != null)
                {
                    if (attribute.Description == description)
                        return (T)field.GetValue(null);
                }
                else
                {
                    if (field.Name.Equals(description, StringComparison.OrdinalIgnoreCase))
                        return (T)field.GetValue(null);
                }
            }
        }
        throw new ArgumentException($"未能根据描述“{description}”找到对应的枚举值。", nameof(description));
    }
}

public class EnumDescriptionConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Enum enumValue)
        {
            FieldInfo field = enumValue.GetType().GetField(enumValue.ToString());
            var attributes = (DescriptionAttribute[])field.GetCustomAttributes(typeof(DescriptionAttribute), false);
            if (attributes != null && attributes.Length > 0)
            {
                return attributes[0].Description;
            }
            return enumValue.ToString();
        }
        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}