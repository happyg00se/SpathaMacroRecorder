using SpathaMacroRecorder.Models;
using SpathaMacroRecorder.Native;

namespace SpathaMacroRecorder.Services;

/// <summary>
/// Превращает поток сырых событий (RawInputEvent) в список шагов макроса. Не читает канал сам —
/// события ему подаёт InputEventDispatcher (Feed вызывается из подписки на EventCaptured), это
/// сделано отдельно от источника событий, чтобы Feed можно было гонять в юнит-тестах напрямую.
///
/// Записывается ТОЛЬКО клавиатура: события мыши (движение, кнопки, колесо) игнорируются.
/// Плеер шаги мыши по-прежнему умеет воспроизводить — старые профили с ними работают.
///
/// Задержки не замеряются: каждый шаг получает 0. Паузы между нажатиями при записи —
/// это скорость печати человека, в макросе она не нужна; при необходимости задержку
/// проставляют вручную в таблице шагов.
/// </summary>
internal sealed class MacroRecorder
{
    private readonly List<MacroStep> _steps = [];
    private readonly HashSet<int> _keysDown = [];

    /// <summary>
    /// Virtual-key аварийной остановки и паузы записи: их самих записывать нельзя, иначе
    /// служебное нажатие попадёт в макрос. 0 — не задана.
    /// </summary>
    internal volatile int PanicVirtualKey;
    internal volatile int PauseVirtualKey;

    internal bool IsRecording { get; private set; }
    internal bool IsPaused { get; private set; }

    internal void Start()
    {
        _steps.Clear();
        _keysDown.Clear();
        IsPaused = false;
        IsRecording = true;
    }

    internal IReadOnlyList<MacroStep> Stop()
    {
        IsRecording = false;
        IsPaused = false;
        return _steps.ToList();
    }

    internal void Pause() => IsPaused = true;

    internal void Resume() => IsPaused = false;

    internal void Feed(RawInputEvent evt)
    {
        // Собственный ввод плеера в запись не попадает: записывается человек, а не макрос.
        if (!IsRecording || IsPaused || evt.FromSelf)
        {
            return;
        }

        // Мышь не записываем вовсе — ни движение, ни кнопки, ни колесо.
        if (evt.Source == RawInputSource.Keyboard)
        {
            FeedKeyboard(evt);
        }
    }

    private void FeedKeyboard(RawInputEvent evt)
    {
        bool isKeyDown = evt.Message is NativeMethods.WM_KEYDOWN or NativeMethods.WM_SYSKEYDOWN;
        bool isKeyUp = evt.Message is NativeMethods.WM_KEYUP or NativeMethods.WM_SYSKEYUP;

        if (!isKeyDown && !isKeyUp)
        {
            return;
        }

        // Служебные клавиши в макрос не попадают: клавиши, назначенные на кнопки мыши (нажатие
        // такой кнопки во время записи иначе вписало бы её же в шаги), плюс panic и пауза.
        if (MouseButtonCatalog.IsBoundKey(evt.VirtualKey, evt.ScanCode, evt.ExtendedKey)
            || evt.VirtualKey == PanicVirtualKey
            || evt.VirtualKey == PauseVirtualKey)
        {
            return;
        }

        if (isKeyDown)
        {
            // Фильтр auto-repeat: повторный down без промежуточного up не записываем.
            if (!_keysDown.Add(evt.VirtualKey))
            {
                return;
            }
        }
        else
        {
            _keysDown.Remove(evt.VirtualKey);
        }

        // Часть источников (в том числе драйверы мышей, рассылающие назначенные клавиши)
        // отдаёт события с нулевым scan-кодом. Отправлять такой шаг бессмысленно — Windows
        // не сопоставит его ни с какой клавишей, поэтому восстанавливаем код по virtual-key.
        ushort scanCode = evt.ScanCode != 0
            ? evt.ScanCode
            : ScanCodeMap.GetScanCode(evt.VirtualKey);

        _steps.Add(new KeyStep
        {
            DelayBeforeMs = 0,
            Action = isKeyDown ? KeyAction.Down : KeyAction.Up,
            VirtualKey = evt.VirtualKey,
            ScanCode = scanCode,
            Extended = evt.ExtendedKey || ScanCodeMap.IsExtended(evt.VirtualKey),
        });
    }

}
