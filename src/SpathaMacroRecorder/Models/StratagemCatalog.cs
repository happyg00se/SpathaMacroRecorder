using System.Collections.ObjectModel;
using System.Text;

namespace SpathaMacroRecorder.Models;

/// <summary>Раздел стратагемы — как на вики.</summary>
public enum StratagemGroup
{
    Orbital,
    Eagle,
    SupportWeapon,
    Backpack,
    Vehicle,
    Sentry,
    Emplacement,
    Mission,
}

/// <summary>
/// Стратагема Helldivers 2. Code — стрелки кода по порядку: U — вверх, D — вниз,
/// L — влево, R — вправо. Icon — имя картинки в Resources/Stratagems без расширения,
/// null — значка нет.
/// </summary>
public sealed record Stratagem(string NameRu, string NameEn, StratagemGroup Group, string Code, string? Icon)
{
    /// <summary>Название на языке интерфейса.</summary>
    public string Name => AppText.Instance.Language == "ru" ? NameRu : NameEn;

    /// <summary>Код стрелками, как он нарисован в игре: «↓←↓↑→».</summary>
    public string Arrows => StratagemCatalog.ToArrows(Code);

    /// <summary>Значок из игры, встроенный в exe.</summary>
    public string? IconUri => Icon is null
        ? null
        : $"pack://application:,,,/SpathaMacroRecorder;component/Resources/Stratagems/{Icon}.png";

