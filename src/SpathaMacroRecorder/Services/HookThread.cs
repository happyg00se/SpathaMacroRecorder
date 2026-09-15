using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using SpathaMacroRecorder.Native;

namespace SpathaMacroRecorder.Services;

/// <summary>
/// Отдельный поток с собственным циклом сообщений под один низкоуровневый хук.
///
/// Раньше хуки жили на потоке интерфейса. Windows вызывает callback низкоуровневого хука через
/// очередь сообщений потока, который его поставил, и ждёт ответа не дольше LowLevelHooksTimeout.
/// Стоило интерфейсу занять поток дольше — открыть окно, загрузить картинку, подождать диск при
/// запуске вместе с Windows, — система молча снимала хук и больше его не вызывала: программа
/// выглядела живой, а кнопки мыши переставали работать до её перезапуска. Этот поток занят только
/// хуком, и ничто в интерфейсе его не задерживает.
/// </summary>
internal sealed class HookThread : IDisposable
{
    private readonly string _name;
    private readonly int _hookId;

    // Делегат хранится в поле: если передать его в SetWindowsHookEx без сохранённой ссылки,
    // GC может собрать его и хук развалится в рантайме (тех. пункт 7 задания).
    private readonly LowLevelHookProc _proc;

    private readonly ILogger _logger;
    private readonly object _lock = new();

    private Thread? _thread;
    private uint _threadId;
    private volatile bool _installed;

    public HookThread(string name, int hookId, LowLevelHookProc proc, ILogger logger)
    {
        _name = name;
        _hookId = hookId;
        _proc = proc;
        _logger = logger;
    }

    /// <summary>
    /// Поставлен ли хук. Если Windows сняла его сама, узнать об этом нельзя, поэтому для
    /// строки состояния это «включён», а не «работает».
    /// </summary>
    internal bool IsInstalled => _installed;

    internal void Start()
    {
        lock (_lock)
        {
            if (_thread is not null)
            {
                return;
            }

            var ready = new ManualResetEventSlim();
            var thread = new Thread(() => Run(ready))
            {
                IsBackground = true,
                Name = _name,

                // Этот поток ждёт ввод всей системы — он не должен уступать фоновой работе.
                Priority = ThreadPriority.Highest,
            };

            thread.Start();

            // Дожидаемся установки, чтобы Stop, вызванный сразу следом, знал, кому слать WM_QUIT.
            ready.Wait(TimeSpan.FromSeconds(5));
            _thread = thread;
        }
    }

    internal void Stop()
    {
        Thread? thread;
        uint threadId;

        lock (_lock)
        {
            thread = _thread;
            threadId = _threadId;
            _thread = null;
        }

        if (thread is null)
        {
            return;
        }

        NativeMethods.PostThreadMessageW(threadId, NativeMethods.WM_QUIT, IntPtr.Zero, IntPtr.Zero);
        thread.Join(TimeSpan.FromSeconds(1));
    }

    /// <summary>
    /// Снять и поставить хук заново. Узнать, что Windows сняла хук, нельзя никак — поэтому
    /// в важный момент (старт игры) его просто ставят ещё раз.
    /// </summary>
    internal void Restart()
    {
        Stop();
        Start();
    }

    private void Run(ManualResetEventSlim ready)
    {
        _threadId = NativeMethods.GetCurrentThreadId();

        // Для низкоуровневых хуков описатель модуля не нужен (процедура живёт в этом же процессе),
        // поэтому IntPtr.Zero корректен.
        IntPtr handle = NativeMethods.SetWindowsHookExW(_hookId, _proc, IntPtr.Zero, 0);
        int error = Marshal.GetLastWin32Error();
        ready.Set();

        if (handle == IntPtr.Zero)
        {
            // Не бросаем исключение: ошибка установки хука не должна мешать окну открыться.
            _logger.LogError(
                "Failed to install {Hook} (error code {Error}). Recording and triggers will not work.",
                _name,
                error);
            return;
        }

        _installed = true;
        _logger.LogInformation("{Hook} installed", _name);

        // Callback'и хука Windows доставляет изнутри GetMessage. Самих сообщений этому потоку
        // никто не шлёт, кроме WM_QUIT при остановке.
        while (NativeMethods.GetMessageW(out _, IntPtr.Zero, 0, 0) > 0)
        {
        }

        _installed = false;
        NativeMethods.UnhookWindowsHookEx(handle);
    }

    public void Dispose() => Stop();
}
