using System.Collections.ObjectModel;

namespace SpathaMacroRecorder.Models;

public sealed record MacroProfile
{
    /// <summary>Версия схемы — понадобится при будущих миграциях формата.</summary>
    public int SchemaVersion { get; init; } = 1;

    public required string ProfileName { get; set; }
    public ObservableCollection<Macro> Macros { get; init; } = [];
}