    /// <summary>Назван ли так макрос — по-русски или по-английски.</summary>
    public bool IsNamed(string name) =>
        string.Equals(NameRu, name, StringComparison.OrdinalIgnoreCase)
        || string.Equals(NameEn, name, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Встроенный список стратагем и превращение стратагемы в макрос. Русские названия и коды —
/// с русской страницы вики: https://helldivers.wiki.gg/wiki/Stratagems/ru; там, где вики даёт
/// название по-английски, русское — перевод. Английские — названия из игры.
/// Значки — из открытого набора https://github.com/nvigneux/Helldivers-2-Stratagems-icons-svg,
/// переведённые в PNG 64×64: WPF сам SVG не рисует. Где своего значка в наборе нет, взят
/// тот, что стоит у стратагемы на вики (у «Доставки SSSD» — как у «Загрузить данные»,
/// у буровых установок — общий значок бура, у ретранслятора — как у грузового контейнера;
/// у «Тактической видеокамеры» значка нет).
/// </summary>
public static class StratagemCatalog
{
    public static IReadOnlyList<Stratagem> All { get; } =
    [
        // Орбитальные удары
        new("Орбитальный прецизионный удар", "Orbital Precision Strike", StratagemGroup.Orbital, "RRU", "orbital-precision-strike"),
        new("Орбитальный заградительный огонь Гатлинга", "Orbital Gatling Barrage", StratagemGroup.Orbital, "RDLUU", "orbital-gatling-barrage"),
        new("Орбитальный газовый удар", "Orbital Gas Strike", StratagemGroup.Orbital, "RRDR", "orbital-gas-strike"),
        new("Орбитальный 120-мм фугасный обстрел", "Orbital 120mm HE Barrage", StratagemGroup.Orbital, "RRDLRD", "orbital-120mm-he-barrage"),
        new("Орбитальный воздушный взрыв", "Orbital Airburst Strike", StratagemGroup.Orbital, "RRR", "orbital-airburst-strike"),
        new("Орбитальный дымовой удар", "Orbital Smoke Strike", StratagemGroup.Orbital, "RRDU", "orbital-smoke-strike"),
        new("Орбитальный удар", "Orbital EMS Strike", StratagemGroup.Orbital, "RRLD", "orbital-ems-strike"),
        new("Орбитальный заградительный огонь из 380-мм HE-орудий", "Orbital 380mm HE Barrage", StratagemGroup.Orbital, "RDUULDD", "orbital-380mm-he-barrage"),
        new("Орбитальный заградительный огонь", "Orbital Walking Barrage", StratagemGroup.Orbital, "RDRDRD", "orbital-walking-barrage"),
        new("Орбитальный лазер", "Orbital Laser", StratagemGroup.Orbital, "RDURD", "orbital-laser"),
        new("Орбитальный напалмовый заградительный огонь", "Orbital Napalm Barrage", StratagemGroup.Orbital, "RRDLRU", "orbital-napalm-barrage"),
        new("Орбитальный рельсопушечный удар", "Orbital Railcannon Strike", StratagemGroup.Orbital, "RUDDR", "orbital-railcannon-strike"),

        // «Орёл»: в новом списке этого раздела нет — названия и коды из первого списка
        new("Вираж орла", "Eagle Strafing Run", StratagemGroup.Eagle, "URR", "eagle-strafing-run"),
        new("Авиаудар орла", "Eagle Airstrike", StratagemGroup.Eagle, "URDR", "eagle-airstrike"),
        new("Орлиная кассетная бомба", "Eagle Cluster Bomb", StratagemGroup.Eagle, "URDDR", "eagle-cluster-bomb"),
        new("Напалмовый авиаудар орла", "Eagle Napalm Airstrike", StratagemGroup.Eagle, "URDU", "eagle-napalm-airstrike"),
        new("Дымовая завеса орла", "Eagle Smoke Strike", StratagemGroup.Eagle, "URUD", "eagle-smoke-strike"),
        new("110-мм ракетные блоки «Орла»", "Eagle 110mm Rocket Pods", StratagemGroup.Eagle, "URUL", "eagle-110mm-rocket-pods"),
        new("Бомба «Игл» весом 500 кг", "Eagle 500kg Bomb", StratagemGroup.Eagle, "URDDD", "eagle-500kg-bomb"),

        // Оружие поддержки
        new("Пулемет MG-43", "MG-43 Machine Gun", StratagemGroup.SupportWeapon, "DLDUR", "machine-gun"),
        new("ОПО-17", "EAT-17 Expendable Anti-Tank", StratagemGroup.SupportWeapon, "DDLUR", "expendable-anti-tank"),
        new("M-105 «Стойкая»", "M-105 Stalwart", StratagemGroup.SupportWeapon, "DLDUUL", "stalwart"),
        new("Лазерная пушка LAS-98", "LAS-98 Laser Cannon", StratagemGroup.SupportWeapon, "DLDUL", "laser-cannon"),
        new("Противотанковая винтовка APW-1", "APW-1 Anti-Materiel Rifle", StratagemGroup.SupportWeapon, "DLRUD", "anti-materiel-rifle"),
        new("GR-8 Безоткатное орудие", "GR-8 Recoilless Rifle", StratagemGroup.SupportWeapon, "DLRRL", "recoilless-rifle"),
        new("Гранатомет ГМ-21", "GL-21 Grenade Launcher", StratagemGroup.SupportWeapon, "DLULD", "grenade-launcher"),
        new("Огнемет ФЛАМ-40", "FLAM-40 Flamethrower", StratagemGroup.SupportWeapon, "DLUDU", "flamethrower"),
        new("Крупнокалиберный пулемёт MG-206", "MG-206 Heavy Machine Gun", StratagemGroup.SupportWeapon, "DLUDD", "heavy-machine-gun"),
        new("Автопушка AC-8", "AC-8 Autocannon", StratagemGroup.SupportWeapon, "DLDUUR", "autocannon"),
        new("Дуга ARC-3", "ARC-3 Arc Thrower", StratagemGroup.SupportWeapon, "DRDULL", "arc-thrower"),
        new("Квазарная пушка LAS-99", "LAS-99 Quasar Cannon", StratagemGroup.SupportWeapon, "DDULR", "quasar-cannon"),
        new("Реактивная система залпового огня RL-77", "RL-77 Airburst Rocket Launcher", StratagemGroup.SupportWeapon, "DUULR", "airburst-rocket-launcher"),
        new("«Коммандос» MLS-4X", "MLS-4X Commando", StratagemGroup.SupportWeapon, "DLUDR", "commando"),
        new("FAF-14 «Копьё»", "FAF-14 Spear", StratagemGroup.SupportWeapon, "DDUDD", "spear"),
        new("Рельсотрон RS-422", "RS-422 Railgun", StratagemGroup.SupportWeapon, "DRDULR", "railgun"),
        new("Пусковая установка W.A.S.P. StA-X3", "StA-X3 W.A.S.P. Launcher", StratagemGroup.SupportWeapon, "DDUDR", "sta-x3-w-a-s-p-launcher"),
        new("Пробивной молот CQC-20", "CQC-20 Breaching Hammer", StratagemGroup.SupportWeapon, "DLRLU", "cqc-20"),
        new("Эпоха PLAS-45", "PLAS-45 Epoch", StratagemGroup.SupportWeapon, "DLULR", "epoch"),
        new("MGX-42 «Шквал пуль»", "MGX-42 Bullet Storm", StratagemGroup.SupportWeapon, "DLDRUL", "bullet-storm"),
        new("Копье S-11", "S-11 Speargun", StratagemGroup.SupportWeapon, "DRDLUR", "speargun"),
        new("Инструмент для удаления листвы CQC-9", "CQC-9 Defoliation Tool", StratagemGroup.SupportWeapon, "DLRRD", "defoliation-tool"),
        new("«Деэскалатор» GL-52", "GL-52 De-Escalator", StratagemGroup.SupportWeapon, "DRULR", "gl-52-de-escalator"),
        new("«Расходуемый напалм» EAT-700", "EAT-700 Expendable Napalm", StratagemGroup.SupportWeapon, "DDLUL", "expendable-napalm"),
        new("Стерилизатор TX-41", "TX-41 Sterilizer", StratagemGroup.SupportWeapon, "DLUDL", "sterilizer"),
        new("Выравниватель EAT-411", "EAT-411 Leveller", StratagemGroup.SupportWeapon, "DDLUD", "eat-411"),
        new("Гранатомет с ленточным питанием GL-28", "GL-28 Belt-Fed Grenade Launcher", StratagemGroup.SupportWeapon, "DLULUU", "gl-28"),
        new("Пакет C4 B/MD", "B/MD C4 Pack", StratagemGroup.SupportWeapon, "DRUURU", "c4-pack"),
        new("Одиночный бункер MS-11", "MS-11 Solo Silo", StratagemGroup.SupportWeapon, "DURDD", "solo-silo"),
        new("B/FLAM-80 Крематор", "B/FLAM-80 Cremator", StratagemGroup.SupportWeapon, "DDRDUU", "cremator"),
        new("«Максиган» M-1000", "M-1000 Maxigun", StratagemGroup.SupportWeapon, "DLRDUU", "maxigun"),
        new("CQC-1 «Единственный истинный флаг»", "CQC-1 One True Flag", StratagemGroup.SupportWeapon, "DLRRU", "one-true-flag"),
        new("«Мельтаган» 40-го тысячелетия", "40-K Meltagun", StratagemGroup.SupportWeapon, "DLULLD", "40-k-meltagun"),

        // Рюкзаки
        new("Набор припасов B-1", "B-1 Supply Pack", StratagemGroup.Backpack, "DLDUUD", "supply-pack"),
        new("Ранец LIFT-850", "LIFT-850 Jump Pack", StratagemGroup.Backpack, "DUUDU", "jump-pack"),
        new("Рюкзак с баллистическим щитом SH-20", "SH-20 Ballistic Shield Backpack", StratagemGroup.Backpack, "DLDDUL", "ballistic-shield-backpack"),
        new("Страж AX/AR-23", "AX/AR-23 \"Guard Dog\"", StratagemGroup.Backpack, "DULURD", "guard-dog"),
        new("Ровер AX/LAS-5", "AX/LAS-5 \"Guard Dog\" Rover", StratagemGroup.Backpack, "DULURR", "guard-dog-rover"),
        new("Комплект генератора щитов SH-32", "SH-32 Shield Generator Pack", StratagemGroup.Backpack, "DULRLR", "shield-generator-pack"),
        new("Направленный щит SH-51", "SH-51 Directional Shield", StratagemGroup.Backpack, "DULRUU", "directional-shield"),
        new("AX/FLAM-75 «Хот-дог»", "AX/FLAM-75 \"Guard Dog\" Hot Dog", StratagemGroup.Backpack, "DULULL", "guard-dog-hot-dog"),
        new("Портативная адская бомба B-100", "B-100 Portable Hellbomb", StratagemGroup.Backpack, "DRUUU", "hellbomb-portable"),
        new("AX/ARC-3 K-9", "AX/ARC-3 \"Guard Dog\" K-9", StratagemGroup.Backpack, "DULURL", "guard-dog-k-9"),
        new("Парящий отряд LIFT-860", "LIFT-860 Hover Pack", StratagemGroup.Backpack, "DUUDLR", "hover-pack"),
        new("AX/TX-13 «Собачье дыхание»", "AX/TX-13 \"Guard Dog\" Dog Breath", StratagemGroup.Backpack, "DULURU", "guard-dog-breath"),
        new("Варп-набор LIFT-182", "LIFT-182 Warp Pack", StratagemGroup.Backpack, "DLRDLR", "warp-pack"),

        // Транспорт
        new("M-103 Транспортное средство снабжения", "M-103 Supply FRV", StratagemGroup.Vehicle, "LDLLDUR", "supply-frv"),
        new("TD-110 «Водоворот»", "TD-110 Maelstrom", StratagemGroup.Vehicle, "LDRDLDULR", "td-110-maelstrom"),
        new("Истребитель-перехватчик M-104 FRV", "M-104 Incinerator FRV", StratagemGroup.Vehicle, "LDRLDUU", "incinerator-frv"),
        new("Экзокостюм EXO-49 «Освободитель»", "EXO-49 Emancipator Exosuit", StratagemGroup.Vehicle, "LDRULDU", "emancipator-exosuit"),
        new("Экзокостюм EXO-45 «Патриот»", "EXO-45 Patriot Exosuit", StratagemGroup.Vehicle, "LDRULDD", "patriot-exosuit"),
        new("Стрелок M-102 на быстроходном разведывательном автомобиле", "M-102 Fast Recon Vehicle", StratagemGroup.Vehicle, "LDRDRDU", "fast-recon-vehicle"),
        new("TD-220 «Бастион» MK XVI", "TD-220 Bastion MK XVI", StratagemGroup.Vehicle, "LDRDLDUDU", "bastion-mk-xvi"),
        new("Экзокостюм прорыва EXO-55", "EXO-55 Breakthrough Exosuit", StratagemGroup.Vehicle, "LDRLRDU", "breakthrough-exosuit"),
        new("Экзоскелет лесоруба EXO-51", "EXO-51 Lumberer Exosuit", StratagemGroup.Vehicle, "LDRURLU", "lumberer-exosuit"),

        // Часовые
        new("Часовой с пулеметом MG-43", "A/MG-43 Machine Gun Sentry", StratagemGroup.Sentry, "DURRU", "machine-gun-sentry"),
        new("A/G-16 Гатлинг-страж", "A/G-16 Gatling Sentry", StratagemGroup.Sentry, "DURL", "gatling-sentry"),
        new("Часовой с автоматической пушкой AC-8", "A/AC-8 Autocannon Sentry", StratagemGroup.Sentry, "DURULU", "autocannon-sentry"),
        new("Часовой с минометом M-12", "A/M-12 Mortar Sentry", StratagemGroup.Sentry, "DURRD", "mortar-sentry"),
        new("Часовой с ракетной установкой MLS-4X", "A/MLS-4X Rocket Sentry", StratagemGroup.Sentry, "DURRL", "rocket-sentry"),
        new("Башня Теслы A/ARC-3", "A/ARC-3 Tesla Tower", StratagemGroup.Sentry, "DURULR", "tesla-tower"),
        new("Минометный дозор EMS A/M-23", "A/M-23 EMS Mortar Sentry", StratagemGroup.Sentry, "DURDR", "ems-mortar-sentry"),
        new("Лазерный часовой A/LAS-98", "A/LAS-98 Laser Sentry", StratagemGroup.Sentry, "DURDUR", "laser-sentry"),
        new("Огнемётный часовой A/FLAM-40", "A/FLAM-40 Flame Sentry", StratagemGroup.Sentry, "DURDUU", "flame-sentry"),
        new("A/GM-17 Газовый миномёт", "A/GM-17 Gas Mortar Sentry", StratagemGroup.Sentry, "DURDL", "gas-mortar-sentry"),

        // Огневые точки
        new("Противопехотное минное поле MD-6", "MD-6 Anti-Personnel Minefield", StratagemGroup.Emplacement, "DLUR", "anti-personnel-minefield"),
        new("Зажигательные мины MD-I4", "MD-I4 Incendiary Mines", StratagemGroup.Emplacement, "DLLD", "incendiary-mines"),
        new("Противотанковые мины MD-17", "MD-17 Anti-Tank Mines", StratagemGroup.Emplacement, "DLUU", "anti-tank-mines"),
        new("Реле генератора щита FX-12", "FX-12 Shield Generator Relay", StratagemGroup.Emplacement, "DDLRLR", "shield-generator-relay"),
        new("Огневая точка крупнокалиберного пулемета E/MG-101", "E/MG-101 HMG Emplacement", StratagemGroup.Emplacement, "DULRRL", "hmg-emplacement"),
        new("Гренадерская позиция E/GL-21", "E/GL-21 Grenadier Battlement", StratagemGroup.Emplacement, "DRDLR", "grenadier-battlement"),
        new("Газовые мины MD-8", "MD-8 Gas Mines", StratagemGroup.Emplacement, "DLLR", "gas-mine"),
        new("Противотанковая засада E/AT-12", "E/AT-12 Anti-Tank Emplacement", StratagemGroup.Emplacement, "DULRRR", "anti-tank-emplacement"),

        // Задание: корабль
        new("Перегруппировка орла", "Eagle Rearm", StratagemGroup.Mission, "UULUR", "eagle-rearm"),
        new("Вызвать суперразрушитель", "Call In Super Destroyer", StratagemGroup.Mission, "UUDDLRLR", "call-in-super-destroyer"),
        new("Маяк SOS", "SOS Beacon", StratagemGroup.Mission, "UDRU", "sos-beacon"),
        new("Пополнение запасов", "Resupply", StratagemGroup.Mission, "DDUR", "resupply"),
        new("Укрепление", "Reinforce", StratagemGroup.Mission, "UDRLU", "reinforce"),

        // Задание: цель
        new("Грузовой контейнер", "Cargo Container", StratagemGroup.Mission, "UUDDRD", "cargo-container"),
        new("Адская бомба NUX-223", "NUX-223 Hellbomb", StratagemGroup.Mission, "DULDURDU", "hellbomb"),
        new("Сейсмический зонд", "Seismic Probe", StratagemGroup.Mission, "UULRDD", "seismic-probe"),
        new("Доставка SSSD", "SSSD Delivery", StratagemGroup.Mission, "DDDDDUU", "upload-data"),
        new("Тектоническая скважина", "Tectonic Drill", StratagemGroup.Mission, "UDUDUD", "prospecting-drill"),
        new("Загрузить данные", "Upload Data", StratagemGroup.Mission, "LRUUU", "upload-data"),
        new("Pods для обучения", "Upload Escape Pod Data", StratagemGroup.Mission, "LRUUU", "upload-data"),
        new("Буровая установка для водоносных горизонтов", "Aquifer Drill", StratagemGroup.Mission, "LLLUDRDD", "prospecting-drill"),
        new("Портативный ретранслятор связи", "Portable Comms Relay", StratagemGroup.Mission, "UUDDLLD", "cargo-container"),
        new("Пробивная буровая установка", "Prospecting Drill", StratagemGroup.Mission, "DDLRDD", "prospecting-drill"),
        new("Буровая установка для разрушения ульев", "Hive Breaker Drill", StratagemGroup.Mission, "LUDRDD", "hive-breaker-drill"),
        new("Активировать буровую установку E-711", "Activate E-711 Drill", StratagemGroup.Mission, "DDLLDD", "prospecting-drill"),
        new("Темный сосуд", "Dark Fluid Vessel", StratagemGroup.Mission, "ULRDUU", "dark-fluid-vessel"),
        new("Суперземля", "Super Earth Flag", StratagemGroup.Mission, "DUDU", "super-earth-flag"),
        new("Артиллерия SEAF", "SEAF Artillery", StratagemGroup.Mission, "RUUD", "seaf-artillery"),
        new("Тактическая видеокамера", "Tactical Camera", StratagemGroup.Mission, "RDDULLU", null),

        // Задание: сейчас недоступно в игре
        new("Орбитальная осветительная ракета", "Orbital Illumination Flare", StratagemGroup.Mission, "RRLL", "orbital-illumination-flare"),
    ];

    /// <summary>
    /// Названия и коды из первого встроенного списка (Release 2.0). Макросы, созданные тогда,
    /// узнаются по старому названию и переводятся на новое — вместе с кодом, если тот в игре
    /// поменялся.
    /// </summary>
    private static readonly (string OldName, string NameEn, string OldCode)[] Legacy =
    [
        new("MG-43 «Пулемёт»", "MG-43 Machine Gun", "DLDUR"),
        new("APW-1 «Крупнокалиберная винтовка»", "APW-1 Anti-Materiel Rifle", "DLRUD"),
        new("M-105 «Доблесть»", "M-105 Stalwart", "DLDUUL"),
        new("EAT-17 «Одноразовый бронебой»", "EAT-17 Expendable Anti-Tank", "DDLUR"),
        new("GR-8 «Безоткатная винтовка»", "GR-8 Recoilless Rifle", "DLRRL"),
        new("FLAM-40 «Огнемёт»", "FLAM-40 Flamethrower", "DLUDU"),
        new("AC-8 «Автопушка»", "AC-8 Autocannon", "DLDUUR"),
        new("MG-206 «Тяжёлый пулемёт»", "MG-206 Heavy Machine Gun", "DLUDD"),
        new("RL-77 «Ракетница с подрывом в воздухе»", "RL-77 Airburst Rocket Launcher", "DUULR"),
        new("RS-422 «Рельсотрон»", "RS-422 Railgun", "DRDULR"),
        new("Ранец для прыжков Lift-850", "LIFT-850 Jump Pack", "DUUDU"),
        new("Ящик с припасами B-1", "B-1 Supply Pack", "DLDUUR"),
        new("Гранатомет GL-21", "GL-21 Grenade Launcher", "DLULD"),
        new("AX/LAS-5 «Страж»", "AX/LAS-5 \"Guard Dog\" Rover", "DULURR"),
        new("Дуговой метатель ARC-3", "ARC-3 Arc Thrower", "DRDULL"),
        new("Щит-генератор SH-32", "SH-32 Shield Generator Pack", "DULRLR"),
        new("AX/AR-23 «Дозорный»", "AX/AR-23 \"Guard Dog\"", "DULURD"),
        new("Экзокостюм EXO-45 Patriot", "EXO-45 Patriot Exosuit", "LDRULDD"),
        new("Орбитальный удар с подрывом в воздухе", "Orbital Airburst Strike", "RRR"),
        new("Орбитальная 120-мм осколочно-фугасная заградительная батарея", "Orbital 120mm HE Barrage", "RRDLRD"),
        new("Орбитальная 380-мм HE-бомба", "Orbital 380mm HE Barrage", "RDUULDD"),
        new("Удар орбитальной рельсопушкой", "Orbital Railcannon Strike", "RUDDR"),
        new("Орбитальный высокоточный удар", "Orbital Precision Strike", "RRU"),
        new("Орбитальный удар по электромагнитному полю", "Orbital EMS Strike", "RRLD"),
        new("Расстановка крупнокалиберных пулеметов E/MG-101", "E/MG-101 HMG Emplacement", "DULRRL"),
        new("A/M-12 «Минометный дозор»", "A/M-12 Mortar Sentry", "DURRD"),
        new("A/AC-8 Автопушечный дозор", "A/AC-8 Autocannon Sentry", "DURULU"),
        new("A/MLS-4X «Ракетный дозор»", "A/MLS-4X Rocket Sentry", "DURRL"),
        new("A/M-23 «Минометный дозор»", "A/M-23 EMS Mortar Sentry", "DURDR"),
        new("Подкрепление", "Reinforce", "UDRLU"),
        new("Сигнал SOS", "SOS Beacon", "UDRU"),
        new("Перезарядка «Орла»", "Eagle Rearm", "UULUR"),
        new("Доставка SSSD", "SSSD Delivery", "DDDUU"),
        new("Разведочное бурение", "Prospecting Drill", "DDLRDD"),
        new("Флаг суперземли", "Super Earth Flag", "DUDU"),
        new("Адская бомба", "NUX-223 Hellbomb", "DULDURDU"),
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
    public static ObservableCollection<MacroStep> BuildSteps(Stratagem stratagem) => BuildSteps(stratagem.Code);

    public static ObservableCollection<MacroStep> BuildSteps(string code)
    {
        var steps = new ObservableCollection<MacroStep>();
        foreach (char direction in code)
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

    /// <summary>
    /// Стратагема, по которой назван макрос, либо null. Узнаёт и русское, и английское название,
    /// и старое — из Release 2.0.
    /// </summary>
    public static Stratagem? ForMacro(Macro macro) =>
        All.FirstOrDefault(s => s.IsNamed(macro.Name))
        ?? (FindLegacy(macro.Name) is { } legacy ? ByEnglishName(legacy.NameEn) : null);

    private static (string OldName, string NameEn, string OldCode)? FindLegacy(string name)
    {
        foreach (var legacy in Legacy)
        {
            if (string.Equals(legacy.OldName, name, StringComparison.OrdinalIgnoreCase))
            {
                return legacy;
            }
        }

        return null;
    }

    private static Stratagem ByEnglishName(string nameEn) => All.Single(s => s.NameEn == nameEn);

    /// <summary>
    /// Приводит макросы стратагем, созданные прошлыми версиями, к нынешнему списку: старое
    /// название меняется на новое, старый код — на тот, что сейчас в игре. Макросы, шаги которых
    /// правили вручную, не трогаются. Возвращает true, если что-то поменялось.
    /// </summary>
    public static bool Upgrade(MacroProfile profile)
    {
        bool changed = false;
        foreach (var macro in profile.Macros)
        {
            var legacy = FindLegacy(macro.Name);
            var stratagem = All.FirstOrDefault(s => s.IsNamed(macro.Name))
                            ?? (legacy is { } l ? ByEnglishName(l.NameEn) : null);
            if (stratagem is null)
            {
                continue;
            }

            bool generatedByUs = macro.Steps.SequenceEqual(BuildSteps(stratagem))
                                 || (legacy is { } old && macro.Steps.SequenceEqual(BuildSteps(old.OldCode)));
            if (!generatedByUs)
            {
                continue;
            }

            if (!stratagem.IsNamed(macro.Name))
            {
                macro.Name = stratagem.Name;
                changed = true;
            }

            if (!macro.Steps.SequenceEqual(BuildSteps(stratagem)))
            {
                macro.Steps.Clear();
                foreach (var step in BuildSteps(stratagem))
                {
                    macro.Steps.Add(step);
                }

                changed = true;
            }
        }

        return changed;
    }

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

    /// <summary>Стратагемы под запрос в порядке каталога — по русскому и английскому названию сразу.</summary>
    public static IEnumerable<Stratagem> Search(string? query) => All.Where(s => Matches(SearchText(s), query));

    /// <summary>Текст, по которому ищется стратагема: оба названия.</summary>
    public static string SearchText(Stratagem stratagem) => $"{stratagem.NameRu} {stratagem.NameEn}";

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
