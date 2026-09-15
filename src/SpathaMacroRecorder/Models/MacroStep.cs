using System.Text.Json.Serialization;

namespace SpathaMacroRecorder.Models;

[JsonConverter(typeof(CamelCaseStringEnumConverter<KeyAction>))]
public enum KeyAction
{
    Down,
    Up,
}

[JsonConverter(typeof(CamelCaseStringEnumConverter<MouseStepAction>))]
public enum MouseStepAction
{
    Move,
    Down,
    Up,
    Wheel,
}

// В отличие от остальных enum'ов модели, значения этого сериализуются как есть (PascalCase,
// "Left"/"Right"/...), а не camelCase — так задано в примере JSON в самом задании.
[JsonConverter(typeof(JsonStringEnumConverter<MouseButtonKind>))]
public enum MouseButtonKind
{
    Left,
    Right,
    Middle,
    X1,
    X2,
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(KeyStep), "key")]
[JsonDerivedType(typeof(MouseStep), "mouse")]
[JsonDerivedType(typeof(PauseStep), "pause")]
public abstract record MacroStep
{
    /// <summary>
    /// Задержка перед этим шагом относительно предыдущего, мс. Settable (не init) — редактор
    /// шагов (Фаза 7) редактирует её прямо в таблице через TwoWay-биндинг.
    /// </summary>
    public required int DelayBeforeMs { get; set; }
}

/// <summary>
/// Шаг без действия — только задержка. Не создаётся записью (MacroRecorder никогда его не
/// производит), только вручную в редакторе шагов операцией «вставить паузу».
/// </summary>
public sealed record PauseStep : MacroStep;

public sealed record KeyStep : MacroStep
{
    public required KeyAction Action { get; init; }

    [JsonPropertyName("vk")]
    public required int VirtualKey { get; init; }

    [JsonPropertyName("scan")]
    public required ushort ScanCode { get; init; }

    public required bool Extended { get; init; }
}

public sealed record MouseStep : MacroStep
{
    public required MouseStepAction Action { get; init; }

    // Поля ниже относятся только к одному конкретному Action — при сериализации не относящиеся
    // к делу поля со значением по умолчанию опускаются (JsonIgnoreCondition.WhenWritingDefault),
    // чтобы JSON шага выглядел так же лаконично, как в примере из задания.

    // Action == Move: относительное смещение (для игр, см. тех. пункт 3 задания)
    // и нормализованная 0–65535 абсолютная позиция (для режима рабочего стола).
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int Dx { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int Dy { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int AbsX { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int AbsY { get; init; }

    // Action == Down/Up.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public MouseButtonKind? Button { get; init; }

    // Action == Wheel.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int WheelDelta { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool WheelHorizontal { get; init; }
}
