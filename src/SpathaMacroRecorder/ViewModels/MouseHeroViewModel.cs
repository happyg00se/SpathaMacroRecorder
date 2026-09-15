using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SpathaMacroRecorder.Models;
using SpathaMacroRecorder.Native;
using SpathaMacroRecorder.Services;

namespace SpathaMacroRecorder.ViewModels;

/// <summary>Маркер одной кнопки поверх изображения мыши.</summary>
internal sealed partial class MouseHeroButtonViewModel : ObservableObject
{
    public const double MarkerSize = 34;

    public MouseHeroButtonViewModel(MouseButtonDefinition definition)
    {
        Definition = definition;
    }

    public MouseButtonDefinition Definition { get; }

    public string ButtonId => Definition.ButtonId;
    public string DisplayName => AppText.ButtonName(Definition.ButtonId);

    // Canvas.Left/Top задают левый верхний угол — смещаем на радиус, чтобы центр метки
    // попал ровно в точку кнопки на фотографии.
    public double MarkerLeft => Definition.HeroX - (MarkerSize / 2);
    public double MarkerTop => Definition.HeroY - (MarkerSize / 2);


    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private string? _assignedMacroName;

    public bool IsAssigned => AssignedMacroName is not null;

    /// <summary>Подпись под маркером: имя макроса, если назначен.</summary>
    public string Caption => AssignedMacroName ?? string.Empty;

    partial void OnAssignedMacroNameChanged(string? value)
    {
        OnPropertyChanged(nameof(IsAssigned));
        OnPropertyChanged(nameof(Caption));
    }
}

/// <summary>Пункт выпадающего списка: снятие привязки или конкретный макрос.</summary>
internal sealed class MacroChoice(Macro? macro)
{
    public Macro? Macro { get; } = macro;

    public string Label => Macro?.Name ?? AppText.Instance["RemoveMacro"];
}

/// <summary>
/// Главный экран: изображение мыши с подсвеченными кнопками. Клик по кнопке открывает
/// назначение макроса — привязка делается прямо здесь, без отдельного диалога.
/// </summary>
internal sealed partial class MouseHeroViewModel : ObservableObject
{
    private readonly MacroEditorViewModel _editor;
    private readonly InputEventDispatcher _input;
    private readonly AppSettingsService _settings;
    private readonly TriggerBindingService _triggers;
    private readonly Dispatcher _ui = Dispatcher.CurrentDispatcher;

