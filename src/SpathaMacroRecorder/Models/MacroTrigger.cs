using System.Text.Json.Serialization;

namespace SpathaMacroRecorder.Models;

// Как и MouseButtonKind — сериализуется как есть (PascalCase, "MouseHook"), см. пример JSON в задании.
[JsonConverter(typeof(JsonStringEnumConverter<TriggerSource>))]
public enum TriggerSource
{
    MouseHook,
    KeyboardHook,
}

/// <summary>
/// Привязка макроса к триггеру. ButtonId — ключ в MouseButtonCatalog (Фаза 8); Source/Code
/// заполняются из каталога автоматически и вручную пользователем не редактируются
/// (см. «Визуальный выбор триггера» в задании).
/// </summary>
public sealed record MacroTrigger
{
    public required string ButtonId { get; init; }
    public required TriggerSource Source { get; init; }
    public required string Code { get; init; }
}
