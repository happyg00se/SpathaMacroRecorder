using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace SpathaMacroRecorder.Models;

[JsonConverter(typeof(CamelCaseStringEnumConverter<PlaybackMode>))]
public enum PlaybackMode
{
    Once,
    Hold,
    Repeat,
}

/// <summary>
/// Не record с init-only свойствами, а обычные settable — редактор шагов (Фаза 7) биндится
/// к этим полям напрямую (TwoWay), без параллельного слоя ViewModel на каждое поле.
/// Steps — ObservableCollection, а не List: DataGrid должен реагировать на добавление/удаление
/// строк без ручного Refresh().
/// </summary>
public sealed record Macro
{
    public required Guid Id { get; init; }
    public required string Name { get; set; }
    public MacroTrigger? Trigger { get; set; }
    public PlaybackMode PlaybackMode { get; set; } = PlaybackMode.Once;
    public int RepeatCount { get; set; } = 1;
    public double SpeedMultiplier { get; set; } = 1.0;
    public int JitterMs { get; set; }
    public ObservableCollection<MacroStep> Steps { get; init; } = [];
}
