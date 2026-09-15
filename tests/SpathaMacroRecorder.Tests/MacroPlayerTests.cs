using SpathaMacroRecorder.Services;

namespace SpathaMacroRecorder.Tests;

// SendInput и реальное воспроизведение (Thread, cancellation, release-all) требуют user32.dll —
// проверяются только вручную на Windows. Здесь — чистая математика тайминга, которую можно
// проверить без обращений к ОС.
public class MacroPlayerTests
{
    [Theory]
    [InlineData(100, 1.0, 100)]
    [InlineData(100, 2.0, 50)]
    [InlineData(100, 0.5, 200)]
    [InlineData(40, 2.0, 20)]
    public void ApplySpeed_ScalesDelayInverselyToMultiplier(int delayMs, double speedMultiplier, int expected)
    {
        int actual = MacroPlayer.ApplySpeed(delayMs, speedMultiplier);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ApplyJitter_ReturnsUnchangedWhenJitterIsZero()
    {
        int actual = MacroPlayer.ApplyJitter(50, 0);

        Assert.Equal(50, actual);
    }

    [Theory]
    [InlineData(50, 10)]
    [InlineData(0, 10)]
    [InlineData(5, 50)]
    public void ApplyJitter_StaysWithinBoundsAndNeverNegative(int delayMs, int jitterMs)
    {
        for (int i = 0; i < 200; i++)
        {
            int actual = MacroPlayer.ApplyJitter(delayMs, jitterMs);

            Assert.InRange(actual, Math.Max(0, delayMs - jitterMs), delayMs + jitterMs);
            Assert.True(actual >= 0);
        }
    }
}
