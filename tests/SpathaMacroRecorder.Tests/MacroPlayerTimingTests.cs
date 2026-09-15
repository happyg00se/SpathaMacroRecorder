using SpathaMacroRecorder.Models;
using SpathaMacroRecorder.Native;
using SpathaMacroRecorder.Services;

namespace SpathaMacroRecorder.Tests;

/// <summary>
/// Пауза между нажатиями при воспроизведении. Макрос из нулевых задержек уходил в игру за
/// микросекунды, и Helldivers 2 не видела ни одного нажатия.
/// </summary>
public class MacroPlayerTimingTests
{
    [Theory]
    [InlineData(0, 30, 30)]
    [InlineData(10, 30, 30)]
    [InlineData(80, 30, 80)]
    [InlineData(0, 0, 0)]
    [InlineData(0, -5, 0)]
    public void EffectiveDelay_NeverDropsBelowTheKeyGap(int stepDelayMs, int gapMs, int expectedMs) =>
        Assert.Equal(expectedMs, MacroPlayer.EffectiveDelay(stepDelayMs, speedMultiplier: 1.0, jitterMs: 0, minGapMs: gapMs));

    [Fact]
    public void EffectiveDelay_SpeedUpDoesNotBeatTheGap() =>
        Assert.Equal(30, MacroPlayer.EffectiveDelay(40, speedMultiplier: 4.0, jitterMs: 0, minGapMs: 30));

    [Fact]
    public void EffectiveDelay_JitterNeverPushesBelowTheGap()
    {
        for (int i = 0; i < 500; i++)
        {
            Assert.True(MacroPlayer.EffectiveDelay(0, speedMultiplier: 1.0, jitterMs: 50, minGapMs: 30) >= 30);
        }
    }

    [Fact]
    public void GapFor_AppliesToKeyPresses()
    {
        var step = new KeyStep
        {
            DelayBeforeMs = 0,
            Action = KeyAction.Down,
            VirtualKey = VirtualKeyCodes.W,
            ScanCode = 0x11,
            Extended = false,
        };

        Assert.Equal(30, MacroPlayer.GapFor(step, 30));
    }

    [Fact]
    public void GapFor_DoesNotStretchPauseSteps() =>
        Assert.Equal(0, MacroPlayer.GapFor(new PauseStep { DelayBeforeMs = 0 }, 30));
}
