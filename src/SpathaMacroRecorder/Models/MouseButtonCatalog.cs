using System.Globalization;
using SpathaMacroRecorder.Native;

namespace SpathaMacroRecorder.Models;

public enum MouseView
{
    Top,
    Side,
}

/// <summary>
/// Одна кнопка каталога: как называется на экране, в каком виде показывается, где рисуется
/// плашка и точка на силуэте, и каким реальным событием ловится.
/// </summary>
/// <param name="Code">
/// Внутренний код: "Left"/"XBUTTON1"/"WheelUp" для событий мыши, "F17"… для клавиатурных.
/// Пользователю не показывается никогда (п. 9 задания) — живёт только в модели и каталоге.
/// </param>
/// <param name="HeroX">
/// Позиция метки на снимке мыши сбоку (система координат 983×370). Взята из самой
/// фотографии: пометки заказчика распознаны по цвету, центр каждой и есть центр кнопки.
/// </param>
/// <param name="Assignable">
/// Можно ли назначить макрос. Настраиваются только 8 кнопок — 6 боковых и вперёд/назад;
/// основные клавиши, колесо и переключатель DPI трогать нельзя, они нужны для обычной работы.
/// </param>
public sealed record MouseButtonDefinition(
    string ButtonId,
    string DisplayName,
    MouseView View,
    TriggerSource Source,
    string Code,
    double PlateX,
    double PlateY,
    double PointX,
    double PointY,
    double HeroX,
    double HeroY,
    bool Assignable);

/// <summary>
/// Статический каталог кнопок Spatha X — single source of truth для UI, привязок и резолвинга
/// событий. Координаты заданы под один конкретный корпус (универсальность под другие мыши
/// не требуется) в системе координат канвы 640×420 диалога выбора триггера; HeroX/HeroY — в системе фото 732×732.
/// </summary>
public static class MouseButtonCatalog
{
    public const double CanvasWidth = 640;
    public const double CanvasHeight = 420;
    public const double PlateWidth = 150;
    public const double PlateHeight = 36;

    private static readonly MouseButtonDefinition[] Definitions =
    [
        // Вид сверху: 5 стандартных кнопок + колесо + переключатель DPI.
        new("lmb",       "Left button",   MouseView.Top, TriggerSource.MouseHook,    "Left",      40, 70,  280, 100, 0, 0, false),
        new("wheel",     "Wheel click",   MouseView.Top, TriggerSource.MouseHook,    "Middle",    40, 140, 320, 135, 0, 0, false),
        new("fwd",       "Forward",       MouseView.Top, TriggerSource.MouseHook,    "XBUTTON2",  40, 210, 250, 180, 112, 155, true),
        new("back",      "Back",          MouseView.Top, TriggerSource.MouseHook,    "XBUTTON1",  40, 280, 250, 215, 250, 103, true),
        new("rmb",       "Right button",  MouseView.Top, TriggerSource.MouseHook,    "Right",     450, 70,  360, 100, 0, 0, false),
        new("wheelup",   "Wheel up",      MouseView.Top, TriggerSource.MouseHook,    "WheelUp",   450, 140, 320, 112, 0, 0, false),
        new("wheeldown", "Wheel down",    MouseView.Top, TriggerSource.MouseHook,    "WheelDown", 450, 210, 320, 158, 0, 0, false),
        new("dpi",       "DPI switch",    MouseView.Top, TriggerSource.KeyboardHook, "F23",       450, 280, 320, 215, 0, 0, false),

        // Вид сбоку: боковой блок из 6 доп. кнопок. Низкоуровневый хук мыши их не видит —
        // они приходят как F17–F22, назначенные заранее в Armoury Crate (тех. пункт 1).
        new("macro1", "Side 1", MouseView.Side, TriggerSource.KeyboardHook, "F17", 40,  90,  265, 180, 483, 133, true),
        new("macro3", "Side 3", MouseView.Side, TriggerSource.KeyboardHook, "F19", 40,  180, 265, 212, 532, 188, true),
        new("macro5", "Side 5", MouseView.Side, TriggerSource.KeyboardHook, "F21", 40,  270, 265, 244, 435, 236, true),
        new("macro2", "Side 2", MouseView.Side, TriggerSource.KeyboardHook, "F18", 450, 90,  305, 180, 632, 107, true),
        new("macro4", "Side 4", MouseView.Side, TriggerSource.KeyboardHook, "F20", 450, 180, 305, 212, 637, 179, true),
        new("macro6", "Side 6", MouseView.Side, TriggerSource.KeyboardHook, "F22", 450, 270, 305, 244, 577, 261, true),
    ];

    private static readonly Dictionary<string, MouseButtonDefinition> ById =
        Definitions.ToDictionary(d => d.ButtonId, StringComparer.Ordinal);

