using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SpathaMacroRecorder.Services;

/// <summary>
/// Следит за процессом игры, запущенной вместе с программой (см. GameLaunch), опросом списка
/// процессов раз в две секунды.
///
/// Только перечисление процессов по имени — ни чтения чужой памяти, ни инъекций, ни хуков в игру
/// (ограничение задания). Опрос выбран вместо подписки на события WMI намеренно: подписка требует
/// прав администратора, а раз в две секунды стоит буквально ничего.
/// </summary>
internal sealed class GameWatcher : IHostedService, IDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Сколько опросов подряд игры не должно быть, чтобы считать её закрытой. При запуске игра
    /// может перезапустить собственный процесс (так делают защиты от читов), и без этого запаса
    /// программа закрылась бы на ровном месте, пока игра ещё грузится.
    /// </summary>
    internal const int MissesBeforeStopped = 3;

    private readonly ILogger<GameWatcher> _logger;
    private readonly CancellationTokenSource _stopping = new();

    private Task? _loop;
    private volatile string? _processName;

    // Опросы подряд, на которых игры не было. Только поток опроса.
    private int _misses;

    public GameWatcher(ILogger<GameWatcher> logger)
    {
        _logger = logger;
    }

    /// <summary>Имя процесса игры, за которой следим; null — ни за чем не следим.</summary>
    internal string? ProcessName => _processName;

    /// <summary>Запущена ли игра. Пока ни за чем не следим — всегда false.</summary>
    public bool IsGameRunning { get; private set; }

    /// <summary>Игра появилась. Приходит из фонового потока.</summary>
    internal event Action? GameStarted;

    /// <summary>Игра закрылась. Приходит из фонового потока.</summary>
    internal event Action? GameStopped;

    /// <summary>Есть ли сейчас процесс с таким именем. Подменяется в тестах.</summary>
    internal Func<string, bool> ProcessExists { get; set; } = DefaultProcessExists;

    /// <summary>Начать следить за игрой с этим именем процесса.</summary>
    internal void Watch(string processName)
    {
        string name = NormalizeProcessName(processName);
        _misses = 0;
        IsGameRunning = name.Length > 0 && ProcessExists(name);
        _processName = name.Length > 0 ? name : null;
        _logger.LogInformation("Watching game process {Process} (running: {Running})", name, IsGameRunning);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _loop = Task.Run(() => PollLoopAsync(_stopping.Token), CancellationToken.None);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _stopping.CancelAsync().ConfigureAwait(false);

        if (_loop is not null)
        {
            try
            {
                await _loop.WaitAsync(TimeSpan.FromSeconds(3), cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is OperationCanceledException or TimeoutException)
            {
                // Опрос не успел завершиться — на выходе из программы это уже не важно.
            }
        }
    }

    /// <summary>Один опрос. Вынесен отдельно, чтобы «когда игра считается закрытой» проверялось тестами.</summary>
    internal void Poll()
    {
        if (_processName is not { } name)
        {
            return;
        }

        if (ProcessExists(name))
        {
            _misses = 0;
            if (!IsGameRunning)
            {
                IsGameRunning = true;
                _logger.LogInformation("Game {Process} is running", name);
                GameStarted?.Invoke();
            }

            return;
        }

        // Игру ещё ни разу не видели — закрываться не из-за чего.
        if (!IsGameRunning || ++_misses < MissesBeforeStopped)
        {
            return;
        }

        IsGameRunning = false;
        _misses = 0;
        _logger.LogInformation("Game {Process} has closed", name);
        GameStopped?.Invoke();
    }

    private async Task PollLoopAsync(CancellationToken token)
    {
        using var timer = new PeriodicTimer(PollInterval);

        while (await SafeWaitAsync(timer, token).ConfigureAwait(false))
        {
            try
            {
                Poll();
            }
            catch (Exception ex)
            {
                // Обработчики живут в UI — их падение не должно останавливать слежение.
                _logger.LogError(ex, "Game state handler failed");
            }
        }
    }

    private static async Task<bool> SafeWaitAsync(PeriodicTimer timer, CancellationToken token)
    {
        try
        {
            return await timer.WaitForNextTickAsync(token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    /// <summary>«helldivers2», «helldivers2.exe», с пробелами по краям — всё равно.</summary>
    internal static string NormalizeProcessName(string? name)
    {
        string trimmed = (name ?? string.Empty).Trim();
        return trimmed.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? trimmed[..^4]
            : trimmed;
    }

    private static bool DefaultProcessExists(string processName)
    {
        try
        {
            var found = Process.GetProcessesByName(processName);
            foreach (var process in found)
            {
                process.Dispose();
            }

            return found.Length > 0;
        }
        catch (InvalidOperationException)
        {
            // Список процессов не прочитался — считаем, что игра идёт: закрыть программу посреди
            // игры хуже, чем подержать её открытой лишние две секунды.
            return true;
        }
    }

    public void Dispose() => _stopping.Dispose();
}
