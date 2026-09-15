using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace SpathaMacroRecorder.Views.Converters;

public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Показывает элемент, пока значение false: обратная пара к BoolToVisibilityConverter.</summary>
public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>
/// Выбранная плашка/точка подсвечивается акцентным цветом, невыбранная — нейтральным контуром.
/// Цвета заданы значениями, а не ссылками на ресурсы: конвертер работает вне дерева ресурсов.
/// </summary>
public sealed class SelectionBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush Accent = BrushFactory.Frozen("#FF2A4D");
    private static readonly SolidColorBrush Neutral = BrushFactory.Frozen("#9A9A9A");

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? Accent : Neutral;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Обводка маркера кнопки: назначенная — акцентная, свободная — приглушённая.</summary>
public sealed class MarkerStrokeConverter : IValueConverter
{
    private static readonly SolidColorBrush Assigned = BrushFactory.Frozen("#FF2A4D");
    private static readonly SolidColorBrush Free = BrushFactory.Frozen("#8899A0AA");

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? Assigned : Free;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Свечение вокруг маркера — радиальное, чтобы кнопка «горела» на фотографии.</summary>
public sealed class MarkerGlowConverter : IValueConverter
{
    private static readonly RadialGradientBrush Assigned = BrushFactory.FrozenGlow("#FFFF2A4D", "#00FF2A4D");
    private static readonly RadialGradientBrush Free = BrushFactory.FrozenGlow("#6699A0AA", "#0099A0AA");

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? Assigned : Free;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

internal static class BrushFactory
{
    internal static SolidColorBrush Frozen(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }

    internal static RadialGradientBrush FrozenGlow(string centerHex, string edgeHex)
    {
        var brush = new RadialGradientBrush
        {
            GradientStops =
            [
                new GradientStop((Color)ColorConverter.ConvertFromString(centerHex), 0),
                new GradientStop((Color)ColorConverter.ConvertFromString(edgeHex), 1),
            ],
        };
        brush.Freeze();
        return brush;
    }
}
