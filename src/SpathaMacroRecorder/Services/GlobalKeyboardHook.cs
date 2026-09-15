using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SpathaMacroRecorder.Native;

namespace SpathaMacroRecorder.Services;

/// <summary>
/// Низкоуровневый клавиатурный хук (WH_KEYBOARD_LL). Живёт всё время работы приложения на
/// собственном потоке (HookThread), а не на потоке интерфейса: занятый интерфейс иначе
/// заставлял Windows молча снимать хук, и после этого не работали ни запись, ни триггеры.
/// </summary>
internal sealed class GlobalKeyboardHook : IHostedService, IDisposable
{
    private readonly InputEventQueue _queue;
    private readonly Stopwatch _stopwatch;
    private readonly HookThread _hookThread;

    /// <summary>
    /// Virtual-key аварийной остановки; 0 — не задана. volatile, потому что записывается из
    /// UI-потока (окно настроек), а читается в callback'е хука.
    /// </summary>
    internal volatile int PanicVirtualKey;

    /// <summary>
    /// Что делать по panic-клавише. Вызывается прямо в callback'е, на потоке хука, поэтому
    /// обязано быть мгновенным и потокобезопасным — у плеера это просто Cancel() токена (тех. пункт 6).
    /// </summary>
    internal Action? PanicRequested;

    /// <summary>Клавиша паузы/продолжения записи; 0 — не задана.</summary>
    internal volatile int PauseRecordingVirtualKey;

    /// <summary>Вызывается на потоке хука — обработчик сам уходит в UI-поток.</summary>
    internal Action? PauseRecordingRequested;

    // Публичный, а не internal: контейнер внедрения зависимостей видит только публичные
    // конструкторы — с internal он не может создать сервис и падает при старте приложения.
    public GlobalKeyboardHook(InputEventQueue queue, Stopwatch stopwatch, ILogger<GlobalKeyboardHook> logger)
    {
        _queue = queue;
        _stopwatch = stopwatch;
        _hookThread = new HookThread("Keyboard hook", NativeMethods.WH_KEYBOARD_LL, HookCallback, logger);
        AppDomain.CurrentDomain.ProcessExit += (_, _) => _hookThread.Stop();
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _hookThread.Start();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _hookThread.Stop();
        return Task.CompletedTask;
    }

    /// <summary>Поставить хук заново — страховка на случай, если Windows его всё-таки сняла.</summary>
    internal void Reinstall() => _hookThread.Restart();

    internal bool IsInstalled => _hookThread.IsInstalled;

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            long timestampMs = _stopwatch.ElapsedMilliseconds;
            var data = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);

            // Panic-клавиша проверяется ПЕРВОЙ, до постановки события в очередь: у хуков нет
            // приоритетов, и единственный способ отреагировать мгновенно — сделать это здесь.
            bool isDown = (int)wParam is NativeMethods.WM_KEYDOWN or NativeMethods.WM_SYSKEYDOWN;
            int vk = (int)data.VkCode;

            int panicKey = PanicVirtualKey;
            if (panicKey != 0 && vk == panicKey && isDown)
            {
                PanicRequested?.Invoke();
            }

            int pauseKey = PauseRecordingVirtualKey;
            if (pauseKey != 0 && vk == pauseKey && isDown)
            {
                PauseRecordingRequested?.Invoke();
            }

            bool extended = (data.Flags & NativeMethods.LLKHF_EXTENDED) != 0;

            // Свой ввод плеер помечает в dwExtraInfo. Чужой синтетический ввод (например, от
            // G-Helper или сервиса Armoury Crate) не отбрасывается: кнопки мыши приходят именно так.
            bool fromSelf = data.ExtraInfo == NativeMethods.SelfInputTag;

            _queue.Writer.TryWrite(new RawInputEvent(
                RawInputSource.Keyboard,
                Message: (int)wParam,
                VirtualKey: vk,
                ScanCode: (ushort)data.ScanCode,
                ExtendedKey: extended,
                MouseX: 0,
                MouseY: 0,
                MouseData: 0,
                TimestampMs: timestampMs,
                FromSelf: fromSelf,
                Injected: (data.Flags & NativeMethods.LLKHF_INJECTED) != 0));

            // Событие уже в очереди — триггеры и определение его получат. Дальше по цепочке
            // клавиша кнопки мыши не идёт: ни Windows, ни игре она не предназначена.
            bool isUp = (int)wParam is NativeMethods.WM_KEYUP or NativeMethods.WM_SYSKEYUP;
            if (InputSuppression.ShouldSwallowKey(vk, (ushort)data.ScanCode, extended, isUp, fromSelf))
            {
                return (IntPtr)1;
            }
        }

        // Первый параметр современная Windows игнорирует.
        return NativeMethods.CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }

    public void Dispose() => _hookThread.Dispose();
}
