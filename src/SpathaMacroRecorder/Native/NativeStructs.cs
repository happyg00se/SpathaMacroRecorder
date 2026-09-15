using System.Runtime.InteropServices;

namespace SpathaMacroRecorder.Native;

// Структуры Win32 для низкоуровневых хуков (WH_KEYBOARD_LL / WH_MOUSE_LL) и SendInput.
// Раскладка полей должна побайтово совпадать с объявлением в winuser.h — порядок и типы полей менять нельзя.

[StructLayout(LayoutKind.Sequential)]
internal struct POINT
{
    public int X;
    public int Y;
}

/// <summary>Сообщение очереди потока — нужно только для цикла GetMessage в HookThread.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct MSG
{
    public IntPtr Hwnd;
    public uint Message;
    public IntPtr WParam;
    public IntPtr LParam;
    public uint Time;
    public POINT Pt;
    public uint LPrivate;
}

[StructLayout(LayoutKind.Sequential)]
internal struct KBDLLHOOKSTRUCT
{
    public uint VkCode;
    public uint ScanCode;
    public uint Flags;
    public uint Time;
    public UIntPtr ExtraInfo;
}

[StructLayout(LayoutKind.Sequential)]
internal struct MSLLHOOKSTRUCT
{
    public POINT Pt;
    public uint MouseData;
    public uint Flags;
    public uint Time;
    public UIntPtr ExtraInfo;
}

[StructLayout(LayoutKind.Sequential)]
internal struct KEYBDINPUT
{
    public ushort WVk;
    public ushort WScan;
    public uint DwFlags;
    public uint Time;
    public UIntPtr DwExtraInfo;
}

[StructLayout(LayoutKind.Sequential)]
internal struct MOUSEINPUT
{
    public int Dx;
    public int Dy;
    public uint MouseData;
    public uint DwFlags;
    public uint Time;
    public UIntPtr DwExtraInfo;
}

[StructLayout(LayoutKind.Sequential)]
internal struct HARDWAREINPUT
{
    public uint UMsg;
    public ushort WParamL;
    public ushort WParamH;
}

[StructLayout(LayoutKind.Explicit)]
internal struct InputUnion
{
    [FieldOffset(0)] public MOUSEINPUT Mi;
    [FieldOffset(0)] public KEYBDINPUT Ki;
    [FieldOffset(0)] public HARDWAREINPUT Hi;
}

[StructLayout(LayoutKind.Sequential)]
internal struct INPUT
{
    public uint Type;
    public InputUnion U;
}
