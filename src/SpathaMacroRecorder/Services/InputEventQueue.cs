using System.Threading.Channels;

namespace SpathaMacroRecorder.Services;

internal enum RawInputSource
{
    Keyboard,
    Mouse,
}

/// <summary>
/// Сырое событие ввода, снятое в callback'е хука. Хранит только то, что реально пришло из
/// KBDLLHOOKSTRUCT/MSLLHOOKSTRUCT — интерпретация (авто-повтор, привязка к кнопке каталога и т.д.)
/// происходит выше по цепочке, не здесь.
/// </summary>
internal readonly record struct RawInputEvent(
    RawInputSource Source,
    int Message,
    int VirtualKey,
    ushort ScanCode,
    bool ExtendedKey,
    int MouseX,
    int MouseY,
    int MouseData,
    long TimestampMs,
    bool FromSelf = false,
    bool Injected = false);

/// <summary>
/// Канал между callback'ами хуков (пишут) и consumer-тасками (читают). Callback хука не должен
/// тормозить дольше LowLevelHooksTimeout (300 мс по умолчанию) — иначе Windows молча снимет хук
/// (см. тех. пункт 4 задания). Запись через TryWrite в unbounded-канал никогда не блокирует.
/// </summary>
internal sealed class InputEventQueue
{
    // Ограниченный канал, а не безразмерный: если consumer почему-то отстанет, очередь не
    // должна расти бесконечно и утаскивать приложение в своп. Ввод — данные реального времени,
    // поэтому при переполнении выгоднее выбросить самое старое событие, чем копить хвост.
    private readonly Channel<RawInputEvent> _channel = Channel.CreateBounded<RawInputEvent>(
        new BoundedChannelOptions(4096)
        {
            SingleWriter = false,
            SingleReader = true,
            FullMode = BoundedChannelFullMode.DropOldest,
        });

    internal ChannelWriter<RawInputEvent> Writer => _channel.Writer;

    internal ChannelReader<RawInputEvent> Reader => _channel.Reader;
}
