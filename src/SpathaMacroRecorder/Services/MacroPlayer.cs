using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using SpathaMacroRecorder.Models;
using SpathaMacroRecorder.Native;

namespace SpathaMacroRecorder.Services;

/// <summary>
/// Воспроизводит макрос через SendInput на отдельном потоке. Singleton: одновременно активно
/// только одно воспроизведение — Start() сначала останавливает предыдущее (см. функциональное
/// требование "новый триггер прерывает предыдущий").
/// </summary>
internal sealed class MacroPlayer(ILogger<MacroPlayer> logger)
{
    private const int MinimumHoldCycleMs = 5;

    private readonly object _lock = new();

    /// <summary>
    /// Что зажато одним конкретным проигрыванием. Раньше это были общие поля класса, и при
    /// быстрой смене макросов старый поток в finally очищал их уже после того, как новый
    /// записал туда свои клавиши, — те не отпускались никогда. Состояние на проигрывание
    /// снимает гонку по определению.
    /// </summary>
    private sealed class HeldInput
    {
        internal Dictionary<int, (ushort ScanCode, bool Extended)> Keys { get; } = [];
        internal HashSet<MouseButtonKind> Buttons { get; } = [];
    }

    private CancellationTokenSource? _currentCts;
    private Thread? _currentThread;
    private volatile bool _isPlaying;

    internal bool IsPlaying => _isPlaying;

    internal event Action? PlaybackCompleted;

    /// <param name="useAbsoluteMovement">
    /// «Режим рабочего стола» (тех. пункт 3): абсолютное позиционирование вместо относительных
    /// дельт. Для игр должен быть false — относительное движение читают через DirectInput/Raw Input.
    /// </param>
    /// <param name="minKeyGapMs">
    /// Наименьшая пауза перед нажатием и отпусканием (настройка «Пауза между нажатиями»).
    /// Без неё макрос из нулевых задержек уходит в игру за микросекунды, и игра его не видит.
    /// </param>
    internal void Start(Macro macro, bool useAbsoluteMovement = false, int minKeyGapMs = 0)
    {
        Stop();

        // Ждём завершения предыдущего проигрывания: иначе два потока какое-то время шлют
        // ввод одновременно. Отмена срабатывает мгновенно (ожидание в PreciseDelay просыпается
        // по токену), так что ждать почти нечего. Таймаут короткий намеренно: Start вызывается
        // из UI-потока, на котором живут хуки, и долгое ожидание здесь заморозило бы ввод
        // во всей системе.
        Thread? previous;
        lock (_lock)
        {
            previous = _currentThread;
        }

        previous?.Join(TimeSpan.FromMilliseconds(250));

        // Шаги и параметры снимаем снимком: коллекция связана с интерфейсом, и правка
        // в редакторе во время проигрывания иначе роняет перебор.
        var steps = macro.Steps.ToArray();
        if (steps.Length == 0)
        {
            return;
        }

        var plan = new PlaybackPlan(
            steps,
            macro.PlaybackMode,
            Math.Max(macro.RepeatCount, 1),
            macro.SpeedMultiplier <= 0 ? 1.0 : macro.SpeedMultiplier,
            macro.JitterMs,
            useAbsoluteMovement,
            Math.Max(minKeyGapMs, 0));

        var cts = new CancellationTokenSource();
        var thread = new Thread(() => RunPlayback(plan, cts))
        {
            IsBackground = true,
            Priority = ThreadPriority.AboveNormal,
            Name = $"MacroPlayer:{macro.Name}",
        };

        lock (_lock)
        {
            _currentCts = cts;
            _currentThread = thread;
        }

        thread.Start();
    }

    /// <summary>Снимок макроса на момент запуска — дальше модель может меняться свободно.</summary>
    private sealed record PlaybackPlan(
        MacroStep[] Steps,
        PlaybackMode Mode,
        int RepeatCount,
        double Speed,
        int JitterMs,
        bool UseAbsoluteMovement,
        int MinGapMs);

    /// <summary>
    /// Останавливает текущее воспроизведение (если есть) и немедленно возвращает управление —
    /// не ждёт завершения потока. Может вызываться из callback'а хука (panic-key), где
    /// блокирующее ожидание недопустимо (тех. пункт 4).
    /// </summary>
    internal void Stop()
    {
        CancellationTokenSource? cts;
        lock (_lock)
        {
            cts = _currentCts;
        }

        cts?.Cancel();
    }

