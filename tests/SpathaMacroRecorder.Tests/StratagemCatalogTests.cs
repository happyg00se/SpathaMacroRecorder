using System.Collections;
using System.Resources;
using SpathaMacroRecorder.Models;

namespace SpathaMacroRecorder.Tests;

public sealed class StratagemCatalogTests
{
    private static Stratagem ByRu(string nameRu) => StratagemCatalog.All.Single(s => s.NameRu == nameRu);

    [Fact]
    public void Catalog_HoldsEveryStratagemFromTheWiki()
    {
        Assert.Equal(114, StratagemCatalog.All.Count);
    }

    [Fact]
    public void Names_AreUniqueInBothLanguages()
    {
        Assert.Equal(StratagemCatalog.All.Count, StratagemCatalog.All.Select(s => s.NameRu).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(StratagemCatalog.All.Count, StratagemCatalog.All.Select(s => s.NameEn).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void Codes_UseOnlyTheFourDirections()
    {
        Assert.All(StratagemCatalog.All, s => Assert.Matches("^[UDLR]{3,10}$", s.Code));
    }

    [Fact]
    public void Icons_AreEmbeddedForEveryStratagem()
    {
        // WPF кладёт Resource-файлы в «<сборка>.g.resources» под путями в нижнем регистре.
        var assembly = typeof(StratagemCatalog).Assembly;
        using var stream = assembly.GetManifestResourceStream("SpathaMacroRecorder.g.resources");
        Assert.NotNull(stream);
        using var reader = new ResourceReader(stream);
        var embedded = reader.Cast<DictionaryEntry>().Select(e => (string)e.Key).ToHashSet();

        Assert.All(
            StratagemCatalog.All.Where(s => s.Icon is not null),
            s => Assert.Contains($"resources/stratagems/{s.Icon}.png", embedded));
        Assert.Single(StratagemCatalog.All, s => s.Icon is null);
    }

    [Theory]
    [InlineData("Укрепление", "↑↓→←↑")]
    [InlineData("Пулемет MG-43", "↓←↓↑→")]
    [InlineData("Набор припасов B-1", "↓←↓↑↑↓")]
    [InlineData("Доставка SSSD", "↓↓↓↓↓↑↑")]
    [InlineData("TD-220 «Бастион» MK XVI", "←↓→↓←↓↑↓↑")]
    [InlineData("Адская бомба NUX-223", "↓↑←↓↑→↓↑")]
    public void Arrows_MatchTheWiki(string nameRu, string arrows)
    {
        Assert.Equal(arrows, ByRu(nameRu).Arrows);
    }

    [Fact]
    public void Steps_PressAndReleaseArrowKeysOnly()
    {
        var stratagem = ByRu("Укрепление");

        var steps = StratagemCatalog.BuildSteps(stratagem).Cast<KeyStep>().ToList();

        Assert.Equal(stratagem.Code.Length * 2, steps.Count);
        Assert.All(steps, s => Assert.True(s.Extended));
        Assert.All(steps, s => Assert.True(s.DelayBeforeMs > 0));
        Assert.Equal(
            [0x26, 0x26, 0x28, 0x28, 0x27, 0x27, 0x25, 0x25, 0x26, 0x26],
            steps.Select(s => s.VirtualKey));
        Assert.Equal(
            [(ushort)0x48, (ushort)0x48, (ushort)0x50, (ushort)0x50, (ushort)0x4D, (ushort)0x4D, (ushort)0x4B, (ushort)0x4B, (ushort)0x48, (ushort)0x48],
            steps.Select(s => s.ScanCode));
        Assert.Equal(
            Enumerable.Range(0, steps.Count).Select(i => i % 2 == 0 ? KeyAction.Down : KeyAction.Up),
            steps.Select(s => s.Action));
    }

    [Fact]
    public void GetOrAddMacro_AddsOnceAndReusesAfterwards()
    {
        var profile = new MacroProfile { ProfileName = "Test" };
        var stratagem = ByRu("Орбитальный лазер");

        var first = StratagemCatalog.GetOrAddMacro(profile, stratagem);
        var second = StratagemCatalog.GetOrAddMacro(profile, stratagem);

        Assert.Same(first, second);
        Assert.Single(profile.Macros);
        Assert.True(stratagem.IsNamed(first.Name));
        Assert.Same(stratagem, StratagemCatalog.ForMacro(first));
    }

    [Theory]
    [InlineData("Orbital Laser")]
    [InlineData("Орбитальный лазер")]
    public void ForMacro_KnowsBothLanguages(string name)
    {
        var macro = new Macro { Id = Guid.NewGuid(), Name = name };
        Assert.Same(ByRu("Орбитальный лазер"), StratagemCatalog.ForMacro(macro));
    }

    [Theory]
    [InlineData("пулемет", "Пулемет MG-43")]
    [InlineData("ac8", "Автопушка AC-8")]
    [InlineData("ОРБИТАЛЬНЫЙ ЛАЗЕР", "Орбитальный лазер")]
    [InlineData("orbital laser", "Орбитальный лазер")]
    [InlineData("380 HE", "Орбитальный заградительный огонь из 380-мм HE-орудий")]
    [InlineData("sos", "Маяк SOS")]
    [InlineData("guard dog", "Страж AX/AR-23")]
    [InlineData("hellbomb", "Портативная адская бомба B-100")]
    public void Search_FindsByPartOfNameInEitherLanguage(string query, string expectedRu)
    {
        Assert.Contains(StratagemCatalog.Search(query), s => s.NameRu == expectedRu);
    }

    [Fact]
    public void Search_EmptyQueryReturnsEverything()
    {
        Assert.Equal(StratagemCatalog.All.Count, StratagemCatalog.Search("  ").Count());
    }

    [Fact]
    public void Search_UnknownWordFindsNothing()
    {
        Assert.Empty(StratagemCatalog.Search("лазер пулемёт"));
    }

    // Названия всех 63 стратагем, встроенных в Release 2.0.
    public static TheoryData<string> Release20Names =>
    [
        "MG-43 «Пулемёт»", "APW-1 «Крупнокалиберная винтовка»", "M-105 «Доблесть»", "EAT-17 «Одноразовый бронебой»",
        "GR-8 «Безоткатная винтовка»", "FLAM-40 «Огнемёт»", "AC-8 «Автопушка»", "MG-206 «Тяжёлый пулемёт»",
        "RL-77 «Ракетница с подрывом в воздухе»", "RS-422 «Рельсотрон»", "FAF-14 «Копьё»", "Ранец для прыжков Lift-850",
        "Ящик с припасами B-1", "Гранатомет GL-21", "LAS-98 Laser Cannon", "AX/LAS-5 «Страж»",
        "Рюкзак с баллистическим щитом SH-20", "Дуговой метатель ARC-3", "Квазарная пушка LAS-99", "Щит-генератор SH-32",
        "AX/AR-23 «Дозорный»", "Экзокостюм EXO-45 Patriot", "Орбитальный заградительный огонь Гатлинга",
        "Орбитальный удар с подрывом в воздухе", "Орбитальная 120-мм осколочно-фугасная заградительная батарея",
        "Орбитальная 380-мм HE-бомба", "Орбитальный заградительный огонь", "Орбитальный лазер",
        "Удар орбитальной рельсопушкой", "Вираж орла", "Eagle Airstrike", "Орлиная кассетная бомба",
        "Напалмовый авиаудар орла", "Дымовая завеса орла", "110-мм ракетные блоки «Орла»", "Бомба «Игл» весом 500 кг",
        "Орбитальный высокоточный удар", "Орбитальный газовый удар", "Орбитальный удар по электромагнитному полю",
        "Орбитальный дымовой удар", "Расстановка крупнокалиберных пулеметов E/MG-101", "Реле генератора щита FX-12",
        "Башня Теслы A/ARC-3", "Противопехотное минное поле MD-6", "MD-I4 Incendiary Mines", "A/MG-43 Machine Gun Sentry",
        "A/G-16 Gatling Sentry", "A/M-12 «Минометный дозор»", "A/AC-8 Автопушечный дозор", "A/MLS-4X «Ракетный дозор»",
        "A/M-23 «Минометный дозор»", "Подкрепление", "Сигнал SOS", "Пополнение запасов", "Перезарядка «Орла»",
        "Доставка SSSD", "Разведочное бурение", "Флаг суперземли", "Адская бомба", "Загрузить данные",
        "Сейсмический зонд", "Орбитальная осветительная ракета", "Артиллерия SEAF",
    ];

    [Theory]
    [MemberData(nameof(Release20Names))]
    public void ForMacro_RecognisesEveryRelease20Name(string oldName)
    {
        var macro = new Macro { Id = Guid.NewGuid(), Name = oldName };
        Assert.NotNull(StratagemCatalog.ForMacro(macro));
    }

    [Fact]
    public void Upgrade_RenamesOldMacroAndFixesChangedCode()
    {
        // В Release 2.0 у B-1 был код ↓←↓↑↑→, в игре теперь ↓←↓↑↑↓.
        var old = new Macro { Id = Guid.NewGuid(), Name = "Ящик с припасами B-1", Steps = StratagemCatalog.BuildSteps("DLDUUR") };
        var profile = new MacroProfile { ProfileName = "Test", Macros = [old] };

        Assert.True(StratagemCatalog.Upgrade(profile));

        var supplyPack = ByRu("Набор припасов B-1");
        Assert.True(supplyPack.IsNamed(old.Name));
        Assert.Equal(StratagemCatalog.BuildSteps(supplyPack), old.Steps);
        Assert.False(StratagemCatalog.Upgrade(profile));
    }

    [Fact]
    public void Upgrade_FixesCodeWhenNameStayedTheSame()
    {
        var old = new Macro { Id = Guid.NewGuid(), Name = "Доставка SSSD", Steps = StratagemCatalog.BuildSteps("DDDUU") };
        var profile = new MacroProfile { ProfileName = "Test", Macros = [old] };

        Assert.True(StratagemCatalog.Upgrade(profile));

        Assert.Equal("Доставка SSSD", old.Name);
        Assert.Equal(StratagemCatalog.BuildSteps("DDDDDUU"), old.Steps);
    }

    [Fact]
    public void Upgrade_LeavesHandEditedMacroAlone()
    {
        var steps = StratagemCatalog.BuildSteps("DLDUUR");
        steps[0].DelayBeforeMs = 80;
        var edited = new Macro { Id = Guid.NewGuid(), Name = "Ящик с припасами B-1", Steps = steps };
        var profile = new MacroProfile { ProfileName = "Test", Macros = [edited] };

        Assert.False(StratagemCatalog.Upgrade(profile));
        Assert.Equal("Ящик с припасами B-1", edited.Name);
    }
}
