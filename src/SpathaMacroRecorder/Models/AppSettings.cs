using SpathaMacroRecorder.Native;

namespace SpathaMacroRecorder.Models;

public sealed record AppSettings
{
    /// <summary>
    /// Virtual-key аварийной остановки. По умолчанию Pause/Break — её почти не используют игры,
    /// в отличие от Esc и F-ряда. 0 означает «не задана».
    /// </summary>
    public int PanicKeyVirtualKey { get; set; } = VirtualKeyCodes.Pause;

    /// <summary>Клавиша паузы/продолжения записи. По умолчанию Scroll Lock. 0 — выключена.</summary>
    public int PauseRecordingVirtualKey { get; set; } = VirtualKeyCodes.ScrollLock;

    /// <summary>Язык интерфейса: "ru" или "en".</summary>
    public string Language { get; set; } = "en";

    /// <summary>
    /// Наименьшая пауза перед каждым нажатием и отпусканием при воспроизведении, мс. Игры
    /// читают клавиатуру раз в кадр: нажатие и отпускание, отправленные в одно мгновение,
    /// до игры не доходят вовсе — Helldivers 2 такую стратагему просто не видит. Задержки
    /// из таблицы шагов больше этого значения работают как обычно. 0 — выключено.
    /// </summary>
    public int KeyGapMs { get; set; } = 30;

    /// <summary>
    /// Фактическое соответствие «кнопка каталога → код, который она шлёт»: buttonId → "F19",
    /// "XBUTTON1" и т.п. Заполняется определением на живой мыши; пусто — берётся порядок
    /// по умолчанию из каталога.
    /// </summary>
    public Dictionary<string, string> ButtonCodes { get; set; } = [];

    /// <summary>
    /// «Режим рабочего стола»: движение мыши воспроизводится абсолютным позиционированием
    /// вместо относительных дельт. Для игр должен быть выключен (тех. пункт 3 задания).
    /// </summary>
    public bool UseAbsoluteMovement { get; set; }
}
