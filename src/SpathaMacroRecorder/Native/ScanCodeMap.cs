namespace SpathaMacroRecorder.Native;

/// <summary>
/// Именованные virtual-key коды, которые используются в проекте — вместо магических чисел.
/// Значения соответствуют константам winuser.h (VK_*).
/// </summary>
internal static class VirtualKeyCodes
{
    internal const int Back = 0x08;
    internal const int Tab = 0x09;
    internal const int Return = 0x0D;
    internal const int Shift = 0x10;
    internal const int Control = 0x11;
    internal const int Pause = 0x13;
    internal const int ScrollLock = 0x91;
    internal const int Escape = 0x1B;
    internal const int Space = 0x20;
    internal const int PageUp = 0x21;
    internal const int PageDown = 0x22;
    internal const int End = 0x23;
    internal const int Home = 0x24;
    internal const int Left = 0x25;
    internal const int Up = 0x26;
    internal const int Right = 0x27;
    internal const int Down = 0x28;
    internal const int Insert = 0x2D;
    internal const int Delete = 0x2E;
    internal const int D0 = 0x30;
    internal const int D1 = 0x31;
    internal const int A = 0x41;
    internal const int E = 0x45;
    internal const int L = 0x4C;
    internal const int M = 0x4D;
    internal const int P = 0x50;
    internal const int Q = 0x51;
    internal const int S = 0x53;
    internal const int W = 0x57;
    internal const int Z = 0x5A;
    internal const int F1 = 0x70;
    internal const int F2 = 0x71;

    // F13–F24 существуют в HID-спецификации, распознаются Windows и не заняты играми —
    // именно на них назначаются доп. кнопки Spatha X в Armoury Crate (тех. пункт 1 задания).
    internal const int F13 = 0x7C;
    internal const int F14 = 0x7D;
    internal const int F15 = 0x7E;
    internal const int F16 = 0x7F;
    internal const int F17 = 0x80;
    internal const int F18 = 0x81;
    internal const int F19 = 0x82;
    internal const int F20 = 0x83;
    internal const int F21 = 0x84;
    internal const int F22 = 0x85;
    internal const int F23 = 0x86;
    internal const int F24 = 0x87;
    internal const int RControl = 0xA3;
    internal const int RMenu = 0xA5;
}

/// <summary>
/// Преобразование virtual-key кода в scan-код (через MapVirtualKey) и признак extended-клавиши.
/// MapVirtualKey extended-флаг не возвращает — для клавиш, у которых он не выводится однозначно
/// из VK (например NumPad Enter, использующий тот же VK_RETURN, что и обычный Enter), extended
/// определяется не отсюда, а из флага LLKHF_EXTENDED реального события хука в момент записи.
/// </summary>
internal static class ScanCodeMap
{
    private static readonly HashSet<int> ExtendedVirtualKeys =
    [
        VirtualKeyCodes.Left, VirtualKeyCodes.Up, VirtualKeyCodes.Right, VirtualKeyCodes.Down,
        VirtualKeyCodes.Insert, VirtualKeyCodes.Delete,
        VirtualKeyCodes.Home, VirtualKeyCodes.End,
        VirtualKeyCodes.PageUp, VirtualKeyCodes.PageDown,
        VirtualKeyCodes.RControl, VirtualKeyCodes.RMenu,
    ];

    internal static ushort GetScanCode(int virtualKey) =>
        (ushort)NativeMethods.MapVirtualKeyW((uint)virtualKey, NativeMethods.MAPVK_VK_TO_VSC);

    internal static bool IsExtended(int virtualKey) => ExtendedVirtualKeys.Contains(virtualKey);

    internal static (ushort ScanCode, bool Extended) Resolve(int virtualKey) =>
        (GetScanCode(virtualKey), IsExtended(virtualKey));
}
