using SpathaMacroRecorder.Models;
using SpathaMacroRecorder.Native;

namespace SpathaMacroRecorder.Tests;

/// <summary>
/// Соответствие «метка на снимке → код кнопки». Порядок F17…F22 — только значение по
/// умолчанию: Armoury Crate не всегда даёт назначить F13–F24, поэтому фактический код
/// определяется на живой мыши и может быть любым.
/// </summary>
public class MouseButtonCatalogTests : IDisposable
{
    public MouseButtonCatalogTests() => MouseButtonCatalog.ApplyCodeOverrides(null);

    public void Dispose() => MouseButtonCatalog.ApplyCodeOverrides(null);

    [Fact]
    public void ByDefault_SideButtonsAnswerToF17ThroughF22()
    {
        Assert.Equal("macro1", MouseButtonCatalog.ButtonIdForVirtualKey(VirtualKeyCodes.F17));
        Assert.Equal("macro6", MouseButtonCatalog.ButtonIdForVirtualKey(VirtualKeyCodes.F22));
    }

    [Fact]
    public void DpiSwitch_IsNeverTreatedAsAMacroButton()
    {
        // Переключатель DPI не настраивается — его клавиша не должна поглощаться и срабатывать.
        Assert.Null(MouseButtonCatalog.ButtonIdForVirtualKey(VirtualKeyCodes.F23));
        Assert.False(MouseButtonCatalog.UsesVirtualKey(VirtualKeyCodes.F23));
    }

    [Fact]
    public void DetectedKey_ReplacesTheDefaultCode()
    {
        MouseButtonCatalog.ApplyCodeOverrides(new Dictionary<string, string>
        {
            ["macro1"] = new MouseButtonCatalog.KeyCode(VirtualKeyCodes.F1, false, false, false).Encode(),
        });

        Assert.Equal("macro1", MouseButtonCatalog.ButtonIdForVirtualKey(VirtualKeyCodes.F1));
        Assert.Null(MouseButtonCatalog.ButtonIdForVirtualKey(VirtualKeyCodes.F17));
    }

    [Fact]
    public void DetectedCombination_RequiresItsModifiers_ButToleratesExtraOnes()
    {
        MouseButtonCatalog.ApplyCodeOverrides(new Dictionary<string, string>
        {
            ["macro3"] = new MouseButtonCatalog.KeyCode(VirtualKeyCodes.D1, Ctrl: true, Alt: true, Shift: false).Encode(),
        });

        Assert.Equal("macro3", MouseButtonCatalog.ButtonIdForKey(VirtualKeyCodes.D1, ctrl: true, alt: true, shift: false));
        Assert.Equal("macro3", MouseButtonCatalog.ButtonIdForKey(VirtualKeyCodes.D1, ctrl: true, alt: true, shift: true));
        Assert.Null(MouseButtonCatalog.ButtonIdForKey(VirtualKeyCodes.D1, ctrl: true, alt: false, shift: false));
        Assert.Null(MouseButtonCatalog.ButtonIdForKey(VirtualKeyCodes.D1, ctrl: false, alt: false, shift: false));
    }

    [Fact]
    public void PlainKey_FiresWithCtrlHeld()
    {
        // Кнопка, определённая без модификаторов, срабатывает и при зажатом Ctrl стратагемы.
        Assert.Equal("macro1", MouseButtonCatalog.ButtonIdForKey(VirtualKeyCodes.F17, ctrl: true, alt: false, shift: false));
    }

    [Fact]
    public void WhenTwoButtonsMatch_TheMoreSpecificCombinationWins()
    {
        MouseButtonCatalog.ApplyCodeOverrides(new Dictionary<string, string>
        {
            ["macro1"] = new MouseButtonCatalog.KeyCode(VirtualKeyCodes.Q, false, false, false).Encode(),
            ["macro2"] = new MouseButtonCatalog.KeyCode(VirtualKeyCodes.Q, Ctrl: true, Alt: false, Shift: false).Encode(),
        });

        Assert.Equal("macro2", MouseButtonCatalog.ButtonIdForKey(VirtualKeyCodes.Q, ctrl: true, alt: false, shift: false));
        Assert.Equal("macro1", MouseButtonCatalog.ButtonIdForKey(VirtualKeyCodes.Q, ctrl: false, alt: false, shift: false));
    }

