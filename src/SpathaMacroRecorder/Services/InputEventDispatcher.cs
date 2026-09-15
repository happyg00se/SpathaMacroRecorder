using Microsoft.Extensions.Hosting;

namespace SpathaMacroRecorder.Services;

/// <summary>
/// Единственный reader канала InputEventQueue. Рассылает каждое событие подписчикам
/// (InputEventLogger, MacroRecorder и т.д.) синхронным событием — так у канала остаётся ровно
/// один читатель (SingleReader = true), а сколько угодно потребителей видят один и тот же поток
/// событий, а не конкурируют за них.
/// </summary>
internal sealed class InputEventDispatcher(InputEventQueue queue) : BackgroundService
{
    internal event Action<RawInputEvent>? EventCaptured;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var evt in queue.Reader.ReadAllAsync(stoppingToken))
        {
            EventCaptured?.Invoke(evt);
        }
    }
}
