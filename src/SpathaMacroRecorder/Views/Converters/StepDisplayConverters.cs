using System.Globalization;
using System.Windows.Data;
using System.Windows.Input;
using SpathaMacroRecorder.Models;

namespace SpathaMacroRecorder.Views.Converters;

/// <summary>
/// Конвертеры для read-only колонок таблицы шагов (Тип/Клавиша-Кнопка/Действие/dx,dy) — все
/// принимают на вход целый MacroStep (Binding без Path) и никогда не пишут назад: ConvertBack
/// не нужен, эти колонки в DataGrid помечены IsReadOnly.
/// </summary>
public sealed class StepTypeConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        KeyStep => "Keyboard",
        MouseStep => "Mouse",
        PauseStep => "Pause",
        _ => "—",
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class StepKeyOrButtonConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        KeyStep keyStep => DescribeKey(keyStep.VirtualKey),
        MouseStep { Button: { } button } => DescribeButton(button),
        MouseStep { Action: MouseStepAction.Move } => "move",
        MouseStep { Action: MouseStepAction.Wheel, WheelHorizontal: true } => "wheel (horizontal)",
        MouseStep { Action: MouseStepAction.Wheel } => "wheel",
        _ => "—",
    };

    private static string DescribeKey(int virtualKey)
    {
        var key = KeyInterop.KeyFromVirtualKey(virtualKey);
        return key == Key.None ? $"VK_{virtualKey:X2}" : key.ToString();
    }

    private static string DescribeButton(MouseButtonKind button) => button switch
    {
        MouseButtonKind.Left => "LMB",
        MouseButtonKind.Right => "RMB",
        MouseButtonKind.Middle => "MMB",
        MouseButtonKind.X1 => "Back",
        MouseButtonKind.X2 => "Forward",
        _ => button.ToString(),
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class StepActionConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        KeyStep { Action: KeyAction.Down } => "down",
        KeyStep { Action: KeyAction.Up } => "up",
        MouseStep { Action: MouseStepAction.Down } => "down",
        MouseStep { Action: MouseStepAction.Up } => "up",
        MouseStep { Action: MouseStepAction.Move } => "move",
        MouseStep { Action: MouseStepAction.Wheel } => "wheel",
        _ => "—",
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class StepDeltaConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        MouseStep { Action: MouseStepAction.Move } move => $"{move.Dx}, {move.Dy}",
        MouseStep { Action: MouseStepAction.Wheel } wheel => wheel.WheelDelta.ToString(culture),
        _ => string.Empty,
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
