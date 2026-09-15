using Microsoft.Extensions.Logging.Abstractions;
using SpathaMacroRecorder.Models;
using SpathaMacroRecorder.Native;
using SpathaMacroRecorder.Services;

namespace SpathaMacroRecorder.Tests;

/// <summary>
/// Строка диагностики: помнит сигналы кнопок мыши и никогда — обычные клавиши.
/// </summary>
public class InputDiagnosticsTests : IDisposable
{
    private readonly InputDiagnostics _diagnostics =
        new(new InputEventDispatcher(new InputEventQueue()), NullLogger<InputDiagnostics>.Instance);

    public InputDiagnosticsTests()
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
    public void OrdinaryKeys_AreNeverRemembered()
    {
        _diagnostics.OnEventCaptured(Key(NativeMethods.WM_KEYDOWN, VirtualKeyCodes.W));

        Assert.Null(_diagnostics.LastPress);
    }

    [Fact]
    public void MouseButtonKey_IsRememberedWithItsMarker()
    {
        _diagnostics.OnEventCaptured(Key(NativeMethods.WM_KEYDOWN, VirtualKeyCodes.F17) with { Injected = true });

        var press = Assert.IsType<InputDiagnostics.Press>(_diagnostics.LastPress);
        Assert.Equal(VirtualKeyCodes.F17, press.VirtualKey);
        Assert.Equal("macro1", press.ButtonId);
        Assert.True(press.Injected);
        Assert.False(press.IsMouse);
    }

    [Fact]
    public void ExtraFunctionKeyWithoutMarker_IsStillShown()
    {
        // F13 ни к чему не привязана, но людьми не печатается — её приход важно увидеть.
        _diagnostics.OnEventCaptured(Key(NativeMethods.WM_KEYDOWN, VirtualKeyCodes.F13));

        var press = Assert.IsType<InputDiagnostics.Press>(_diagnostics.LastPress);
        Assert.Null(press.ButtonId);
    }

    [Fact]
    public void OwnPlaybackInput_IsIgnored()
    {
        _diagnostics.OnEventCaptured(Key(NativeMethods.WM_KEYDOWN, VirtualKeyCodes.F17) with { FromSelf = true });

        Assert.Null(_diagnostics.LastPress);
    }

    [Fact]
    public void AutoRepeat_KeepsTheFirstPress()
    {
        _diagnostics.OnEventCaptured(Key(NativeMethods.WM_KEYDOWN, VirtualKeyCodes.F17));
        var first = _diagnostics.LastPress;

        _diagnostics.OnEventCaptured(Key(NativeMethods.WM_KEYDOWN, VirtualKeyCodes.F17));

        Assert.Same(first, _diagnostics.LastPress);
    }

    [Fact]
    public void SecondPressAfterRelease_IsRecordedAgain()
    {
        _diagnostics.OnEventCaptured(Key(NativeMethods.WM_KEYDOWN, VirtualKeyCodes.F17));
        var first = _diagnostics.LastPress;

        _diagnostics.OnEventCaptured(Key(NativeMethods.WM_KEYUP, VirtualKeyCodes.F17));
        _diagnostics.OnEventCaptured(Key(NativeMethods.WM_KEYDOWN, VirtualKeyCodes.F17));

        Assert.NotSame(first, _diagnostics.LastPress);
    }

    [Fact]
    public void SideMouseButton_IsRemembered()
    {
        _diagnostics.OnEventCaptured(new RawInputEvent(
            RawInputSource.Mouse, NativeMethods.WM_XBUTTONDOWN, VirtualKey: 0, ScanCode: 0, ExtendedKey: false,
            MouseX: 0, MouseY: 0, MouseData: NativeMethods.XBUTTON2, TimestampMs: 0));

        var press = Assert.IsType<InputDiagnostics.Press>(_diagnostics.LastPress);
        Assert.True(press.IsMouse);
        Assert.Equal(2, press.VirtualKey);
        Assert.Equal("fwd", press.ButtonId);
    }

    [Fact]
    public void ReportTrigger_StoresWhatHappened()
    {
        _diagnostics.ReportTrigger("macro1", "Стратагема");

        var outcome = Assert.IsType<InputDiagnostics.Outcome>(_diagnostics.LastOutcome);
        Assert.Equal("macro1", outcome.ButtonId);
        Assert.Equal("Стратагема", outcome.MacroName);
    }

    private static RawInputEvent Key(int message, int virtualKey) =>
        new(RawInputSource.Keyboard, message, virtualKey, ScanCode: 0, ExtendedKey: false,
            MouseX: 0, MouseY: 0, MouseData: 0, TimestampMs: 0);
}
