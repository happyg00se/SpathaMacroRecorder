using System.Collections.ObjectModel;
using System.Text;

namespace SpathaMacroRecorder.Models;

/// <summary>Группа стратагемы — по цвету значка в игре.</summary>
public enum StratagemGroup
{
    /// <summary>Синие: оружие поддержки, рюкзаки, экзокостюм.</summary>
    Supply,

    /// <summary>Красные: орбитальные удары и «Орёл».</summary>
    Offensive,

    /// <summary>Зелёные: турели, мины, укрепления.</summary>
    Defensive,

    /// <summary>Жёлтые: общие стратагемы задания.</summary>
    Mission,
}

/// <summary>
/// Стратагема Helldivers 2. Code — стрелки кода по порядку: U — вверх, D — вниз,
/// L — влево, R — вправо.
/// </summary>
public sealed record Stratagem(string Name, StratagemGroup Group, string Code)
{
    /// <summary>Код стрелками, как он нарисован в игре: «↓←↓↑→».</summary>
    public string Arrows => StratagemCatalog.ToArrows(Code);
}

/// <summary>
/// Встроенный список стратагем и превращение стратагемы в макрос. Названия и коды —
/// с русской страницы вики: https://helldivers.wiki.gg/wiki/Stratagems/ru.
/// </summary>
public static class StratagemCatalog
{
    public static IReadOnlyList<Stratagem> All { get; } =
    [
        // Оружие поддержки и рюкзаки
        new("MG-43 «Пулемёт»", StratagemGroup.Supply, "DLDUR"),
        new("APW-1 «Крупнокалиберная винтовка»", StratagemGroup.Supply, "DLRUD"),
        new("M-105 «Доблесть»", StratagemGroup.Supply, "DLDUUL"),
        new("EAT-17 «Одноразовый бронебой»", StratagemGroup.Supply, "DDLUR"),
        new("GR-8 «Безоткатная винтовка»", StratagemGroup.Supply, "DLRRL"),
        new("FLAM-40 «Огнемёт»", StratagemGroup.Supply, "DLUDU"),
        new("AC-8 «Автопушка»", StratagemGroup.Supply, "DLDUUR"),
        new("MG-206 «Тяжёлый пулемёт»", StratagemGroup.Supply, "DLUDD"),
        new("RL-77 «Ракетница с подрывом в воздухе»", StratagemGroup.Supply, "DUULR"),
        new("RS-422 «Рельсотрон»", StratagemGroup.Supply, "DRDULR"),
        new("FAF-14 «Копьё»", StratagemGroup.Supply, "DDUDD"),
        new("Ранец для прыжков Lift-850", StratagemGroup.Supply, "DUUDU"),
        new("Ящик с припасами B-1", StratagemGroup.Supply, "DLDUUR"),
        new("Гранатомет GL-21", StratagemGroup.Supply, "DLULD"),
        new("LAS-98 Laser Cannon", StratagemGroup.Supply, "DLDUL"),
        new("AX/LAS-5 «Страж»", StratagemGroup.Supply, "DULURR"),
        new("Рюкзак с баллистическим щитом SH-20", StratagemGroup.Supply, "DLDDUL"),
        new("Дуговой метатель ARC-3", StratagemGroup.Supply, "DRDULL"),
        new("Квазарная пушка LAS-99", StratagemGroup.Supply, "DDULR"),
        new("Щит-генератор SH-32", StratagemGroup.Supply, "DULRLR"),
        new("AX/AR-23 «Дозорный»", StratagemGroup.Supply, "DULURD"),
        new("Экзокостюм EXO-45 Patriot", StratagemGroup.Supply, "LDRULDD"),

        // Орбитальные удары и «Орёл»
        new("Орбитальный заградительный огонь Гатлинга", StratagemGroup.Offensive, "RDLUU"),
        new("Орбитальный удар с подрывом в воздухе", StratagemGroup.Offensive, "RRR"),
        new("Орбитальная 120-мм осколочно-фугасная заградительная батарея", StratagemGroup.Offensive, "RRDLRD"),
        new("Орбитальная 380-мм HE-бомба", StratagemGroup.Offensive, "RDUULDD"),
        new("Орбитальный заградительный огонь", StratagemGroup.Offensive, "RDRDRD"),
        new("Орбитальный лазер", StratagemGroup.Offensive, "RDURD"),
        new("Удар орбитальной рельсопушкой", StratagemGroup.Offensive, "RUDDR"),
        new("Вираж орла", StratagemGroup.Offensive, "URR"),
        new("Eagle Airstrike", StratagemGroup.Offensive, "URDR"),
        new("Орлиная кассетная бомба", StratagemGroup.Offensive, "URDDR"),
        new("Напалмовый авиаудар орла", StratagemGroup.Offensive, "URDU"),
        new("Дымовая завеса орла", StratagemGroup.Offensive, "URUD"),
        new("110-мм ракетные блоки «Орла»", StratagemGroup.Offensive, "URUL"),
        new("Бомба «Игл» весом 500 кг", StratagemGroup.Offensive, "URDDD"),
        new("Орбитальный высокоточный удар", StratagemGroup.Offensive, "RRU"),
        new("Орбитальный газовый удар", StratagemGroup.Offensive, "RRDR"),
        new("Орбитальный удар по электромагнитному полю", StratagemGroup.Offensive, "RRLD"),
        new("Орбитальный дымовой удар", StratagemGroup.Offensive, "RRDU"),

        // Оборона
        new("Расстановка крупнокалиберных пулеметов E/MG-101", StratagemGroup.Defensive, "DULRRL"),
        new("Реле генератора щита FX-12", StratagemGroup.Defensive, "DDLRLR"),
        new("Башня Теслы A/ARC-3", StratagemGroup.Defensive, "DURULR"),
        new("Противопехотное минное поле MD-6", StratagemGroup.Defensive, "DLUR"),
        new("MD-I4 Incendiary Mines", StratagemGroup.Defensive, "DLLD"),
        new("A/MG-43 Machine Gun Sentry", StratagemGroup.Defensive, "DURRU"),
        new("A/G-16 Gatling Sentry", StratagemGroup.Defensive, "DURL"),
        new("A/M-12 «Минометный дозор»", StratagemGroup.Defensive, "DURRD"),
        new("A/AC-8 Автопушечный дозор", StratagemGroup.Defensive, "DURULU"),
        new("A/MLS-4X «Ракетный дозор»", StratagemGroup.Defensive, "DURRL"),
        new("A/M-23 «Минометный дозор»", StratagemGroup.Defensive, "DURDR"),

        // Задание
        new("Подкрепление", StratagemGroup.Mission, "UDRLU"),
        new("Сигнал SOS", StratagemGroup.Mission, "UDRU"),
        new("Пополнение запасов", StratagemGroup.Mission, "DDUR"),
        new("Перезарядка «Орла»", StratagemGroup.Mission, "UULUR"),
        new("Доставка SSSD", StratagemGroup.Mission, "DDDUU"),
        new("Разведочное бурение", StratagemGroup.Mission, "DDLRDD"),
        new("Флаг суперземли", StratagemGroup.Mission, "DUDU"),
        new("Адская бомба", StratagemGroup.Mission, "DULDURDU"),
        new("Загрузить данные", StratagemGroup.Mission, "LRUUU"),
        new("Сейсмический зонд", StratagemGroup.Mission, "UULRDD"),
        new("Орбитальная осветительная ракета", StratagemGroup.Mission, "RRLL"),
        new("Артиллерия SEAF", StratagemGroup.Mission, "RUUD"),
    ];