    private void RunPlayback(PlaybackPlan plan, CancellationTokenSource cts)
    {
        var held = new HeldInput();
        _isPlaying = true;
        try
        {
            RunPlaybackMode(plan, held, cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Штатная остановка (panic-key, новый триггер, отпускание в режиме hold) — не ошибка.
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error while playing a macro");
        }
        finally
        {
            // Отпускаем только то, что зажали мы сами в этом проигрывании.
            ReleaseAllHeld(held);
            _isPlaying = false;

            lock (_lock)
            {
                // Если Start() уже запустил следующее воспроизведение, пока это заканчивалось,
                // не затираем его состояние состоянием более старой попытки.
                if (ReferenceEquals(_currentCts, cts))
                {
                    _currentCts = null;
                    _currentThread = null;
                }
            }

            cts.Dispose();
            PlaybackCompleted?.Invoke();
        }
    }

    private void RunPlaybackMode(PlaybackPlan plan, HeldInput held, CancellationToken token)
    {
        switch (plan.Mode)
        {
            case PlaybackMode.Once:
                PlayOnce(plan, held, token);
                break;

            case PlaybackMode.Repeat:
                for (int i = 0; i < plan.RepeatCount; i++)
                {
                    token.ThrowIfCancellationRequested();
                    PlayOnce(plan, held, token);
                }
                break;

            case PlaybackMode.Hold:
                while (true)
                {
                    token.ThrowIfCancellationRequested();

                    var pass = Stopwatch.StartNew();
                    PlayOnce(plan, held, token);

                    // Макрос из шагов с нулевыми задержками прокрутился бы здесь вплотную и
                    // сжёг ядро: между проходами держим минимальную паузу.
                    if (pass.ElapsedMilliseconds < MinimumHoldCycleMs)
                    {
                        PreciseDelay(MinimumHoldCycleMs - (int)pass.ElapsedMilliseconds, token);
                    }
                }
        }
    }

    private void PlayOnce(PlaybackPlan plan, HeldInput held, CancellationToken token)
    {
        foreach (var step in plan.Steps)
        {
            token.ThrowIfCancellationRequested();

            int delay = EffectiveDelay(step.DelayBeforeMs, plan.Speed, plan.JitterMs, GapFor(step, plan.MinGapMs));
            PreciseDelay(delay, token);

            token.ThrowIfCancellationRequested();
            SendStep(step, held, plan.UseAbsoluteMovement);
        }
    }

    internal static int ApplySpeed(int delayMs, double speedMultiplier) =>
        (int)Math.Round(delayMs / speedMultiplier, MidpointRounding.AwayFromZero);

    internal static int ApplyJitter(int delayMs, int jitterMs)
    {
        if (jitterMs <= 0)
        {
            return delayMs;
        }

        int offset = Random.Shared.Next(-jitterMs, jitterMs + 1);
        return Math.Max(delayMs + offset, 0);
    }

    /// <summary>
    /// Пауза перед шагом с учётом наименьшего промежутка для игр. Игра читает клавиатуру раз
    /// в кадр: нажатие и отпускание без промежутка между ними до неё не доходят. Ни ускорение,
    /// ни разброс не опускают паузу ниже этого промежутка; большие задержки из таблицы
    /// остаются как есть.
    /// </summary>
    internal static int EffectiveDelay(int delayMs, double speedMultiplier, int jitterMs, int minGapMs) =>
        Math.Max(ApplyJitter(ApplySpeed(delayMs, speedMultiplier), jitterMs), Math.Max(minGapMs, 0));

    /// <summary>
    /// Промежуток нужен нажатиям и отпусканиям — клавиш и кнопок мыши. Движению мыши и колесу
    /// он ни к чему: старые макросы из сотни мелких движений иначе замедлились бы в разы, а
    /// шаг-пауза сам по себе и есть задержка.
    /// </summary>
    internal static int GapFor(MacroStep step, int minGapMs) => step switch
    {
        KeyStep => minGapMs,
        MouseStep { Action: MouseStepAction.Down or MouseStepAction.Up } => minGapMs,
        _ => 0,
    };

    /// <summary>
    /// Точность обычного Thread.Sleep ограничена разрешением системного таймера (~15 мс),
    /// а нужно ≤10 мс отклонения на шаг — поэтому спим грубо, оставляя запас, и добираем
    /// точность busy-wait'ом по Stopwatch. WaitHandle.WaitOne реагирует на отмену немедленно,
    /// не дожидаясь истечения таймаута — это и есть мгновенная реакция на panic-key.
    /// </summary>
    private static void PreciseDelay(int delayMs, CancellationToken token)
    {
        if (delayMs <= 0)
        {
            token.ThrowIfCancellationRequested();
            return;
        }

        var stopwatch = Stopwatch.StartNew();

        int coarseMs = delayMs - 2;
        if (coarseMs > 0 && token.WaitHandle.WaitOne(coarseMs))
        {
            token.ThrowIfCancellationRequested();
        }

        while (stopwatch.ElapsedMilliseconds < delayMs)
        {
            token.ThrowIfCancellationRequested();
            Thread.SpinWait(100);
        }
    }

    private void SendStep(MacroStep step, HeldInput held, bool useAbsoluteMovement)
    {
        switch (step)
        {
            case KeyStep keyStep:
                SendKey(keyStep, held);
                break;
            case MouseStep mouseStep:
                SendMouse(mouseStep, held, useAbsoluteMovement);
                break;
        }
    }

    private void SendKey(KeyStep step, HeldInput held)
    {
        bool isDown = step.Action == KeyAction.Down;

        // Обычный путь — scan-код: только его читают игры через DirectInput/Raw Input.
        // Если scan-кода нет (старый профиль, экзотическая клавиша), шаг не потерян:
        // отправляем virtual-key, иначе событие получилось бы пустым и его никто не распознает.
        ushort scanCode = step.ScanCode != 0
            ? step.ScanCode
            : ScanCodeMap.GetScanCode(step.VirtualKey);
        bool useScanCode = scanCode != 0;

        uint flags = useScanCode ? NativeMethods.KEYEVENTF_SCANCODE : 0;
        if (step.Extended)
        {
            flags |= NativeMethods.KEYEVENTF_EXTENDEDKEY;
        }

        if (!isDown)
        {
            flags |= NativeMethods.KEYEVENTF_KEYUP;
        }

        SendSingleInput(new INPUT
        {
            Type = NativeMethods.INPUT_KEYBOARD,
            U = new InputUnion
            {
                Ki = new KEYBDINPUT
                {
                    WVk = useScanCode ? (ushort)0 : (ushort)step.VirtualKey,
                    WScan = scanCode,
                    DwFlags = flags,
                    Time = 0,
                    DwExtraInfo = UIntPtr.Zero,
                },
            },
        });

        if (isDown)
        {
            // Именно scanCode, а не step.ScanCode: при нуле в шаге сюда попадал ноль, и
            // аварийное отпускание слало пустое событие — клавиша оставалась зажатой.
            held.Keys[step.VirtualKey] = (scanCode, step.Extended);
        }
        else
        {
            held.Keys.Remove(step.VirtualKey);
        }
    }

    private void SendMouse(MouseStep step, HeldInput held, bool useAbsoluteMovement)
    {
        switch (step.Action)
        {
            case MouseStepAction.Move:
                SendMouseMove(step, useAbsoluteMovement);
                break;
            case MouseStepAction.Down:
            case MouseStepAction.Up:
                SendMouseButton(step, held);
                break;
            case MouseStepAction.Wheel:
                SendMouseWheel(step);
                break;
        }
    }

    private void SendMouseMove(MouseStep step, bool useAbsoluteMovement)
    {
        var mi = useAbsoluteMovement
            ? new MOUSEINPUT
            {
                Dx = step.AbsX,
                Dy = step.AbsY,
                DwFlags = NativeMethods.MOUSEEVENTF_MOVE | NativeMethods.MOUSEEVENTF_ABSOLUTE,
            }
            : new MOUSEINPUT
            {
                Dx = step.Dx,
                Dy = step.Dy,
                DwFlags = NativeMethods.MOUSEEVENTF_MOVE,
            };

        SendSingleInput(new INPUT { Type = NativeMethods.INPUT_MOUSE, U = new InputUnion { Mi = mi } });
    }

    private void SendMouseButton(MouseStep step, HeldInput held)
    {
        if (step.Button is not { } button)
        {
            return;
        }

        bool isDown = step.Action == MouseStepAction.Down;
        uint mouseData = 0;

        uint flags = button switch
        {
            MouseButtonKind.Left => isDown ? NativeMethods.MOUSEEVENTF_LEFTDOWN : NativeMethods.MOUSEEVENTF_LEFTUP,
            MouseButtonKind.Right => isDown ? NativeMethods.MOUSEEVENTF_RIGHTDOWN : NativeMethods.MOUSEEVENTF_RIGHTUP,
            MouseButtonKind.Middle => isDown ? NativeMethods.MOUSEEVENTF_MIDDLEDOWN : NativeMethods.MOUSEEVENTF_MIDDLEUP,
            MouseButtonKind.X1 or MouseButtonKind.X2 => isDown ? NativeMethods.MOUSEEVENTF_XDOWN : NativeMethods.MOUSEEVENTF_XUP,
            _ => 0,
        };

        if (button is MouseButtonKind.X1 or MouseButtonKind.X2)
        {
            mouseData = button == MouseButtonKind.X1 ? NativeMethods.XBUTTON1 : NativeMethods.XBUTTON2;
        }

        SendSingleInput(new INPUT
        {
            Type = NativeMethods.INPUT_MOUSE,
            U = new InputUnion { Mi = new MOUSEINPUT { DwFlags = flags, MouseData = mouseData } },
        });

        if (isDown)
        {
            held.Buttons.Add(button);
        }
        else
        {
            held.Buttons.Remove(button);
        }
    }

    private void SendMouseWheel(MouseStep step)
    {
        uint flags = step.WheelHorizontal ? NativeMethods.MOUSEEVENTF_HWHEEL : NativeMethods.MOUSEEVENTF_WHEEL;

        SendSingleInput(new INPUT
        {
            Type = NativeMethods.INPUT_MOUSE,
            U = new InputUnion { Mi = new MOUSEINPUT { DwFlags = flags, MouseData = unchecked((uint)step.WheelDelta) } },
        });
    }

    private void ReleaseAllHeld(HeldInput held)
    {
        foreach (var info in held.Keys.Values)
        {
            SendSingleInput(new INPUT
            {
                Type = NativeMethods.INPUT_KEYBOARD,
                U = new InputUnion
                {
                    Ki = new KEYBDINPUT
                    {
                        WVk = 0,
                        WScan = info.ScanCode,
                        DwFlags = NativeMethods.KEYEVENTF_SCANCODE | NativeMethods.KEYEVENTF_KEYUP
                            | (info.Extended ? NativeMethods.KEYEVENTF_EXTENDEDKEY : 0),
                        Time = 0,
                        DwExtraInfo = UIntPtr.Zero,
                    },
                },
            });
        }
        held.Keys.Clear();

        foreach (var button in held.Buttons)
        {
            SendSingleInput(new INPUT
            {
                Type = NativeMethods.INPUT_MOUSE,
                U = new InputUnion
                {
                    Mi = new MOUSEINPUT
                    {
                        DwFlags = button switch
                        {
                            MouseButtonKind.Left => NativeMethods.MOUSEEVENTF_LEFTUP,
                            MouseButtonKind.Right => NativeMethods.MOUSEEVENTF_RIGHTUP,
                            MouseButtonKind.Middle => NativeMethods.MOUSEEVENTF_MIDDLEUP,
                            _ => NativeMethods.MOUSEEVENTF_XUP,
                        },
                        MouseData = button switch
                        {
                            MouseButtonKind.X1 => NativeMethods.XBUTTON1,
                            MouseButtonKind.X2 => NativeMethods.XBUTTON2,
                            _ => 0,
                        },
                    },
                },
            });
        }
        held.Buttons.Clear();
    }

    private void SendSingleInput(INPUT input)
    {
        // Подпись, по которой хуки узнают собственный ввод плеера: его нельзя ни поглощать,
        // ни принимать за нажатие кнопки мыши, ни записывать (см. NativeMethods.SelfInputTag).
        if (input.Type == NativeMethods.INPUT_KEYBOARD)
        {
            input.U.Ki.DwExtraInfo = NativeMethods.SelfInputTag;
        }
        else if (input.Type == NativeMethods.INPUT_MOUSE)
        {
            input.U.Mi.DwExtraInfo = NativeMethods.SelfInputTag;
        }

        var inputs = new[] { input };
        uint sent = NativeMethods.SendInput(1, inputs, Marshal.SizeOf<INPUT>());
        if (sent != 1)
        {
            logger.LogWarning(
                "SendInput did not deliver the event (error code {Error})",
                Marshal.GetLastWin32Error());
        }
    }
}