    [Fact]
    public void ScanCode_IdentifiesTheKeyWhenVirtualKeyChanges()
    {
        const ushort scanNumpad2 = 0x50;
        MouseButtonCatalog.ApplyCodeOverrides(new Dictionary<string, string>
        {
            ["macro4"] = new MouseButtonCatalog.KeyCode(0x62, false, false, false, scanNumpad2, Extended: false).Encode(),
        });

        Assert.Equal("macro4", MouseButtonCatalog.MatchKey(0x62, scanNumpad2, false, false, false, false));
        Assert.Equal("macro4", MouseButtonCatalog.MatchKey(VirtualKeyCodes.Down, scanNumpad2, false, false, false, false));
        Assert.Null(MouseButtonCatalog.MatchKey(VirtualKeyCodes.Down, scanNumpad2, true, false, false, false));
    }

    [Fact]
    public void EmptyCode_LeavesTheButtonWithoutAnyKey()
    {
        MouseButtonCatalog.ApplyCodeOverrides(new Dictionary<string, string>
        {
            ["macro1"] = string.Empty,
        });

        Assert.Null(MouseButtonCatalog.ButtonIdForVirtualKey(VirtualKeyCodes.F17));
        Assert.False(MouseButtonCatalog.UsesVirtualKey(VirtualKeyCodes.F17));
    }

    [Fact]
    public void ForwardAndBack_CanSwapTheirMouseCodes()
    {
        MouseButtonCatalog.ApplyCodeOverrides(new Dictionary<string, string>
        {
            ["fwd"] = "XBUTTON1",
            ["back"] = "XBUTTON2",
        });

        Assert.Equal("fwd", MouseButtonCatalog.ButtonIdForCode("XBUTTON1"));
        Assert.Equal("back", MouseButtonCatalog.ButtonIdForCode("XBUTTON2"));
    }

    [Fact]
    public void IsBoundKey_IgnoresModifiers()
    {
        MouseButtonCatalog.ApplyCodeOverrides(new Dictionary<string, string>
        {
            ["macro2"] = new MouseButtonCatalog.KeyCode(VirtualKeyCodes.Q, Ctrl: true, Alt: false, Shift: false).Encode(),
        });

        // Рекордер по этому признаку решает, что клавишу писать в макрос нельзя.
        Assert.True(MouseButtonCatalog.IsBoundKey(VirtualKeyCodes.Q, 0x10, false));
    }

    [Theory]
    [InlineData("F17", VirtualKeyCodes.F17, false, false, false, 0, false)]
    [InlineData("K:112", 112, false, false, false, 0, false)]
    [InlineData("K:112:C", 112, true, false, false, 0, false)]
    [InlineData("K:112:CAS", 112, true, true, true, 0, false)]
    [InlineData("K:98::80:0", 98, false, false, false, 80, false)]
    [InlineData("K:40:C:80:1", 40, true, false, false, 80, true)]
    public void DecodeKey_UnderstandsLegacyAndCompactForms(
        string code, int virtualKey, bool ctrl, bool alt, bool shift, int scanCode, bool extended)
    {
        var decoded = MouseButtonCatalog.DecodeKey(code);

        Assert.NotNull(decoded);
        Assert.Equal(new MouseButtonCatalog.KeyCode(virtualKey, ctrl, alt, shift, (ushort)scanCode, extended), decoded!.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("XBUTTON1")]
    [InlineData("nonsense")]
    [InlineData("K:98::zz")]
    public void DecodeKey_ReturnsNullForEverythingThatIsNotAKey(string code) =>
        Assert.Null(MouseButtonCatalog.DecodeKey(code));

    [Theory]
    [InlineData(VirtualKeyCodes.Delete, false, true, true, 0, false)]
    [InlineData(0x62, false, false, false, 0x50, false)]
    [InlineData(VirtualKeyCodes.Down, true, false, false, 0x50, true)]
    public void Encode_RoundTripsThroughDecode(int virtualKey, bool ctrl, bool alt, bool shift, int scanCode, bool extended)
    {
        var original = new MouseButtonCatalog.KeyCode(virtualKey, ctrl, alt, shift, (ushort)scanCode, extended);

        Assert.Equal(original, MouseButtonCatalog.DecodeKey(original.Encode()));
    }
}
