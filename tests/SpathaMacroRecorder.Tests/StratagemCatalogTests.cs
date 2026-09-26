using System.Collections;
using System.Resources;
using SpathaMacroRecorder.Models;

namespace SpathaMacroRecorder.Tests;

public sealed class StratagemCatalogTests
{
    [Fact]
    public void Catalog_HoldsEveryStratagemFromTheWiki()
    {
        Assert.Equal(63, StratagemCatalog.All.Count);
    }

    [Fact]
    public void Names_AreUnique()
    {
        var names = StratagemCatalog.All.Select(s => s.Name).ToList();
        Assert.Equal(names.Count, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void Codes_UseOnlyTheFourDirections()
    {
        Assert.All(StratagemCatalog.All, s => Assert.Matches("^[UDLR]{3,8}$", s.Code));
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

        Assert.All(StratagemCatalog.All, s => Assert.Contains($"resources/stratagems/{s.Icon}.png", embedded));
    }

    [Theory]
    [InlineData("Подкрепление", "↑↓→←↑")]
    [InlineData("MG-43 «Пулемёт»", "↓←↓↑→")]
    [InlineData("Орбитальная 380-мм HE-бомба", "→↓↑↑←↓↓")]
    [InlineData("Адская бомба", "↓↑←↓↑→↓↑")]
    public void Arrows_MatchTheWiki(string name, string arrows)
    {
        Assert.Equal(arrows, StratagemCatalog.All.Single(s => s.Name == name).Arrows);
    }

    [Fact]
    public void Steps_PressAndReleaseArrowKeysOnly()
    {
        var stratagem = StratagemCatalog.All.Single(s => s.Name == "Подкрепление");

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
        var stratagem = StratagemCatalog.All.Single(s => s.Name == "Орбитальный лазер");

        var first = StratagemCatalog.GetOrAddMacro(profile, stratagem);
        var second = StratagemCatalog.GetOrAddMacro(profile, stratagem);

        Assert.Same(first, second);
        Assert.Single(profile.Macros);
        Assert.Equal("Орбитальный лазер", first.Name);
        Assert.Same(stratagem, StratagemCatalog.ForMacro(first));
    }

    [Theory]
    [InlineData("пулемет", "MG-43 «Пулемёт»")]
    [InlineData("ac8", "AC-8 «Автопушка»")]
    [InlineData("ОРБИТАЛЬНЫЙ ЛАЗЕР", "Орбитальный лазер")]
    [InlineData("380 бомба", "Орбитальная 380-мм HE-бомба")]
    [InlineData("sos", "Сигнал SOS")]
    public void Search_FindsByPartOfName(string query, string expected)
    {
        Assert.Contains(StratagemCatalog.Search(query), s => s.Name == expected);
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
}
