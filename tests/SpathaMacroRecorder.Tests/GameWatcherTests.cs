using Microsoft.Extensions.Logging.Abstractions;
using SpathaMacroRecorder.Services;

namespace SpathaMacroRecorder.Tests;

/// <summary>
/// Когда игра считается закрытой — а значит, когда закрывается и программа.
/// </summary>
public class GameWatcherTests
{
    private readonly GameWatcher _watcher = new(NullLogger<GameWatcher>.Instance);
    private bool _gameRunning;
    private int _started;
    private int _stopped;

    public GameWatcherTests()
    {
        _watcher.ProcessExists = _ => _gameRunning;
        _watcher.GameStarted += () => _started++;
        _watcher.GameStopped += () => _stopped++;
    }

    [Fact]
    public void ClosedGame_IsReportedOnlyAfterSeveralMissedPolls()
    {
        _gameRunning = true;
        _watcher.Watch("helldivers2");
        Assert.True(_watcher.IsGameRunning);

        _gameRunning = false;
        for (int i = 0; i < GameWatcher.MissesBeforeStopped - 1; i++)
        {
            _watcher.Poll();
        }

        Assert.Equal(0, _stopped);

        _watcher.Poll();

        Assert.Equal(1, _stopped);
        Assert.False(_watcher.IsGameRunning);
    }

    [Fact]
    public void BriefRestartOfTheGameProcess_DoesNotCloseTheProgram()
    {
        _gameRunning = true;
        _watcher.Watch("helldivers2");

        _gameRunning = false;
        _watcher.Poll();
        _gameRunning = true;
        _watcher.Poll();
        _gameRunning = false;
        for (int i = 0; i < GameWatcher.MissesBeforeStopped - 1; i++)
        {
            _watcher.Poll();
        }

        Assert.Equal(0, _stopped);
    }

    [Fact]
    public void GameThatNeverAppeared_NeverClosesTheProgram()
    {
        _watcher.Watch("helldivers2");

        for (int i = 0; i < 10; i++)
        {
            _watcher.Poll();
        }

        Assert.Equal(0, _started);
        Assert.Equal(0, _stopped);
    }

    [Fact]
    public void GameAppearingLater_IsReportedAsStarted()
    {
        _watcher.Watch("helldivers2");

        _gameRunning = true;
        _watcher.Poll();

        Assert.Equal(1, _started);
        Assert.True(_watcher.IsGameRunning);
    }

    [Fact]
    public void WithoutWatch_NothingIsReported()
    {
        _gameRunning = true;
        _watcher.Poll();

        Assert.Equal(0, _started);
        Assert.Null(_watcher.ProcessName);
    }

    [Fact]
    public void Watch_AcceptsTheExeSuffix()
    {
        _watcher.Watch("helldivers2.exe");

        Assert.Equal("helldivers2", _watcher.ProcessName);
    }
}
