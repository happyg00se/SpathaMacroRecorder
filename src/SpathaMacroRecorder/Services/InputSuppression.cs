using SpathaMacroRecorder.Models;
using SpathaMacroRecorder.Native;

namespace SpathaMacroRecorder.Services;

/// <summary>
/// Решает прямо в callback'е хука, пропустить нажатие дальше или поглотить.
///
/// Клавиши, назначенные в Armoury Crate на кнопки мыши, существуют только ради этой программы.
/// Пропускать их дальше нельзя: Windows и игра реагируют на них сами — Windows запускает
/// по ним встроенные действия, а цифровой блок в игре читается как стрелки и сбивает ввод
/// стратагемы. Поэтому нажатие, в котором программа узнала кнопку мыши, до системы не доходит.
///
/// Это единственная работа сверх «отметить время и поставить в очередь», которую делает хук:
/// вызывается на каждое нажатие клавиши в системе, поэтому только сравнения без выделения
/// памяти, а модификаторы спрашиваются лишь тогда, когда клавиша вообще чем-то занята.
/// Вызывается только из потока хука — состояние «нажатие поглощено» принадлежит ему одному.
/// </summary>
internal static class InputSuppression
{
    /// <summary>Идёт определение кнопки: поглощается любая клавиша, кроме модификаторов.</summary>
    internal static volatile bool DetectionActive;

    /// <summary>
    /// Откуда берётся состояние Ctrl/Alt/Shift. По умолчанию — «ничего не зажато»: обращение
    /// к user32 прямо из разбора событий сделало бы его непроверяемым вне Windows. Настоящий
    /// источник подставляется при запуске приложения.
    /// </summary>
    internal static Func<(bool Ctrl, bool Alt, bool Shift)> ModifierState { get; set; } = NoModifiers;

    // Virtual-key → «нажатие этой клавиши было поглощено». Нужно для отпускания: к моменту
    // key-up модификаторы сочетания часто уже отпущены и сопоставление не прошло бы, а
    // отпускание без нажатия, дошедшее до игры, ей ни к чему.
    private static readonly bool[] SwallowedKeys = new bool[256];

    // То же для XBUTTON1/XBUTTON2 (индексы 1 и 2).
    private static readonly bool[] SwallowedXButtons = new bool[3];

    internal static bool ShouldSwallowKey(int virtualKey, ushort scanCode, bool extended, bool isKeyUp, bool fromSelf)
    {
        // Свой ввод плеера не трогаем никогда: макрос должен дойти до игры целиком.
        if (fromSelf || (uint)virtualKey >= SwallowedKeys.Length)
        {
            return false;
        }

        if (isKeyUp)
        {
            bool wasSwallowed = SwallowedKeys[virtualKey];
            SwallowedKeys[virtualKey] = false;
            return wasSwallowed;
        }

        bool swallow = DetectionActive
            ? !IsModifier(virtualKey)
            : MouseButtonCatalog.IsBoundKey(virtualKey, scanCode, extended)
              && MatchesWithCurrentModifiers(virtualKey, scanCode, extended);

        SwallowedKeys[virtualKey] = swallow;
        return swallow;
    }

    /// <summary>
    /// «Вперёд/назад» поглощаются только на время определения: иначе, показывая программе
    /// кнопку, пользователь заодно листал бы страницы в открытом окне. В остальное время у этих
    /// кнопок есть собственное назначение, и программа его не отнимает.
    /// </summary>
    internal static bool ShouldSwallowMouseButton(int message, int xButton, bool fromSelf)
    {
        if (fromSelf || (uint)xButton >= SwallowedXButtons.Length)
        {
            return false;
        }

        if (message == NativeMethods.WM_XBUTTONUP)
        {
            bool wasSwallowed = SwallowedXButtons[xButton];
            SwallowedXButtons[xButton] = false;
            return wasSwallowed;
        }

        if (message != NativeMethods.WM_XBUTTONDOWN)
        {
            return false;
        }

        SwallowedXButtons[xButton] = DetectionActive;
        return DetectionActive;
    }

    /// <summary>Ctrl/Alt/Shift/Win — только модификаторы, кнопкой мыши они быть не могут.</summary>
    internal static bool IsModifier(int virtualKey) => virtualKey
        is NativeMethods.VK_SHIFT or NativeMethods.VK_CONTROL or NativeMethods.VK_MENU
        or 0xA0 or 0xA1 or 0xA2 or 0xA3 or 0xA4 or 0xA5 or 0x5B or 0x5C;

    /// <summary>Возвращает исходное состояние — для тестов.</summary>
    internal static void Reset()
    {
        DetectionActive = false;
        ModifierState = NoModifiers;
        Array.Clear(SwallowedKeys);
        Array.Clear(SwallowedXButtons);
    }

    private static bool MatchesWithCurrentModifiers(int virtualKey, ushort scanCode, bool extended)
    {
        var (ctrl, alt, shift) = ModifierState();
        return MouseButtonCatalog.MatchKey(virtualKey, scanCode, extended, ctrl, alt, shift) is not null;
    }

    private static (bool Ctrl, bool Alt, bool Shift) NoModifiers() => (false, false, false);
}