    /// <summary>Исторические имена клавиш F13–F24 — коды по умолчанию из каталога.</summary>
    private static readonly Dictionary<string, int> LegacyKeyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["F13"] = VirtualKeyCodes.F13,
        ["F14"] = VirtualKeyCodes.F14,
        ["F15"] = VirtualKeyCodes.F15,
        ["F16"] = VirtualKeyCodes.F16,
        ["F17"] = VirtualKeyCodes.F17,
        ["F18"] = VirtualKeyCodes.F18,
        ["F19"] = VirtualKeyCodes.F19,
        ["F20"] = VirtualKeyCodes.F20,
        ["F21"] = VirtualKeyCodes.F21,
        ["F22"] = VirtualKeyCodes.F22,
        ["F23"] = VirtualKeyCodes.F23,
        ["F24"] = VirtualKeyCodes.F24,
    };

    /// <summary>
    /// Какой код на самом деле шлёт та или иная кнопка.
    ///
    /// Порядок F17…F22 в каталоге — только значение по умолчанию. Armoury Crate не у всех
    /// даёт назначить F13–F24, а порядок назначения вообще произвольный, поэтому фактическое
    /// соответствие определяется на живой мыши (кнопка «Определить») и хранится здесь.
    ///
    /// Словарь и таблица клавиш не правятся на месте, а подменяются целиком: нажатия
    /// разбираются из callback'а хука и из фонового потока, и те должны видеть либо старое
    /// соответствие, либо новое, но не переписанное наполовину.
    /// </summary>
    private static volatile Dictionary<string, string> _codeOverrides = new(StringComparer.Ordinal);

    private static volatile (string ButtonId, KeyCode Key)[] _keys = BuildKeyTable();

    /// <summary>
    /// Клавиша с модификаторами. ScanCode и Extended — физическое место клавиши: по ним нажатие
    /// узнаётся надёжнее, чем по virtual-key, который у цифрового блока меняется вместе с
    /// NumLock (NumPad2 превращается в «стрелку вниз»). Ноль — место неизвестно, сравнение
    /// идёт по virtual-key.
    /// </summary>
    public readonly record struct KeyCode(
        int VirtualKey, bool Ctrl, bool Alt, bool Shift, ushort ScanCode = 0, bool Extended = false)
    {
        /// <summary>Компактная запись для settings.json: «K:112», «K:112:CA», «K:98::80:0».</summary>
        public string Encode()
        {
            string mods = $"{(Ctrl ? "C" : string.Empty)}{(Alt ? "A" : string.Empty)}{(Shift ? "S" : string.Empty)}";

            if (ScanCode != 0)
            {
                return $"K:{VirtualKey}:{mods}:{ScanCode}:{(Extended ? 1 : 0)}";
            }

            return mods.Length == 0 ? $"K:{VirtualKey}" : $"K:{VirtualKey}:{mods}";
        }

        /// <summary>Сколько модификаторов требует сочетание: чем больше, тем оно конкретнее.</summary>
        internal int ModifierCount => (Ctrl ? 1 : 0) + (Alt ? 1 : 0) + (Shift ? 1 : 0);
    }

    /// <summary>Разбирает код кнопки: «K:98::80:0», «K:112:CA», историческое «F17». XBUTTON* — не клавиша.</summary>
    public static KeyCode? DecodeKey(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        string value = code.Trim();

        if (LegacyKeyNames.TryGetValue(value, out int legacy))
        {
            return new KeyCode(legacy, false, false, false);
        }

        if (!value.StartsWith("K:", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string[] parts = value.Split(':');
        if (parts.Length < 2
            || !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int virtualKey))
        {
            return null;
        }

        ushort scanCode = 0;
        if (parts.Length > 3
            && !ushort.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out scanCode))
        {
            return null;
        }

        string mods = parts.Length > 2 ? parts[2] : string.Empty;
        return new KeyCode(
            virtualKey,
            mods.Contains('C', StringComparison.OrdinalIgnoreCase),
            mods.Contains('A', StringComparison.OrdinalIgnoreCase),
            mods.Contains('S', StringComparison.OrdinalIgnoreCase),
            scanCode,
            Extended: parts.Length > 4 && parts[4] == "1");
    }

    /// <summary>Применяет сохранённое соответствие «кнопка → код». null/пусто — вернуть исходное.</summary>
    public static void ApplyCodeOverrides(IReadOnlyDictionary<string, string>? overrides)
    {
        var fresh = new Dictionary<string, string>(StringComparer.Ordinal);

        if (overrides is not null)
        {
            foreach (var (buttonId, code) in overrides)
            {
                if (ById.ContainsKey(buttonId))
                {
                    fresh[buttonId] = (code ?? string.Empty).Trim();
                }
            }
        }

        _codeOverrides = fresh;
        _keys = BuildKeyTable();
    }

    /// <summary>Код, которым кнопка отзывается сейчас: переопределённый или каталожный.</summary>
    public static string CodeFor(string buttonId) =>
        _codeOverrides.TryGetValue(buttonId, out string? code) ? code : Find(buttonId)?.Code ?? string.Empty;

    /// <summary>Откуда придёт событие: коды XBUTTON* ловит хук мыши, остальные — клавиатурный.</summary>
    public static TriggerSource SourceFor(string buttonId) =>
        CodeFor(buttonId).StartsWith("XBUTTON", StringComparison.OrdinalIgnoreCase)
            ? TriggerSource.MouseHook
            : TriggerSource.KeyboardHook;

    /// <summary>Настраиваемая кнопка, которая сейчас отзывается этим кодом.</summary>
    public static string? ButtonIdForCode(string code) =>
        Definitions.FirstOrDefault(d => d.Assignable
                                        && string.Equals(CodeFor(d.ButtonId), code, StringComparison.OrdinalIgnoreCase))
            ?.ButtonId;

    /// <summary>
    /// Кнопка, которой соответствует нажатие. Модификаторы сочетания должны быть зажаты, но
    /// лишние не мешают: в Helldivers 2 стратагемы вводятся с зажатым Ctrl, и кнопка,
    /// определённая без модификаторов, обязана срабатывать и так. Если подходят несколько
    /// кнопок, побеждает самое конкретное сочетание.
    /// </summary>
    public static string? MatchKey(int virtualKey, ushort scanCode, bool extended, bool ctrl, bool alt, bool shift)
    {
        string? best = null;
        int bestModifiers = -1;

        foreach (var (buttonId, key) in _keys)
        {
            if (!IsSameKey(key, virtualKey, scanCode, extended)
                || (key.Ctrl && !ctrl) || (key.Alt && !alt) || (key.Shift && !shift))
            {
                continue;
            }

            if (key.ModifierCount > bestModifiers)
            {
                best = buttonId;
                bestModifiers = key.ModifierCount;
            }
        }

        return best;
    }

    /// <summary>Кнопка для нажатия без известного scan-кода.</summary>
    public static string? ButtonIdForKey(int virtualKey, bool ctrl, bool alt, bool shift) =>
        MatchKey(virtualKey, 0, false, ctrl, alt, shift);

    /// <summary>Занята ли клавиша какой-нибудь кнопкой мыши — независимо от модификаторов.</summary>
    public static bool IsBoundKey(int virtualKey, ushort scanCode, bool extended)
    {
        foreach (var (_, key) in _keys)
        {
            if (IsSameKey(key, virtualKey, scanCode, extended))
            {
                return true;
            }
        }

        return false;
    }

    public static bool UsesVirtualKey(int virtualKey) => IsBoundKey(virtualKey, 0, false);

    private static bool IsSameKey(KeyCode key, int virtualKey, ushort scanCode, bool extended) =>
        key.ScanCode != 0 && scanCode != 0
            ? key.ScanCode == scanCode && key.Extended == extended
            : key.VirtualKey == virtualKey;

    /// <summary>Только настраиваемые кнопки: переключатель DPI и основные клавиши не трогаются.</summary>
    private static (string ButtonId, KeyCode Key)[] BuildKeyTable() =>
        Definitions
            .Where(d => d.Assignable)
            .Select(d => (d.ButtonId, Key: DecodeKey(CodeFor(d.ButtonId))))
            .Where(p => p.Key is not null)
            .Select(p => (p.ButtonId, Key: p.Key!.Value))
            .ToArray();

    public static IReadOnlyList<MouseButtonDefinition> All => Definitions;

    public static IEnumerable<MouseButtonDefinition> ForView(MouseView view) =>
        Definitions.Where(d => d.View == view && d.Assignable);

    /// <summary>Кнопки, которым можно назначить макрос.</summary>
    public static IEnumerable<MouseButtonDefinition> Assignable =>
        Definitions.Where(d => d.Assignable);

    public static MouseButtonDefinition? Find(string? buttonId) =>
        buttonId is not null && ById.TryGetValue(buttonId, out var definition) ? definition : null;

    /// <summary>Служебное имя кнопки. Для экрана берите AppText.ButtonName — оно переведено.</summary>
    public static string DisplayNameFor(string? buttonId) => Find(buttonId)?.DisplayName ?? "Not assigned";

    internal static string? ButtonIdForVirtualKey(int virtualKey) =>
        ButtonIdForKey(virtualKey, ctrl: false, alt: false, shift: false);

    /// <summary>Готовая привязка для модели: source/code заполняются из каталога, не вручную.</summary>
    public static MacroTrigger? CreateTrigger(string buttonId)
    {
        var definition = Find(buttonId);
        return definition is null
            ? null
            : new MacroTrigger
            {
                ButtonId = definition.ButtonId,
                Source = SourceFor(definition.ButtonId),
                Code = CodeFor(definition.ButtonId),
            };
    }
}
