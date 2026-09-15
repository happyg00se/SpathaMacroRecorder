namespace SpathaMacroRecorder.Models;

/// <summary>
/// Привязка макросов к кнопкам мыши. Вынесено из ViewModel отдельно и без зависимостей от UI —
/// именно здесь легко ошибиться (кнопка должна остаться ровно у одного макроса), и именно это
/// нужно уметь проверять тестами.
/// </summary>
public static class MacroAssignment
{
    /// <summary>Макрос, назначенный на кнопку, либо null.</summary>
    public static Macro? Find(MacroProfile profile, string buttonId) =>
        profile.Macros.FirstOrDefault(m => m.Trigger?.ButtonId == buttonId);

    /// <summary>
    /// Вешает макрос на кнопку. Кнопка может держать только один макрос, поэтому у прежнего
    /// владельца привязка снимается. macro = null просто освобождает кнопку.
    /// Привязки остальных кнопок не затрагиваются.
    /// </summary>
    public static void Assign(MacroProfile profile, string buttonId, Macro? macro)
    {
        if (MouseButtonCatalog.Find(buttonId)?.Assignable != true)
        {
            return;
        }

        foreach (var previous in profile.Macros.Where(m => m.Trigger?.ButtonId == buttonId).ToList())
        {
            previous.Trigger = null;
        }

        if (macro is null)
        {
            return;
        }

        // Один макрос — одна кнопка: если его уже вешали на другую, старую привязку снимаем,
        // иначе он сработал бы сразу от двух кнопок.
        macro.Trigger = MouseButtonCatalog.CreateTrigger(buttonId);
    }
}
