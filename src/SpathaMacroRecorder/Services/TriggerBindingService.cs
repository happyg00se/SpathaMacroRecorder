using Microsoft.Extensions.Hosting;
using SpathaMacroRecorder.Models;
using SpathaMacroRecorder.Native;

namespace SpathaMacroRecorder.Services;

/// <summary>
/// Подписывается на поток событий обоих хуков и резолвит их в buttonId каталога. Вызывающему
/// коду (плееру, UI) разница в источнике не видна: «Вперёд» с хука мыши и «Боковая 1» с
/// клавиатурного хука приходят одинаково — как нажатие кнопки каталога.
/// </summary>
internal sealed class TriggerBindingService : IHostedService
{
    private readonly InputEventDispatcher _dispatcher;

    /// <summary>
    /// Какие клавиши сейчас держат кнопки мыши: virtual-key → buttonId.
    ///
    /// Зажатую клавишу Windows повторяет десятки раз в секунду, и каждый повтор иначе
    /// перезапускал бы макрос с начала — он не доигрывал бы ни разу. А отпускание узнаётся
    /// по запомненному нажатию, а не заново: модификаторы сочетания к этому моменту обычно
    /// уже отпущены. События приходят из одного потока диспетчера, блокировка не нужна.
    /// </summary>
    private readonly Dictionary<int, string> _pressedKeys = [];

    public TriggerBindingService(InputEventDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    /// <summary>
    /// Пока true, нажатия не запускают макросы. Нужно на время определения кнопки: иначе
    /// нажатие, которым пользователь показывает кнопку, заодно проиграет её макрос.
    /// </summary>
    internal bool Suspended { get; set; }

    internal event Action<string>? TriggerPressed;

    internal event Action<string>? TriggerReleased;

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
        // Собственный ввод плеера возвращается через те же хуки. Макрос, в котором есть
        // клавиша кнопки мыши, иначе запускал бы сам себя.
        if (evt.FromSelf)
        {
            return;
        }

        if (evt.Source == RawInputSource.Keyboard
            && evt.Message is NativeMethods.WM_KEYUP or NativeMethods.WM_SYSKEYUP
            && _pressedKeys.Remove(evt.VirtualKey, out string? releasedButtonId))
        {
            TriggerReleased?.Invoke(releasedButtonId);
            return;
        }

        if (Suspended)
        {
            return;
        }

        var resolved = Resolve(evt);
        if (resolved is not { } trigger)
        {
            return;
        }

        // Повтор зажатой клавиши — не новое нажатие.
        if (evt.Source == RawInputSource.Keyboard
            && trigger.IsPressed
            && !_pressedKeys.TryAdd(evt.VirtualKey, trigger.ButtonId))
        {
            return;
        }

        if (trigger.IsPressed)
        {
            TriggerPressed?.Invoke(trigger.ButtonId);
        }
        else
        {
            TriggerReleased?.Invoke(trigger.ButtonId);
        }

        // У колеса нет события отпускания — сразу отдаём парный Released, иначе режим hold
        // залипнет навсегда.
        if (trigger.IsMomentary)
        {
            TriggerReleased?.Invoke(trigger.ButtonId);
        }
    }

    internal static (string ButtonId, bool IsPressed, bool IsMomentary)? Resolve(RawInputEvent evt)
    {
        var resolved = evt.Source == RawInputSource.Keyboard ? ResolveKeyboard(evt) : ResolveMouse(evt);

        // Настраиваемых кнопок всего восемь. Обычные ЛКМ/ПКМ/колесо в путь триггеров не
        // попадают вовсе — иначе каждый клик в системе гонял бы лишнюю работу.
        return resolved is { } t && MouseButtonCatalog.Find(t.ButtonId)?.Assignable == true ? resolved : null;
    }

    private static (string ButtonId, bool IsPressed, bool IsMomentary)? ResolveKeyboard(RawInputEvent evt)
    {
        bool isDown = evt.Message is NativeMethods.WM_KEYDOWN or NativeMethods.WM_SYSKEYDOWN;
        bool isUp = evt.Message is NativeMethods.WM_KEYUP or NativeMethods.WM_SYSKEYUP;
        if (!isDown && !isUp)
        {
            return null;
        }

        // Лишние модификаторы не мешают: в Helldivers 2 стратагема вводится с зажатым Ctrl,
        // и кнопка, определённая без модификаторов, обязана срабатывать и так. Scan-код
        // узнаёт клавишу цифрового блока независимо от NumLock.
        var (ctrl, alt, shift) = InputSuppression.ModifierState();
        string? buttonId = MouseButtonCatalog.MatchKey(evt.VirtualKey, evt.ScanCode, evt.ExtendedKey, ctrl, alt, shift);

        return buttonId is null ? null : (buttonId, isDown, false);
    }

    private static (string ButtonId, bool IsPressed, bool IsMomentary)? ResolveMouse(RawInputEvent evt) => evt.Message switch
    {
        NativeMethods.WM_LBUTTONDOWN => ("lmb", true, false),
        NativeMethods.WM_LBUTTONUP => ("lmb", false, false),
        NativeMethods.WM_RBUTTONDOWN => ("rmb", true, false),
        NativeMethods.WM_RBUTTONUP => ("rmb", false, false),
        NativeMethods.WM_MBUTTONDOWN => ("wheel", true, false),
        NativeMethods.WM_MBUTTONUP => ("wheel", false, false),
        NativeMethods.WM_XBUTTONDOWN => (XButtonId(evt), true, false),
        NativeMethods.WM_XBUTTONUP => (XButtonId(evt), false, false),
        NativeMethods.WM_MOUSEWHEEL when evt.MouseData > 0 => ("wheelup", true, true),
        NativeMethods.WM_MOUSEWHEEL when evt.MouseData < 0 => ("wheeldown", true, true),
        _ => null,
    };

    // Какая из боковых «вперёд/назад» шлёт XBUTTON2, зависит от мыши и настроек Armoury
    // Crate — соответствие берём из каталога, где его можно переопределить определением.
    private static string XButtonId(RawInputEvent evt)
    {
        string code = evt.MouseData == NativeMethods.XBUTTON2 ? "XBUTTON2" : "XBUTTON1";
        return MouseButtonCatalog.ButtonIdForCode(code) ?? (evt.MouseData == NativeMethods.XBUTTON2 ? "fwd" : "back");
    }
}