    /// <summary>
    /// Пауза перед каждым нажатием и отпусканием стрелки, мс. Та же величина, что и пауза
    /// между нажатиями по умолчанию: стратагема вводится и тогда, когда ту выключили.
    /// </summary>
    public const int StepDelayMs = 30;

    // Стратагемы вводятся только клавиатурными стрелками: их игра принимает при зажатом Ctrl,
    // а персонаж от них не двигается. Scan-коды заданы прямо; стрелки — extended-клавиши,
    // без этого флага Windows приняла бы их за цифры NumPad.
    private static readonly Dictionary<char, (int Vk, ushort Scan)> DirectionKeys = new()
    {
        ['U'] = (0x26, 0x48), // ↑
        ['D'] = (0x28, 0x50), // ↓
        ['L'] = (0x25, 0x4B), // ←
        ['R'] = (0x27, 0x4D), // →
    };

    public static string ToArrows(string code)
    {
        var builder = new StringBuilder(code.Length);
        foreach (char direction in code)
        {
            builder.Append(direction switch
            {
                'U' => '↑',
                'D' => '↓',
                'L' => '←',
                'R' => '→',
                _ => '?',
            });
        }

        return builder.ToString();
    }

    /// <summary>Шаги макроса: нажать и отпустить клавишу каждой стрелки по порядку.</summary>
    public static ObservableCollection<MacroStep> BuildSteps(Stratagem stratagem)
    {
        var steps = new ObservableCollection<MacroStep>();
        foreach (char direction in stratagem.Code)
        {
            var (vk, scan) = DirectionKeys[direction];
            steps.Add(new KeyStep { DelayBeforeMs = StepDelayMs, Action = KeyAction.Down, VirtualKey = vk, ScanCode = scan, Extended = true });
            steps.Add(new KeyStep { DelayBeforeMs = StepDelayMs, Action = KeyAction.Up, VirtualKey = vk, ScanCode = scan, Extended = true });
        }

        return steps;
    }

