using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace RepoGalaxy.Desktop.Converters;

/// <summary>
/// 将 Enum 转换为 Bool（用于状态判断）
/// </summary>
public class EnumToBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null || parameter == null)
            return false;

        return value.ToString()?.Equals(parameter.ToString(), StringComparison.OrdinalIgnoreCase) ?? false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
