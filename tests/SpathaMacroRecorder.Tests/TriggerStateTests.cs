using SpathaMacroRecorder.Models;
using SpathaMacroRecorder.Native;
using SpathaMacroRecorder.Services;

namespace SpathaMacroRecorder.Tests;

/// <summary>
/// Поведение триггеров во времени: повтор зажатой клавиши, отпускание сочетания, собственный
/// ввод плеера. Именно здесь макрос раньше не доигрывал в игре.
/// </summary>
public class TriggerStateTests : IDisposable
{
    private readonly List<string> _pressed = [];
    private readonly List<string> _released = [];
    private readonly TriggerBindingService _service;

    public TriggerStateTests()
    {
        InputSuppression.Reset();
        MouseButtonCatalog.ApplyCodeOverrides(null);

        _service = new TriggerBindingService(new InputEventDispatcher(new InputEventQueue()));
        _service.TriggerPressed += _pressed.Add;
        _service.TriggerReleased += _released.Add;
    }

    public void Dispose()
    {
        InputSuppression.Reset();
        MouseButtonCatalog.ApplyCodeOverrides(null);
    }

    [Fact]
    public void HeldButton_AutoRepeatDoesNotRestartTheMacro()
    {
        // Зажатую клавишу Windows повторяет — каждый повтор раньше начинал макрос заново.
        _service.OnEventCaptured(Key(NativeMethods.WM_KEYDOWN, VirtualKeyCodes.F17));
        _service.OnEventCaptured(Key(NativeMethods.WM_KEYDOWN, VirtualKeyCodes.F17));
        _service.OnEventCaptured(Key(NativeMethods.WM_KEYDOWN, VirtualKeyCodes.F17));

        Assert.Equal(new[] { "macro1" }, _pressed);
    }

    [Fact]
    public void SecondPress_AfterRelease_StartsTheMacroAgain()
    {
        _service.OnEventCaptured(Key(NativeMethods.WM_KEYDOWN, VirtualKeyCodes.F17));
        _service.OnEventCaptured(Key(NativeMethods.WM_KEYUP, VirtualKeyCodes.F17));
        _service.OnEventCaptured(Key(NativeMethods.WM_KEYDOWN, VirtualKeyCodes.F17));

        Assert.Equal(new[] { "macro1", "macro1" }, _pressed);
        Assert.Equal(new[] { "macro1" }, _released);
    }

    [Fact]
    public void SideButton_FiresWhileCtrlIsHeld()
    {
        // Стратагема в Helldivers 2 вводится с зажатым Ctrl.
        InputSuppression.ModifierState = static () => (true, false, false);

        _service.OnEventCaptured(Key(NativeMethods.WM_KEYDOWN, VirtualKeyCodes.F17));

        Assert.Equal(new[] { "macro1" }, _pressed);
    }

    [Fact]
    public void CombinationRelease_IsReportedAfterModifiersWereLetGo()
    {
        MouseButtonCatalog.ApplyCodeOverrides(new Dictionary<string, string>
        {
            ["macro3"] = new MouseButtonCatalog.KeyCode(VirtualKeyCodes.D1, Ctrl: true, Alt: true, Shift: false).Encode(),
        });

        InputSuppression.ModifierState = static () => (true, true, false);
        _service.OnEventCaptured(Key(NativeMethods.WM_KEYDOWN, VirtualKeyCodes.D1));

        // Режим «пока зажата» гаснет только по отпусканию — без него макрос крутился бы вечно.
        InputSuppression.ModifierState = static () => (false, false, false);
        _service.OnEventCaptured(Key(NativeMethods.WM_KEYUP, VirtualKeyCodes.D1));

        Assert.Equal(new[] { "macro3" }, _pressed);
        Assert.Equal(new[] { "macro3" }, _released);
    }

    [Fact]
    public void OwnPlaybackInput_NeverTriggersAMacro()
    {
        _service.OnEventCaptured(Key(NativeMethods.WM_KEYDOWN, VirtualKeyCodes.F17) with { FromSelf = true });

        Assert.Empty(_pressed);
    }

    [Fact]
    public void SuspendedService_StillReleasesWhatWasPressedBefore()
    {
        _service.OnEventCaptured(Key(NativeMethods.WM_KEYDOWN, VirtualKeyCodes.F17));

        _service.Suspended = true;
        _service.OnEventCaptured(Key(NativeMethods.WM_KEYUP, VirtualKeyCodes.F17));

        Assert.Equal(new[] { "macro1" }, _released);
    }

    private static RawInputEvent Key(int message, int virtualKey) =>
        new(RawInputSource.Keyboard, message, virtualKey, ScanCode: 0, ExtendedKey: false,
            MouseX: 0, MouseY: 0, MouseData: 0, TimestampMs: 0);
}
