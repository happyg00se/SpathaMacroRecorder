using System.Text.Json;
using System.Text.Json.Serialization;

namespace SpathaMacroRecorder.Models;

/// <summary>
/// JsonStringEnumConverter&lt;T&gt; из атрибута нельзя сконструировать с параметром (naming policy) —
/// атрибуту нужен параметрless-конструируемый тип. Этот класс просто фиксирует camelCase.
/// </summary>
internal sealed class CamelCaseStringEnumConverter<TEnum>()
    : JsonStringEnumConverter<TEnum>(JsonNamingPolicy.CamelCase)
    where TEnum : struct, Enum;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true)]
[JsonSerializable(typeof(MacroProfile))]
[JsonSerializable(typeof(Macro))]
[JsonSerializable(typeof(MacroStep))]
[JsonSerializable(typeof(KeyStep))]
[JsonSerializable(typeof(MouseStep))]
[JsonSerializable(typeof(PauseStep))]
[JsonSerializable(typeof(MacroTrigger))]
[JsonSerializable(typeof(AppSettings))]
internal partial class MacroJsonContext : JsonSerializerContext;
