using System.Runtime.InteropServices;

namespace SpathaMacroRecorder.Native;

internal delegate IntPtr LowLevelHookProc(int nCode, IntPtr wParam, IntPtr lParam);

internal static partial class NativeMethods
{
    private const string User32 = "user32.dll";
    private const string Kernel32 = "kernel32.dll";

    // Типы хуков (SetWindowsHookEx idHook).
    internal const int WH_KEYBOARD_LL = 13;
    internal const int WH_MOUSE_LL = 14;

    // Сообщения клавиатуры/мыши, приходящие в хук через wParam.
    internal const uint WM_QUIT = 0x0012;

    internal const int WM_KEYDOWN = 0x0100;
    internal const int WM_KEYUP = 0x0101;
    internal const int WM_SYSKEYDOWN = 0x0104;
    internal const int WM_SYSKEYUP = 0x0105;
    internal const int WM_LBUTTONDOWN = 0x0201;
    internal const int WM_LBUTTONUP = 0x0202;
    internal const int WM_RBUTTONDOWN = 0x0204;
    internal const int WM_RBUTTONUP = 0x0205;
    internal const int WM_MBUTTONDOWN = 0x0207;
    internal const int WM_MBUTTONUP = 0x0208;
    internal const int WM_MOUSEWHEEL = 0x020A;
    internal const int WM_MOUSEHWHEEL = 0x020E;
    internal const int WM_XBUTTONDOWN = 0x020B;
    internal const int WM_XBUTTONUP = 0x020C;
    internal const int WM_MOUSEMOVE = 0x0200;

    // KBDLLHOOKSTRUCT.Flags: бит "клавиша была уже нажата" (auto-repeat / переотправка) и extended.
    internal const uint LLKHF_EXTENDED = 0x01;
    internal const uint LLKHF_UP = 0x80;

    // Событие синтетическое (SendInput) — так приходят клавиши от G-Helper и Armoury Crate.
    internal const uint LLKHF_INJECTED = 0x10;
    internal const uint LLMHF_INJECTED = 0x01;

    // MapVirtualKey: код операции преобразования.
    internal const uint MAPVK_VK_TO_VSC = 0;

    // SendInput: тип элемента INPUT.
    internal const uint INPUT_MOUSE = 0;
    internal const uint INPUT_KEYBOARD = 1;

    // KEYBDINPUT.DwFlags.
    internal const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
    internal const uint KEYEVENTF_KEYUP = 0x0002;
    internal const uint KEYEVENTF_SCANCODE = 0x0008;

    // MOUSEINPUT.DwFlags.
    internal const uint MOUSEEVENTF_MOVE = 0x0001;
    internal const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    internal const uint MOUSEEVENTF_LEFTUP = 0x0004;
    internal const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
    internal const uint MOUSEEVENTF_RIGHTUP = 0x0010;
    internal const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
    internal const uint MOUSEEVENTF_MIDDLEUP = 0x0040;
    internal const uint MOUSEEVENTF_XDOWN = 0x0080;
    internal const uint MOUSEEVENTF_XUP = 0x0100;
    internal const uint MOUSEEVENTF_WHEEL = 0x0800;
    internal const uint MOUSEEVENTF_HWHEEL = 0x1000;
    internal const uint MOUSEEVENTF_ABSOLUTE = 0x8000;

    /// <summary>
    /// Подпись в dwExtraInfo всего ввода, который шлёт плеер. По ней хуки отличают свои события
    /// от чужих: LLKHF_INJECTED для этого не годится — синтетическим бывает и ввод от сервиса
    /// Armoury Crate, а его как раз нужно узнавать как нажатие кнопки мыши.
    /// </summary>
    internal static readonly UIntPtr SelfInputTag = (UIntPtr)0x53504D52;

    internal const int VK_SHIFT = 0x10;
    internal const int VK_CONTROL = 0x11;
    internal const int VK_MENU = 0x12;

    internal const ushort XBUTTON1 = 0x0001;
    internal const ushort XBUTTON2 = 0x0002;

    // GetSystemMetrics: размеры основного экрана, нужны для нормализации абсолютных координат
    // мыши в диапазон 0–65535 (режим рабочего стола, см. тех. пункт 3 задания).
    internal const int SM_CXSCREEN = 0;
    internal const int SM_CYSCREEN = 1;

    [LibraryImport(User32, SetLastError = true)]
    internal static partial IntPtr SetWindowsHookExW(int idHook, LowLevelHookProc lpfn, IntPtr hMod, uint dwThreadId);

    [LibraryImport(User32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool UnhookWindowsHookEx(IntPtr hhk);

    [LibraryImport(User32)]
    internal static partial IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    // Цикл сообщений отдельного потока хука (HookThread): callback'и низкоуровневых хуков
    // доставляются изнутри GetMessage, а WM_QUIT этот цикл завершает.
    [LibraryImport(User32)]
    internal static partial int GetMessageW(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [LibraryImport(User32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool PostThreadMessageW(uint idThread, uint msg, IntPtr wParam, IntPtr lParam);

    [LibraryImport(Kernel32)]
    internal static partial uint GetCurrentThreadId();

    [LibraryImport(Kernel32, EntryPoint = "GetModuleHandleW", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial IntPtr GetModuleHandle(string? lpModuleName);

    [LibraryImport(User32)]
    internal static partial uint SendInput(uint nInputs, [In] INPUT[] pInputs, int cbSize);

    [LibraryImport(User32)]
    internal static partial uint MapVirtualKeyW(uint uCode, uint uMapType);

    [LibraryImport(User32)]
    internal static partial int GetSystemMetrics(int nIndex);

    /// <summary>
    /// Возвращает окно консоли текущего процесса или NULL, если консоли нет. Нужно, чтобы не
    /// подключать консольный sink логгера в GUI-режиме: без консоли запись в неё может упасть.
    /// </summary>
    [LibraryImport(Kernel32)]
    internal static partial IntPtr GetConsoleWindow();

    /// <summary>Состояние клавиши прямо сейчас — нужно, чтобы узнать зажатые модификаторы.</summary>
    [LibraryImport("user32.dll")]
    internal static partial short GetAsyncKeyState(int vKey);

    internal static bool IsKeyDown(int virtualKey) => (GetAsyncKeyState(virtualKey) & 0x8000) != 0;

    /// <summary>Зажатые модификаторы прямо сейчас.</summary>
    internal static (bool Ctrl, bool Alt, bool Shift) ReadModifiers() =>
        (IsKeyDown(VK_CONTROL), IsKeyDown(VK_MENU), IsKeyDown(VK_SHIFT));
}
