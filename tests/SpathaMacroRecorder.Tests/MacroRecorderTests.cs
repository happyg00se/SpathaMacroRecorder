using SpathaMacroRecorder.Models;
using SpathaMacroRecorder.Native;
using SpathaMacroRecorder.Services;

namespace SpathaMacroRecorder.Tests;

// Значения vk/scan — из примера в самом задании (раздел "Модель данных"): комбо Ctrl+W.
public class MacroRecorderTests
{
    private const int VkControl = 0x11;
    private const ushort ScanControl = 0x1D;
    private const int VkW = 0x57;
    private const ushort ScanW = 0x11;

    // Задержки при записи не замеряются: паузы между нажатиями — это скорость печати
    // человека, в макросе она не нужна. Каждый шаг получает 0, при необходимости значение
    // проставляют вручную в таблице шагов.
    [Fact]
    public void Feed_RecordsCtrlWComboWithZeroDelays()
    {
        var recorder = new MacroRecorder();
        recorder.Start();

        recorder.Feed(KeyEvent(NativeMethods.WM_KEYDOWN, VkControl, ScanControl, timestampMs: 0));
        recorder.Feed(KeyEvent(NativeMethods.WM_KEYDOWN, VkW, ScanW, timestampMs: 40));
        recorder.Feed(KeyEvent(NativeMethods.WM_KEYUP, VkW, ScanW, timestampMs: 85));
        recorder.Feed(KeyEvent(NativeMethods.WM_KEYUP, VkControl, ScanControl, timestampMs: 115));

        var steps = recorder.Stop();

        Assert.Equal(4, steps.Count);
        AssertKeyStep(steps[0], KeyAction.Down, VkControl, ScanControl, delayBeforeMs: 0);
        AssertKeyStep(steps[1], KeyAction.Down, VkW, ScanW, delayBeforeMs: 0);
        AssertKeyStep(steps[2], KeyAction.Up, VkW, ScanW, delayBeforeMs: 0);
        AssertKeyStep(steps[3], KeyAction.Up, VkControl, ScanControl, delayBeforeMs: 0);
    }

    [Fact]
    public void Feed_IgnoresAutoRepeatKeyDownWithoutIntermediateUp()
    {
        var recorder = new MacroRecorder();
        recorder.Start();

        recorder.Feed(KeyEvent(NativeMethods.WM_KEYDOWN, VkW, ScanW, timestampMs: 0));
        recorder.Feed(KeyEvent(NativeMethods.WM_KEYDOWN, VkW, ScanW, timestampMs: 30)); // auto-repeat
        recorder.Feed(KeyEvent(NativeMethods.WM_KEYDOWN, VkW, ScanW, timestampMs: 60)); // auto-repeat
        recorder.Feed(KeyEvent(NativeMethods.WM_KEYUP, VkW, ScanW, timestampMs: 200));

        var steps = recorder.Stop();

        Assert.Equal(2, steps.Count);
        AssertKeyStep(steps[0], KeyAction.Down, VkW, ScanW, delayBeforeMs: 0);
        AssertKeyStep(steps[1], KeyAction.Up, VkW, ScanW, delayBeforeMs: 0);
    }

    [Fact]
    public void Feed_AllowsSecondDownAfterInterveningUp()
    {
        var recorder = new MacroRecorder();
        recorder.Start();

        recorder.Feed(KeyEvent(NativeMethods.WM_KEYDOWN, VkW, ScanW, timestampMs: 0));
        recorder.Feed(KeyEvent(NativeMethods.WM_KEYUP, VkW, ScanW, timestampMs: 50));
        recorder.Feed(KeyEvent(NativeMethods.WM_KEYDOWN, VkW, ScanW, timestampMs: 100));
        recorder.Feed(KeyEvent(NativeMethods.WM_KEYUP, VkW, ScanW, timestampMs: 150));

        var steps = recorder.Stop();

        Assert.Equal(4, steps.Count);
    }

    [Fact]
    public void Feed_IgnoredWhenNotRecording()
    {
        var recorder = new MacroRecorder();

        recorder.Feed(KeyEvent(NativeMethods.WM_KEYDOWN, VkW, ScanW, timestampMs: 0));

        var steps = recorder.Stop();

        Assert.Empty(steps);
    }

    [Fact]
    public void Feed_IgnoredWhilePaused()
    {
        var recorder = new MacroRecorder();
        recorder.Start();

        recorder.Feed(KeyEvent(NativeMethods.WM_KEYDOWN, VkControl, ScanControl, timestampMs: 0));
        recorder.Pause();
        recorder.Feed(KeyEvent(NativeMethods.WM_KEYDOWN, VkW, ScanW, timestampMs: 40));
        recorder.Resume();
        recorder.Feed(KeyEvent(NativeMethods.WM_KEYUP, VkControl, ScanControl, timestampMs: 80));

        var steps = recorder.Stop();

        Assert.Equal(2, steps.Count);
    }

    [Fact]
    public void Feed_IgnoresAllMouseEvents()
    {
        var recorder = new MacroRecorder();
        recorder.Start();

        recorder.Feed(KeyEvent(NativeMethods.WM_KEYDOWN, VkW, ScanW, timestampMs: 0));
        recorder.Feed(MouseEvent(NativeMethods.WM_MOUSEMOVE, timestampMs: 10));
        recorder.Feed(MouseEvent(NativeMethods.WM_LBUTTONDOWN, timestampMs: 20));
        recorder.Feed(MouseEvent(NativeMethods.WM_LBUTTONUP, timestampMs: 30));
        recorder.Feed(MouseEvent(NativeMethods.WM_MOUSEWHEEL, timestampMs: 40));
        recorder.Feed(KeyEvent(NativeMethods.WM_KEYUP, VkW, ScanW, timestampMs: 50));

        var steps = recorder.Stop();

        // Остались только две клавиатурные строки.
        Assert.Equal(2, steps.Count);
        Assert.All(steps, step => Assert.IsType<KeyStep>(step));
        AssertKeyStep(steps[1], KeyAction.Up, VkW, ScanW, delayBeforeMs: 0);
    }

    private static RawInputEvent MouseEvent(int message, long timestampMs) =>
        new(RawInputSource.Mouse, message, VirtualKey: 0, ScanCode: 0, ExtendedKey: false,
            MouseX: 100, MouseY: 100, MouseData: 120, timestampMs);

    private static RawInputEvent KeyEvent(int message, int virtualKey, ushort scanCode, long timestampMs) =>
        new(RawInputSource.Keyboard, message, virtualKey, scanCode, ExtendedKey: false, MouseX: 0, MouseY: 0, MouseData: 0, timestampMs);

    private static void AssertKeyStep(MacroStep step, KeyAction action, int virtualKey, ushort scanCode, int delayBeforeMs)
    {
        var keyStep = Assert.IsType<KeyStep>(step);
        Assert.Equal(action, keyStep.Action);
        Assert.Equal(virtualKey, keyStep.VirtualKey);
        Assert.Equal(scanCode, keyStep.ScanCode);
        Assert.Equal(delayBeforeMs, keyStep.DelayBeforeMs);
    }
}