    public MouseHeroViewModel(
        MacroEditorViewModel editor,
        InputEventDispatcher input,
        AppSettingsService settings,
        TriggerBindingService triggers)
    {
        _editor = editor;
        _input = input;
        _settings = settings;
        _triggers = triggers;

        foreach (var definition in MouseButtonCatalog.Assignable)
        {
            Buttons.Add(new MouseHeroButtonViewModel(definition));
        }

        _editor.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(MacroEditorViewModel.CurrentProfile)
                or nameof(MacroEditorViewModel.SelectedMacro))
            {
                Refresh();
            }
        };

        Refresh();
    }

    public ObservableCollection<MouseHeroButtonViewModel> Buttons { get; } = [];

    /// <summary>Пункты списка: первым идёт снятие привязки, дальше макросы профиля.</summary>
    public ObservableCollection<MacroChoice> MacroChoices { get; } = [];

    [ObservableProperty]
    private MouseHeroButtonViewModel? _selectedButton;

    /// <summary>Идёт назначение или перестройка списка — сеттер SelectedChoice не должен писать.</summary>
    private bool _isAssigning;

    public string ProfileName => _editor.CurrentProfile?.ProfileName ?? AppText.Instance["NoProfile"];

    public bool HasSelection => SelectedButton is not null;

    public string SelectionTitle => SelectedButton?.DisplayName ?? string.Empty;

    // Настройки воспроизведения выбранного макроса. Живут здесь, а не в редакторе: панель
    // назначения — единственное место, где виден вопрос «что произойдёт по этой кнопке».
    public IReadOnlyList<PlaybackMode> PlaybackModes { get; } = Enum.GetValues<PlaybackMode>();

    public PlaybackMode PlaybackMode
    {
        get => AssignedMacro?.PlaybackMode ?? PlaybackMode.Once;
        set => UpdateMacro(m => m.PlaybackMode = value);
    }

    public bool IsRepeatMode => PlaybackMode == PlaybackMode.Repeat;

    public int RepeatCount
    {
        get => AssignedMacro?.RepeatCount ?? 1;
        set => UpdateMacro(m => m.RepeatCount = Math.Max(value, 1));
    }

    private void UpdateMacro(Action<Macro> change)
    {
        if (AssignedMacro is not { } macro)
        {
            return;
        }

        change(macro);
        _editor.SaveCurrentProfile();
        NotifyPlaybackSettingsChanged();
    }

    private void NotifyPlaybackSettingsChanged()
    {
        OnPropertyChanged(nameof(PlaybackMode));
        OnPropertyChanged(nameof(IsRepeatMode));
        OnPropertyChanged(nameof(RepeatCount));
    }

    /// <summary>Полное обновление: список макросов и подсветка кнопок.</summary>
    public void Refresh()
    {
        var profile = _editor.CurrentProfile;

        // Пересобираем список, только если он реально изменился: Refresh дёргается на каждую
        // смену выбранного макроса, а лишняя перестройка зря сбрасывает выбор в меню.
        if (profile is not null
            && MacroChoices.Skip(1).Select(c => c.Macro).SequenceEqual(profile.Macros))
        {
            RefreshAssignments();
            OnPropertyChanged(nameof(ProfileName));
            return;
        }

        // Перестройка списка сбрасывает выбор в выпадающем меню, а это ведёт в сеттер
        // AssignedMacro и стирает привязку. Под флагом сеттер ничего не пишет в модель.
        _isAssigning = true;
        try
        {
            MacroChoices.Clear();
            MacroChoices.Add(new MacroChoice(null));
            if (profile is not null)
            {
                foreach (var macro in profile.Macros)
                {
                    MacroChoices.Add(new MacroChoice(macro));
                }
            }
        }
        finally
        {
            _isAssigning = false;
        }

        RefreshAssignments();
        OnPropertyChanged(nameof(ProfileName));
    }

    /// <summary>
    /// Только подсветка кнопок, без пересборки AvailableMacros. Этот список — источник данных
    /// того самого выпадающего списка, через который идёт назначение: очистить его в момент
    /// смены выбора значит сбросить выбор в null и войти в сеттер повторно.
    /// </summary>
    private void RefreshAssignments()
    {
        var profile = _editor.CurrentProfile;

        foreach (var button in Buttons)
        {
            button.AssignedMacroName = profile is null
                ? null
                : MacroAssignment.Find(profile, button.ButtonId)?.Name;
        }

        OnPropertyChanged(nameof(AssignedMacro));
        OnPropertyChanged(nameof(SelectedChoice));
        NotifyPlaybackSettingsChanged();
    }

    /// <summary>Макрос, назначенный на выбранную кнопку.</summary>
    public Macro? AssignedMacro =>
        SelectedButton is null || _editor.CurrentProfile is null
            ? null
            : MacroAssignment.Find(_editor.CurrentProfile, SelectedButton.ButtonId);

    /// <summary>Выбранный пункт списка. Первый пункт снимает привязку.</summary>
    public MacroChoice? SelectedChoice
    {
        get
        {
            var assigned = AssignedMacro;
            return MacroChoices.FirstOrDefault(c => ReferenceEquals(c.Macro, assigned)) ?? MacroChoices.FirstOrDefault();
        }
        set
        {
            // Защита от повторного входа: любое изменение списка или выбора внутри сеттера
            // приводит WPF обратно сюда же.
            if (_isAssigning || SelectedButton is null || _editor.CurrentProfile is null)
            {
                return;
            }

            _isAssigning = true;
            try
            {
                MacroAssignment.Assign(_editor.CurrentProfile, SelectedButton.ButtonId, value?.Macro);
                _editor.SaveCurrentProfile();
                RefreshAssignments();
            }
            finally
            {
                _isAssigning = false;
            }
        }
    }

    [RelayCommand]
    private void SelectButton(MouseHeroButtonViewModel button)
    {
        foreach (var item in Buttons)
        {
            item.IsSelected = ReferenceEquals(item, button);
        }

        SelectedButton = button;
    }

    // --- Определение кнопки ------------------------------------------------------------
    //
    // Какая физическая кнопка шлёт F17, а какая F22, задаётся в Armoury Crate, и извне это
    // не прочитать. Поэтому вместо угадывания — короткий диалог с самой мышью: пользователь
    // выбирает метку на снимке и нажимает эту кнопку, программа запоминает пришедший код.

    [ObservableProperty]
    private bool _isDetecting;

    /// <summary>Что показывать вместо списка макросов, пока идёт определение.</summary>
    public string DetectHint => AppText.Instance["DetectHint"];

    /// <summary>Чем сейчас отзывается выбранная кнопка — человеческим языком.</summary>
    public string SelectedButtonCode =>
        SelectedButton is null ? string.Empty : DescribeCode(MouseButtonCatalog.CodeFor(SelectedButton.ButtonId));

    private static string DescribeCode(string code)
    {
        if (string.Equals(code, "XBUTTON1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(code, "XBUTTON2", StringComparison.OrdinalIgnoreCase))
        {
            return code.ToUpperInvariant();
        }

        if (MouseButtonCatalog.DecodeKey(code) is not { } key)
        {
            return AppText.Instance["CodeUnknown"];
        }

        string name = System.Windows.Input.KeyInterop.KeyFromVirtualKey(key.VirtualKey) is var wpfKey
                      && wpfKey != System.Windows.Input.Key.None
            ? wpfKey.ToString()
            : $"VK_{key.VirtualKey:X2}";

        string prefix = $"{(key.Ctrl ? "Ctrl+" : string.Empty)}{(key.Alt ? "Alt+" : string.Empty)}{(key.Shift ? "Shift+" : string.Empty)}";
        return prefix + name;
    }

    [RelayCommand]
    private void DetectButton()
    {
        if (SelectedButton is null || IsDetecting)
        {
            return;
        }

        IsDetecting = true;
        _triggers.Suspended = true;
        InputSuppression.DetectionActive = true;
        _input.EventCaptured += OnDetectEvent;
    }

    [RelayCommand]
    private void CancelDetect() => StopDetecting();

    private void StopDetecting()
    {
        if (!IsDetecting)
        {
            return;
        }

        _input.EventCaptured -= OnDetectEvent;
        InputSuppression.DetectionActive = false;
        _triggers.Suspended = false;
        IsDetecting = false;
    }

    /// <summary>Приходит из фонового потока диспетчера событий — работу делаем в UI-потоке.</summary>
    private void OnDetectEvent(RawInputEvent evt)
    {
        string? code = CodeOf(evt);
        if (code is null)
        {
            return;
        }

        _ui.BeginInvoke(() => BindDetectedCode(code));
    }

    /// <summary>
    /// Код кнопки из сырого события: любая клавиша (при желании — с Ctrl/Alt/Shift) с
    /// клавиатурного хука либо XBUTTON1/2 с мыши. Ограничения «только F17–F22» здесь нет:
    /// Armoury Crate даёт назначить не все клавиши, и программа принимает то, что она умеет.
    /// </summary>
    private static string? CodeOf(RawInputEvent evt)
    {
        if (evt.FromSelf)
        {
            return null;
        }

        if (evt.Source == RawInputSource.Keyboard)
        {
            bool isDown = evt.Message is NativeMethods.WM_KEYDOWN or NativeMethods.WM_SYSKEYDOWN;
            if (!isDown || IsModifier(evt.VirtualKey))
            {
                return null;
            }

            // Scan-код и extended — физическое место клавиши: по ним кнопка узнаётся и тогда,
            // когда virtual-key меняется вместе с NumLock.
            var (ctrl, alt, shift) = InputSuppression.ModifierState();
            var key = new MouseButtonCatalog.KeyCode(evt.VirtualKey, ctrl, alt, shift, evt.ScanCode, evt.ExtendedKey);

            return key.Encode();
        }

        return evt.Message == NativeMethods.WM_XBUTTONDOWN
            ? evt.MouseData == NativeMethods.XBUTTON2 ? "XBUTTON2" : "XBUTTON1"
            : null;
    }

    /// <summary>Сами Ctrl/Alt/Shift/Win кнопкой мыши быть не могут — это только модификаторы.</summary>
    private static bool IsModifier(int virtualKey) => InputSuppression.IsModifier(virtualKey);

    private void BindDetectedCode(string code)
    {
        if (SelectedButton is not { } button)
        {
            StopDetecting();
            return;
        }

        var codes = _settings.Settings.ButtonCodes;

        // Один код не может принадлежать двум кнопкам: снимаем его со старой владелицы,
        // иначе две метки начали бы срабатывать от одного нажатия.
        foreach (string owner in codes.Where(p => string.Equals(p.Value, code, StringComparison.OrdinalIgnoreCase))
                     .Select(p => p.Key).ToList())
        {
            codes.Remove(owner);
        }

        if (MouseButtonCatalog.ButtonIdForCode(code) is { } previous && previous != button.ButtonId)
        {
            // Код был закреплён за другой кнопкой по умолчанию — освобождаем её явно,
            // подставив код, которого у неё быть не может, до следующего определения.
            codes[previous] = string.Empty;
        }

        codes[button.ButtonId] = code;
        _settings.Settings.ButtonCodes = codes;
        _settings.Save();
        MouseButtonCatalog.ApplyCodeOverrides(codes);

        StopDetecting();
        OnPropertyChanged(nameof(SelectionTitle));
        OnPropertyChanged(nameof(SelectedButtonCode));
    }

    [RelayCommand]
    private void ResetDetection()
    {
        _settings.Settings.ButtonCodes.Clear();
        _settings.Save();
        MouseButtonCatalog.ApplyCodeOverrides(null);
        StopDetecting();
        OnPropertyChanged(nameof(SelectedButtonCode));
    }

    [RelayCommand]
    private void CloseSelection()
    {
        foreach (var item in Buttons)
        {
            item.IsSelected = false;
        }

        SelectedButton = null;
    }

    partial void OnSelectedButtonChanged(MouseHeroButtonViewModel? value)
    {
        StopDetecting();
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(SelectedButtonCode));
        OnPropertyChanged(nameof(SelectionTitle));
        OnPropertyChanged(nameof(AssignedMacro));
        OnPropertyChanged(nameof(SelectedChoice));
        NotifyPlaybackSettingsChanged();
    }
}