    public static Macro CreateMacro(Stratagem stratagem) => new()
    {
        Id = Guid.NewGuid(),
        Name = stratagem.Name,
        Steps = BuildSteps(stratagem),
    };

    /// <summary>Стратагема, по которой назван макрос, либо null.</summary>
    public static Stratagem? ForMacro(Macro macro) =>
        All.FirstOrDefault(s => string.Equals(s.Name, macro.Name, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Макрос этой стратагемы из профиля; если его там ещё нет — создаётся и добавляется.
    /// Макрос, созданный раньше и, может быть, поправленный вручную, не пересоздаётся.
    /// </summary>
    public static Macro GetOrAddMacro(MacroProfile profile, Stratagem stratagem)
    {
        var existing = profile.Macros.FirstOrDefault(m => ReferenceEquals(ForMacro(m), stratagem));
        if (existing is not null)
        {
            return existing;
        }

        var macro = CreateMacro(stratagem);
        profile.Macros.Add(macro);
        return macro;
    }

    /// <summary>
    /// Подходит ли текст под запрос. Каждое слово запроса должно найтись в тексте; регистр,
    /// «ё», кавычки и дефисы не важны — «AC-8», «ac8» и «автопушка» находят одно и то же.
    /// </summary>
    public static bool Matches(string text, string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        string haystack = Normalize(text);
        string compact = haystack.Replace(" ", string.Empty, StringComparison.Ordinal);

        return Normalize(query)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .All(word => haystack.Contains(word, StringComparison.Ordinal) || compact.Contains(word, StringComparison.Ordinal));
    }

    /// <summary>Стратагемы под запрос в порядке каталога.</summary>
    public static IEnumerable<Stratagem> Search(string? query) => All.Where(s => Matches(s.Name, query));

    private static string Normalize(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (char c in text.ToLowerInvariant())
        {
            builder.Append(c switch
            {
                'ё' => 'е',
                _ when char.IsLetterOrDigit(c) => c,
                _ => ' ',
            });
        }

        return builder.ToString();
    }
}
