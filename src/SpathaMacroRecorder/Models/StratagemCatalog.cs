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
/// L — влево, R — вправо. Icon — имя картинки в Resources/Stratagems без расширения.
/// </summary>
public sealed record Stratagem(string Name, StratagemGroup Group, string Code, string Icon)
{
    /// <summary>Код стрелками, как он нарисован в игре: «↓←↓↑→».</summary>
    public string Arrows => StratagemCatalog.ToArrows(Code);

    /// <summary>Значок из игры, встроенный в exe.</summary>
    public string IconUri => $"pack://application:,,,/SpathaMacroRecorder;component/Resources/Stratagems/{Icon}.png";
}

/// <summary>
/// Встроенный список стратагем и превращение стратагемы в макрос. Названия и коды —
/// с русской страницы вики: https://helldivers.wiki.gg/wiki/Stratagems/ru. Значки —
/// из открытого набора https://github.com/nvigneux/Helldivers-2-Stratagems-icons-svg,
/// переведённые в PNG 64×64: WPF сам SVG не рисует. У «Доставки SSSD» своего значка в наборе
/// нет, в игре у неё тот же, что у «Загрузить данные».
/// </summary>
public static class StratagemCatalog
{
    public static IReadOnlyList<Stratagem> All { get; } =
    [
        // Оружие поддержки и рюкзаки
        new("MG-43 «Пулемёт»", StratagemGroup.Supply, "DLDUR", "machine-gun"),
        new("APW-1 «Крупнокалиберная винтовка»", StratagemGroup.Supply, "DLRUD", "anti-materiel-rifle"),
        new("M-105 «Доблесть»", StratagemGroup.Supply, "DLDUUL", "stalwart"),
        new("EAT-17 «Одноразовый бронебой»", StratagemGroup.Supply, "DDLUR", "expendable-anti-tank"),
        new("GR-8 «Безоткатная винтовка»", StratagemGroup.Supply, "DLRRL", "recoilless-rifle"),
        new("FLAM-40 «Огнемёт»", StratagemGroup.Supply, "DLUDU", "flamethrower"),
        new("AC-8 «Автопушка»", StratagemGroup.Supply, "DLDUUR", "autocannon"),
        new("MG-206 «Тяжёлый пулемёт»", StratagemGroup.Supply, "DLUDD", "heavy-machine-gun"),
        new("RL-77 «Ракетница с подрывом в воздухе»", StratagemGroup.Supply, "DUULR", "airburst-rocket-launcher"),
        new("RS-422 «Рельсотрон»", StratagemGroup.Supply, "DRDULR", "railgun"),
        new("FAF-14 «Копьё»", StratagemGroup.Supply, "DDUDD", "spear"),
        new("Ранец для прыжков Lift-850", StratagemGroup.Supply, "DUUDU", "jump-pack"),
        new("Ящик с припасами B-1", StratagemGroup.Supply, "DLDUUR", "supply-pack"),
        new("Гранатомет GL-21", StratagemGroup.Supply, "DLULD", "grenade-launcher"),
        new("LAS-98 Laser Cannon", StratagemGroup.Supply, "DLDUL", "laser-cannon"),
        new("AX/LAS-5 «Страж»", StratagemGroup.Supply, "DULURR", "guard-dog-rover"),
        new("Рюкзак с баллистическим щитом SH-20", StratagemGroup.Supply, "DLDDUL", "ballistic-shield-backpack"),
        new("Дуговой метатель ARC-3", StratagemGroup.Supply, "DRDULL", "arc-thrower"),
        new("Квазарная пушка LAS-99", StratagemGroup.Supply, "DDULR", "quasar-cannon"),
        new("Щит-генератор SH-32", StratagemGroup.Supply, "DULRLR", "shield-generator-pack"),
        new("AX/AR-23 «Дозорный»", StratagemGroup.Supply, "DULURD", "guard-dog"),
        new("Экзокостюм EXO-45 Patriot", StratagemGroup.Supply, "LDRULDD", "patriot-exosuit"),

        // Орбитальные удары и «Орёл»
        new("Орбитальный заградительный огонь Гатлинга", StratagemGroup.Offensive, "RDLUU", "orbital-gatling-barrage"),
        new("Орбитальный удар с подрывом в воздухе", StratagemGroup.Offensive, "RRR", "orbital-airburst-strike"),
        new("Орбитальная 120-мм осколочно-фугасная заградительная батарея", StratagemGroup.Offensive, "RRDLRD", "orbital-120mm-he-barrage"),
        new("Орбитальная 380-мм HE-бомба", StratagemGroup.Offensive, "RDUULDD", "orbital-380mm-he-barrage"),
        new("Орбитальный заградительный огонь", StratagemGroup.Offensive, "RDRDRD", "orbital-walking-barrage"),
        new("Орбитальный лазер", StratagemGroup.Offensive, "RDURD", "orbital-laser"),
        new("Удар орбитальной рельсопушкой", StratagemGroup.Offensive, "RUDDR", "orbital-railcannon-strike"),
        new("Вираж орла", StratagemGroup.Offensive, "URR", "eagle-strafing-run"),
        new("Eagle Airstrike", StratagemGroup.Offensive, "URDR", "eagle-airstrike"),
        new("Орлиная кассетная бомба", StratagemGroup.Offensive, "URDDR", "eagle-cluster-bomb"),
        new("Напалмовый авиаудар орла", StratagemGroup.Offensive, "URDU", "eagle-napalm-airstrike"),
        new("Дымовая завеса орла", StratagemGroup.Offensive, "URUD", "eagle-smoke-strike"),
        new("110-мм ракетные блоки «Орла»", StratagemGroup.Offensive, "URUL", "eagle-110mm-rocket-pods"),
        new("Бомба «Игл» весом 500 кг", StratagemGroup.Offensive, "URDDD", "eagle-500kg-bomb"),
        new("Орбитальный высокоточный удар", StratagemGroup.Offensive, "RRU", "orbital-precision-strike"),
        new("Орбитальный газовый удар", StratagemGroup.Offensive, "RRDR", "orbital-gas-strike"),
        new("Орбитальный удар по электромагнитному полю", StratagemGroup.Offensive, "RRLD", "orbital-ems-strike"),
        new("Орбитальный дымовой удар", StratagemGroup.Offensive, "RRDU", "orbital-smoke-strike"),

        // Оборона
        new("Расстановка крупнокалиберных пулеметов E/MG-101", StratagemGroup.Defensive, "DULRRL", "hmg-emplacement"),
        new("Реле генератора щита FX-12", StratagemGroup.Defensive, "DDLRLR", "shield-generator-relay"),
        new("Башня Теслы A/ARC-3", StratagemGroup.Defensive, "DURULR", "tesla-tower"),
        new("Противопехотное минное поле MD-6", StratagemGroup.Defensive, "DLUR", "anti-personnel-minefield"),
        new("MD-I4 Incendiary Mines", StratagemGroup.Defensive, "DLLD", "incendiary-mines"),
        new("A/MG-43 Machine Gun Sentry", StratagemGroup.Defensive, "DURRU", "machine-gun-sentry"),
        new("A/G-16 Gatling Sentry", StratagemGroup.Defensive, "DURL", "gatling-sentry"),
        new("A/M-12 «Минометный дозор»", StratagemGroup.Defensive, "DURRD", "mortar-sentry"),
        new("A/AC-8 Автопушечный дозор", StratagemGroup.Defensive, "DURULU", "autocannon-sentry"),
        new("A/MLS-4X «Ракетный дозор»", StratagemGroup.Defensive, "DURRL", "rocket-sentry"),
        new("A/M-23 «Минометный дозор»", StratagemGroup.Defensive, "DURDR", "ems-mortar-sentry"),

        // Задание
        new("Подкрепление", StratagemGroup.Mission, "UDRLU", "reinforce"),
        new("Сигнал SOS", StratagemGroup.Mission, "UDRU", "sos-beacon"),
        new("Пополнение запасов", StratagemGroup.Mission, "DDUR", "resupply"),
        new("Перезарядка «Орла»", StratagemGroup.Mission, "UULUR", "eagle-rearm"),
        new("Доставка SSSD", StratagemGroup.Mission, "DDDUU", "upload-data"),
        new("Разведочное бурение", StratagemGroup.Mission, "DDLRDD", "prospecting-drill"),
        new("Флаг суперземли", StratagemGroup.Mission, "DUDU", "super-earth-flag"),
        new("Адская бомба", StratagemGroup.Mission, "DULDURDU", "hellbomb"),
        new("Загрузить данные", StratagemGroup.Mission, "LRUUU", "upload-data"),
        new("Сейсмический зонд", StratagemGroup.Mission, "UULRDD", "seismic-probe"),
        new("Орбитальная осветительная ракета", StratagemGroup.Mission, "RRLL", "orbital-illumination-flare"),
        new("Артиллерия SEAF", StratagemGroup.Mission, "RUUD", "seaf-artillery"),
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
