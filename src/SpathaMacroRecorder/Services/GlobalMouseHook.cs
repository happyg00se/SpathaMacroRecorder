using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SpathaMacroRecorder.Native;

namespace SpathaMacroRecorder.Services;

/// <summary>
/// Низкоуровневый хук мыши (WH_MOUSE_LL). Ловит только 5 стандартных кнопок и колесо —
/// доп. кнопки Spatha X приходят как клавиши через GlobalKeyboardHook (см. тех. пункт 1 задания).
/// Как и клавиатурный, живёт на собственном потоке (HookThread), а не на потоке интерфейса.
/// </summary>
internal sealed class GlobalMouseHook : IHostedService, IDisposable
{
    private readonly InputEventQueue _queue;
    private readonly Stopwatch _stopwatch;
    private readonly HookThread _hookThread;

    // Публичный по той же причине, что и в GlobalKeyboardHook — иначе DI не создаст сервис.
    public GlobalMouseHook(InputEventQueue queue, Stopwatch stopwatch, ILogger<GlobalMouseHook> logger)
    {
        _queue = queue;
        _stopwatch = stopwatch;
        _hookThread = new HookThread("Mouse hook", NativeMethods.WH_MOUSE_LL, HookCallback, logger);
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
        // Движение мыши отбрасывается ДО любой работы. Это самое частое событие в системе
        // (у игровой мыши ~1000 раз в секунду), а callback стоит в самом пути обработки ввода:
        // любая работа здесь тормозит курсор во всей системе. Ни запись (только клавиатура),
        // ни триггеры движение не используют.
        if (nCode >= 0 && (int)wParam != NativeMethods.WM_MOUSEMOVE)
        {
            long timestampMs = _stopwatch.ElapsedMilliseconds;
            var data = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);

            // Верхнее слово MouseData: знаковая дельта колеса для WM_MOUSEWHEEL/HWHEEL,
            // номер XBUTTON1/XBUTTON2 для WM_XBUTTONDOWN/UP. Интерпретация — у consumer'а.
            int highWord = unchecked((short)(data.MouseData >> 16));
            bool fromSelf = data.ExtraInfo == NativeMethods.SelfInputTag;

            _queue.Writer.TryWrite(new RawInputEvent(
                RawInputSource.Mouse,
                Message: (int)wParam,
                VirtualKey: 0,
                ScanCode: 0,
                ExtendedKey: false,
                MouseX: data.Pt.X,
                MouseY: data.Pt.Y,
                MouseData: highWord,
                TimestampMs: timestampMs,
                FromSelf: fromSelf,
                Injected: (data.Flags & NativeMethods.LLMHF_INJECTED) != 0));

            // Во время определения кнопки «вперёд/назад» не должны листать страницы в окне.
            if ((int)wParam is NativeMethods.WM_XBUTTONDOWN or NativeMethods.WM_XBUTTONUP
                && InputSuppression.ShouldSwallowMouseButton((int)wParam, highWord, fromSelf))
            {
                return (IntPtr)1;
            }
        }

        // Первый параметр современная Windows игнорирует.
        return NativeMethods.CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }

    public void Dispose() => _hookThread.Dispose();
}
