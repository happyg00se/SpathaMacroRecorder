using System.Collections.Specialized;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SpathaMacroRecorder.Models;
using SpathaMacroRecorder.Services;
using SpathaMacroRecorder.Views;

namespace SpathaMacroRecorder.ViewModels;

/// <summary>Центральная панель: список макросов текущего профиля + таблица шагов выбранного макроса.</summary>
internal partial class MacroEditorViewModel : ObservableObject
{
    private readonly ProfileManager _profileManager;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddMacroCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveProfileCommand))]
    private MacroProfile? _currentProfile;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DeleteMacroCommand))]
    [NotifyCanExecuteChangedFor(nameof(AddStepCommand))]
    private Macro? _selectedMacro;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DeleteStepCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveStepUpCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveStepDownCommand))]
    private MacroStep? _selectedStep;

    public MacroEditorViewModel(ProfileManager profileManager)
    {
        _profileManager = profileManager;
    }

    public void SetProfile(MacroProfile? profile)
    {
        CurrentProfile = profile;
        SelectedMacro = profile?.Macros.FirstOrDefault();
    }

    internal void SaveCurrentProfile()
    {
        if (CurrentProfile is not null)
        {
            _profileManager.Save(CurrentProfile);
        }
    }

    /// <summary>
    /// Явное сохранение по кнопке. Профиль и так пишется на диск после каждого изменения —
    /// эта команда просто даёт возможность убедиться в этом самому.
    /// </summary>
    [RelayCommand(CanExecute = nameof(HasProfile))]
    private void SaveProfile()
    {
        if (CurrentProfile is null)
        {
            return;
        }

        SaveCurrentProfile();
        MessageBox.Show(
            string.Format(AppText.Instance["SavedText"], CurrentProfile.ProfileName),
            AppText.Instance["SavedTitle"],
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    [RelayCommand(CanExecute = nameof(HasProfile))]
    private void AddMacro()
    {
        if (CurrentProfile is null)
        {
            return;
        }

        string? name = TextPromptDialog.Show(Application.Current.MainWindow, AppText.Instance["NewMacro"], AppText.Instance["MacroName"], AppText.Instance["NewMacro"]);
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        var macro = new Macro { Id = Guid.NewGuid(), Name = name };
        CurrentProfile.Macros.Add(macro);
        SelectedMacro = macro;
        SaveCurrentProfile();
    }

    /// <summary>
    /// Все выделенные в списке макросы. ListBox.SelectedItems не является свойством зависимости
    /// и напрямую не биндится — список приходит из code-behind по SelectionChanged.
    /// </summary>
    public List<Macro> SelectedMacros { get; private set; } = [];

    public void SetSelectedMacros(IEnumerable<Macro> macros)
    {
        SelectedMacros = macros.ToList();
        DeleteMacroCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(HasSelectedMacro))]
    private void DeleteMacro()
    {
        if (CurrentProfile is null)
        {
            return;
        }

        // Удаляем всё выделение целиком; если выделения нет — текущий макрос.
        var toDelete = SelectedMacros.Count > 0
            ? SelectedMacros.ToList()
            : SelectedMacro is null ? [] : new List<Macro> { SelectedMacro };

        if (toDelete.Count == 0)
        {
            return;
        }

        string question = toDelete.Count == 1
            ? string.Format(AppText.Instance["DeleteMacroQ"], toDelete[0].Name)
            : string.Format(AppText.Instance["DeleteMacrosQ"], toDelete.Count);

        if (MessageBox.Show(question, AppText.Instance["DeleteMacroTitle"], MessageBoxButton.YesNo, MessageBoxImage.Question)
            != MessageBoxResult.Yes)
        {
            return;
        }

        foreach (var macro in toDelete)
        {
            CurrentProfile.Macros.Remove(macro);
        }

        SelectedMacros = [];
        SelectedMacro = CurrentProfile.Macros.FirstOrDefault();
        SaveCurrentProfile();
    }

    /// <summary>
    /// Отдельного пикера содержимого шага в задании нет, поэтому "добавить" клонирует
    /// выбранный шаг (а без выделения — последний): получается копия, у которой остаётся
    /// поправить задержку. Пустых шагов-заглушек команда не создаёт.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanAddStep))]
    private void AddStep()
    {
        if (SelectedMacro is null)
        {
            return;
        }

        var source = SelectedStep ?? SelectedMacro.Steps.LastOrDefault();
        MacroStep? clone = source switch
        {
            KeyStep keyStep => keyStep with { },
            MouseStep mouseStep => mouseStep with { },
            PauseStep pauseStep => pauseStep with { },
            _ => null,
        };

        if (clone is not null)
        {
            InsertStep(clone);
        }
    }

    /// <summary>
    /// Обнуляет задержку у выделенных шагов, а если ничего не выделено — у всех сразу.
    /// Иначе ноль пришлось бы вбивать в каждую строку руками.
    /// </summary>
    [RelayCommand(CanExecute = nameof(HasSelectedMacro))]
    private void ZeroDelays()
    {
        if (SelectedMacro is null)
        {
            return;
        }

        var target = SelectedSteps.Count > 0 ? SelectedSteps : SelectedMacro.Steps.ToList();
        foreach (var step in target)
        {
            step.DelayBeforeMs = 0;
        }

        SaveCurrentProfile();

        // Шаги — не ObservableObject, таблица сама о правке не узнает: перевставляем строки.
        var snapshot = SelectedMacro.Steps.ToList();
        SelectedMacro.Steps.Clear();
        foreach (var step in snapshot)
        {
            SelectedMacro.Steps.Add(step);
        }
    }

    private void InsertStep(MacroStep step)
    {
        if (SelectedMacro is null)
        {
            return;
        }

        int index = SelectedStep is null ? SelectedMacro.Steps.Count : SelectedMacro.Steps.IndexOf(SelectedStep) + 1;
        SelectedMacro.Steps.Insert(index, step);
        SelectedStep = step;
        SaveCurrentProfile();
    }

    /// <summary>Все выделенные шаги — как и у макросов, приходят из code-behind.</summary>
    public List<MacroStep> SelectedSteps { get; private set; } = [];

    public void SetSelectedSteps(IEnumerable<MacroStep> steps)
    {
        SelectedSteps = steps.ToList();
        DeleteStepCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(HasSelectedStep))]
    private void DeleteStep()
    {
        if (SelectedMacro is null)
        {
            return;
        }

        // Удаляем всё выделение целиком, а не по одной строке.
        var toDelete = SelectedSteps.Count > 0
            ? SelectedSteps.ToList()
            : SelectedStep is null ? [] : new List<MacroStep> { SelectedStep };

        if (toDelete.Count == 0)
        {
            return;
        }

        foreach (var step in toDelete)
        {
            SelectedMacro.Steps.Remove(step);
        }

        SelectedSteps = [];
        SelectedStep = null;
        SaveCurrentProfile();
    }

    [RelayCommand(CanExecute = nameof(CanMoveStepUp))]
    private void MoveStepUp() => MoveStep(-1);

    [RelayCommand(CanExecute = nameof(CanMoveStepDown))]
    private void MoveStepDown() => MoveStep(1);

    private void MoveStep(int offset)
    {
        if (SelectedMacro is null || SelectedStep is null)
        {
            return;
        }

        int index = SelectedMacro.Steps.IndexOf(SelectedStep);
        int newIndex = index + offset;
        if (newIndex < 0 || newIndex >= SelectedMacro.Steps.Count)
        {
            return;
        }

        SelectedMacro.Steps.Move(index, newIndex);
        SaveCurrentProfile();

        // SelectedStep — та же ссылка, что и раньше (просто на новой позиции), поэтому
        // ObservableProperty-сеттер не срабатывает и не переоценивает CanExecute сам по себе.
        MoveStepUpCommand.NotifyCanExecuteChanged();
        MoveStepDownCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Копировать нечего, пока в макросе нет ни одного шага: список шагов наблюдаемый,
    /// поэтому команда переспрашивается на каждое его изменение.
    /// </summary>
    private bool CanAddStep => SelectedMacro is { Steps.Count: > 0 };

    partial void OnSelectedMacroChanging(Macro? oldValue, Macro? newValue)
    {
        if (oldValue is not null)
        {
            oldValue.Steps.CollectionChanged -= OnStepsChanged;
        }

        if (newValue is not null)
        {
            newValue.Steps.CollectionChanged += OnStepsChanged;
        }
    }

    private void OnStepsChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        AddStepCommand.NotifyCanExecuteChanged();

    private bool HasProfile => CurrentProfile is not null;
    private bool HasSelectedMacro => SelectedMacro is not null || SelectedMacros.Count > 0;
    private bool HasSelectedStep => SelectedStep is not null || SelectedSteps.Count > 0;

    private bool CanMoveStepUp =>
        SelectedMacro is not null && SelectedStep is not null && SelectedMacro.Steps.IndexOf(SelectedStep) > 0;

    private bool CanMoveStepDown =>
        SelectedMacro is not null && SelectedStep is not null
        && SelectedMacro.Steps.IndexOf(SelectedStep) < SelectedMacro.Steps.Count - 1;
}
