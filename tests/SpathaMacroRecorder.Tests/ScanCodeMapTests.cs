using SpathaMacroRecorder.Native;

namespace SpathaMacroRecorder.Tests;

// Значения — стандартные PC/AT Set 1 scan-коды (MapVirtualKey(vk, MAPVK_VK_TO_VSC) на обычной
// US-раскладке); проверяются только физически однозначные, не-extended клавиши.
// Тест обращается к реальному user32.dll и поэтому выполняется только на Windows.
public class ScanCodeMapTests
{
    public static TheoryData<int, ushort> KnownVirtualKeyToScanCode => new()
    {
        { VirtualKeyCodes.Escape, 0x01 },
        { VirtualKeyCodes.D1, 0x02 },
        { VirtualKeyCodes.D0, 0x0B },
        { VirtualKeyCodes.Back, 0x0E },
        { VirtualKeyCodes.Tab, 0x0F },
        { VirtualKeyCodes.Q, 0x10 },
        { VirtualKeyCodes.W, 0x11 },
        { VirtualKeyCodes.E, 0x12 },
        { VirtualKeyCodes.P, 0x19 },
        { VirtualKeyCodes.Return, 0x1C },
        { VirtualKeyCodes.Control, 0x1D },
        { VirtualKeyCodes.A, 0x1E },
        { VirtualKeyCodes.S, 0x1F },
        { VirtualKeyCodes.L, 0x26 },
        { VirtualKeyCodes.Shift, 0x2A },
        { VirtualKeyCodes.Z, 0x2C },
        { VirtualKeyCodes.M, 0x32 },
        { VirtualKeyCodes.Space, 0x39 },
        { VirtualKeyCodes.F1, 0x3B },
        { VirtualKeyCodes.F2, 0x3C },
    };

    [Theory]
    [MemberData(nameof(KnownVirtualKeyToScanCode))]
    public void GetScanCode_ReturnsStandardSet1ScanCode(int virtualKey, ushort expectedScanCode)
    {
        var actual = ScanCodeMap.GetScanCode(virtualKey);

        Assert.Equal(expectedScanCode, actual);
    }

    [Theory]
    [InlineData(VirtualKeyCodes.Left)]
    [InlineData(VirtualKeyCodes.Up)]
    [InlineData(VirtualKeyCodes.Right)]
    [InlineData(VirtualKeyCodes.Down)]
    [InlineData(VirtualKeyCodes.Insert)]
    [InlineData(VirtualKeyCodes.Delete)]
    [InlineData(VirtualKeyCodes.Home)]
    [InlineData(VirtualKeyCodes.End)]
    [InlineData(VirtualKeyCodes.PageUp)]
    [InlineData(VirtualKeyCodes.PageDown)]
    [InlineData(VirtualKeyCodes.RControl)]
    [InlineData(VirtualKeyCodes.RMenu)]
    public void IsExtended_TrueForKeysListedInSpec(int virtualKey)
    {
        Assert.True(ScanCodeMap.IsExtended(virtualKey));
    }

    [Theory]
    [InlineData(VirtualKeyCodes.A)]
    [InlineData(VirtualKeyCodes.Return)]
    [InlineData(VirtualKeyCodes.Control)]
    [InlineData(VirtualKeyCodes.Shift)]
    public void IsExtended_FalseForRegularKeys(int virtualKey)
    {
        Assert.False(ScanCodeMap.IsExtended(virtualKey));
    }
}
