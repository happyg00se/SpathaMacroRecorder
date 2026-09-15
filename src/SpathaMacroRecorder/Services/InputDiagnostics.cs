using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SpathaMacroRecorder.Models;
using SpathaMacroRecorder.Native;

namespace SpathaMacroRecorder.Services;

/// <summary>
/// Что последним пришло от кнопок мыши и чем это кончилось — для строки состояния.
///
/// Посмотреть на компьютер заказчика нельзя, а «не работает» бывает по трём разным причинам:
/// клавиша не приходит вовсе (не запущен G-Helper или Armoury Crate сбросила назначения), приходит,
/// но не привязана к метке, или привязана, но макрос не запускается. Строка состояния показывает
/// всю цепочку, и одного скриншота хватает, чтобы понять, где обрыв.
///
/// Обычные клавиши не запоминаются и не пишутся в лог никогда — только коды кнопок мыши
/// (привязанные к меткам или F13–F24, которыми люди не печатают). Иначе это был бы кейлоггер.
/// </summary>
internal sealed class InputDiagnostics : IHostedService
{
    /// <summary>Сигнал кнопки. Для мыши VirtualKey — номер XBUTTON (1 или 2).</summary>
    internal sealed record Press(int VirtualKey, bool IsMouse, bool Injected, string? ButtonId, DateTime AtUtc);

    /// <summary>Чем кончилось нажатие: какой макрос запущен (null — на кнопке его нет).</summary>
    internal sealed record Outcome(string ButtonId, string? MacroName, DateTime AtUtc);

    private readonly InputEventDispatcher _dispatcher;
    private readonly ILogger<InputDiagnostics> _logger;

    // Зажатая сейчас клавиша кнопки мыши: её повторы не новое нажатие. Только поток диспетчера.
    private int _heldKey = -1;

    private volatile Press? _lastPress;
    private volatile Outcome? _lastOutcome;

    public InputDiagnostics(InputEventDispatcher dispatcher, ILogger<InputDiagnostics> logger)
    {
        _dispatcher = dispatcher;
        _logger = logger;
    }

    internal Press? LastPress => _lastPress;

    internal Outcome? LastOutcome => _lastOutcome;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _dispatcher.EventCaptured += OnEventCaptured;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _dispatcher.EventCaptured -= OnEventCaptured;
        return Task.CompletedTask;
    }

    internal void OnEventCaptured(RawInputEvent evt)
    {
        if (evt.FromSelf)
        {
            return;
        }

        if (evt.Source == RawInputSource.Keyboard)
        {
            bool isDown = evt.Message is NativeMethods.WM_KEYDOWN or NativeMethods.WM_SYSKEYDOWN;
            bool isUp = evt.Message is NativeMethods.WM_KEYUP or NativeMethods.WM_SYSKEYUP;

            if (isUp && evt.VirtualKey == _heldKey)
            {
                _heldKey = -1;
                return;
            }

            if (!isDown || evt.VirtualKey == _heldKey)
            {
                return;
            }

            string? buttonId = TriggerBindingService.Resolve(evt)?.ButtonId;
            if (buttonId is null && !IsExtraFunctionKey(evt.VirtualKey))
            {
                return;
            }

            _heldKey = evt.VirtualKey;
            Record(new Press(evt.VirtualKey, IsMouse: false, evt.Injected, buttonId, DateTime.UtcNow));
        }
        else if (evt.Message == NativeMethods.WM_XBUTTONDOWN)
        {
            int xButton = evt.MouseData == NativeMethods.XBUTTON2 ? 2 : 1;
            Record(new Press(xButton, IsMouse: true, evt.Injected, TriggerBindingService.Resolve(evt)?.ButtonId, DateTime.UtcNow));
        }
    }

    /// <summary>Главное окно сообщает, нашёлся ли на нажатой кнопке макрос.</summary>
    internal void ReportTrigger(string buttonId, string? macroName)
    {
        _lastOutcome = new Outcome(buttonId, macroName, DateTime.UtcNow);

        if (macroName is null)
        {
            _logger.LogInformation("Mouse button {Button} pressed, but no macro is assigned to it", buttonId);
        }
        else
        {
            _logger.LogInformation("Mouse button {Button} pressed, macro {Macro} started", buttonId, macroName);
        }
    }

    private void Record(Press press)
    {
        _lastPress = press;

        _logger.LogInformation(
            "Mouse button signal {Code} (sent by software: {Injected}) -> {Button}",
            press.IsMouse ? $"XBUTTON{press.VirtualKey}" : $"VK 0x{press.VirtualKey:X2}",
            press.Injected,
            press.ButtonId ?? "no marker");
    }

    private static bool IsExtraFunctionKey(int virtualKey) =>
        virtualKey is >= VirtualKeyCodes.F13 and <= VirtualKeyCodes.F24;
}
