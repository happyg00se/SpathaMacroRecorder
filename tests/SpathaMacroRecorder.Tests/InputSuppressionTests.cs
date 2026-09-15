using SpathaMacroRecorder.Models;
using SpathaMacroRecorder.Native;
using SpathaMacroRecorder.Services;

namespace SpathaMacroRecorder.Tests;

/// <summary>
/// Что поглощает хук. Клавиши кнопок мыши не должны доходить до Windows и игры, а всё
/// остальное обязано доходить без изменений.
/// </summary>
public class InputSuppressionTests : IDisposable
{
    private const ushort ScanF17 = 0x68;
    private const ushort ScanW = 0x11;
    private const ushort ScanNumpad2 = 0x50;
    private const int VkNumpad2 = 0x62;

    public InputSuppressionTests()
    {
        InputSuppression.Reset();
        MouseButtonCatalog.ApplyCodeOverrides(null);
    }

    public void Dispose()
    {
        InputSuppression.Reset();
        MouseButtonCatalog.ApplyCodeOverrides(null);
    }

    [Fact]
    public void BoundKey_IsSwallowedOnPressAndRelease()
    {
        Assert.True(InputSuppression.ShouldSwallowKey(VirtualKeyCodes.F17, ScanF17, false, isKeyUp: false, fromSelf: false));
        Assert.True(InputSuppression.ShouldSwallowKey(VirtualKeyCodes.F17, ScanF17, false, isKeyUp: true, fromSelf: false));
    }

    [Fact]
    public void OrdinaryKey_PassesThrough()
    {
        Assert.False(InputSuppression.ShouldSwallowKey(VirtualKeyCodes.W, ScanW, false, isKeyUp: false, fromSelf: false));
        Assert.False(InputSuppression.ShouldSwallowKey(VirtualKeyCodes.W, ScanW, false, isKeyUp: true, fromSelf: false));
    }

    [Fact]
    public void OwnPlaybackInput_IsNeverSwallowed()
    {
        // Макрос, в котором есть клавиша кнопки мыши, должен дойти до игры целиком.
        Assert.False(InputSuppression.ShouldSwallowKey(VirtualKeyCodes.F17, ScanF17, false, isKeyUp: false, fromSelf: true));
    }

    [Fact]
    public void BoundKey_IsSwallowedWhileCtrlIsHeld()
    {
        // В Helldivers 2 стратагемы вводятся с зажатым Ctrl.
        InputSuppression.ModifierState = static () => (true, false, false);

        Assert.True(InputSuppression.ShouldSwallowKey(VirtualKeyCodes.F17, ScanF17, false, isKeyUp: false, fromSelf: false));
    }

    [Fact]
    public void Release_IsSwallowedAfterModifiersWereLetGo()
    {
        MouseButtonCatalog.ApplyCodeOverrides(new Dictionary<string, string>
        {
            ["macro1"] = new MouseButtonCatalog.KeyCode(VirtualKeyCodes.D1, Ctrl: true, Alt: true, Shift: false).Encode(),
        });

        InputSuppression.ModifierState = static () => (true, true, false);
        Assert.True(InputSuppression.ShouldSwallowKey(VirtualKeyCodes.D1, 0x02, false, isKeyUp: false, fromSelf: false));

        InputSuppression.ModifierState = static () => (false, false, false);
        Assert.True(InputSuppression.ShouldSwallowKey(VirtualKeyCodes.D1, 0x02, false, isKeyUp: true, fromSelf: false));
    }

    [Fact]
    public void CombinationKey_WithoutItsModifiers_PassesThrough()
    {
        MouseButtonCatalog.ApplyCodeOverrides(new Dictionary<string, string>
        {
            ["macro1"] = new MouseButtonCatalog.KeyCode(VirtualKeyCodes.D1, Ctrl: true, Alt: true, Shift: false).Encode(),
        });

        // Обычная «1» на клавиатуре должна печататься, как печаталась.
        Assert.False(InputSuppression.ShouldSwallowKey(VirtualKeyCodes.D1, 0x02, false, isKeyUp: false, fromSelf: false));
    }

    [Fact]
    public void NumpadKey_IsRecognisedWhateverNumLockSays()
    {
        // С включённым NumLock цифровой блок шлёт NumPad2, с выключенным — «стрелку вниз»
        // с тем же scan-кодом. Кнопка мыши должна узнаваться в обоих случаях.
        MouseButtonCatalog.ApplyCodeOverrides(new Dictionary<string, string>
        {
            ["macro2"] = new MouseButtonCatalog.KeyCode(VkNumpad2, false, false, false, ScanNumpad2, Extended: false).Encode(),
        });

        Assert.True(InputSuppression.ShouldSwallowKey(VkNumpad2, ScanNumpad2, false, isKeyUp: false, fromSelf: false));
        Assert.True(InputSuppression.ShouldSwallowKey(VkNumpad2, ScanNumpad2, false, isKeyUp: true, fromSelf: false));
        Assert.True(InputSuppression.ShouldSwallowKey(VirtualKeyCodes.Down, ScanNumpad2, false, isKeyUp: false, fromSelf: false));
    }

    [Fact]
    public void RealArrowKey_IsNotMistakenForTheNumpadOne()
    {
        // Настоящая стрелка вниз — тот же scan-код, но extended. Ей в игре пользуются.
        MouseButtonCatalog.ApplyCodeOverrides(new Dictionary<string, string>
        {
            ["macro2"] = new MouseButtonCatalog.KeyCode(VkNumpad2, false, false, false, ScanNumpad2, Extended: false).Encode(),
        });

        Assert.False(InputSuppression.ShouldSwallowKey(VirtualKeyCodes.Down, ScanNumpad2, true, isKeyUp: false, fromSelf: false));
    }

    [Fact]
    public void DuringDetection_AnyKeyButModifiersIsSwallowed()
    {
        InputSuppression.DetectionActive = true;

        Assert.True(InputSuppression.ShouldSwallowKey(VirtualKeyCodes.W, ScanW, false, isKeyUp: false, fromSelf: false));
        Assert.False(InputSuppression.ShouldSwallowKey(VirtualKeyCodes.Control, 0x1D, false, isKeyUp: false, fromSelf: false));
    }

    [Fact]
    public void DetectedKeyRelease_IsSwallowedAfterDetectionEnds()
    {
        InputSuppression.DetectionActive = true;
        Assert.True(InputSuppression.ShouldSwallowKey(VirtualKeyCodes.W, ScanW, false, isKeyUp: false, fromSelf: false));

        InputSuppression.DetectionActive = false;
        Assert.True(InputSuppression.ShouldSwallowKey(VirtualKeyCodes.W, ScanW, false, isKeyUp: true, fromSelf: false));
    }

    [Fact]
    public void MouseSideButtons_AreSwallowedOnlyDuringDetection()
    {
        Assert.False(InputSuppression.ShouldSwallowMouseButton(NativeMethods.WM_XBUTTONDOWN, NativeMethods.XBUTTON1, fromSelf: false));
        Assert.False(InputSuppression.ShouldSwallowMouseButton(NativeMethods.WM_XBUTTONUP, NativeMethods.XBUTTON1, fromSelf: false));

        InputSuppression.DetectionActive = true;
        Assert.True(InputSuppression.ShouldSwallowMouseButton(NativeMethods.WM_XBUTTONDOWN, NativeMethods.XBUTTON1, fromSelf: false));

        InputSuppression.DetectionActive = false;
        Assert.True(InputSuppression.ShouldSwallowMouseButton(NativeMethods.WM_XBUTTONUP, NativeMethods.XBUTTON1, fromSelf: false));
    }
}
