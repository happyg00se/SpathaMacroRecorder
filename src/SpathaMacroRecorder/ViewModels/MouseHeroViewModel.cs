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

/// <summary>
/// Пункт списка назначения: снятие привязки, макрос профиля или встроенная стратагема,
/// макроса которой в профиле ещё нет.
/// </summary>
internal sealed class MacroChoice
{
    private MacroChoice(Macro? macro, Stratagem? stratagem, bool isCurrent)
    {
        Macro = macro;
        Stratagem = stratagem;
        IsCurrent = isCurrent;
    }

    public static MacroChoice Remove() => new(null, null, false);

    public static MacroChoice ForMacro(Macro macro, bool isCurrent) =>
        new(macro, StratagemCatalog.ForMacro(macro), isCurrent);

    public static MacroChoice ForStratagem(Stratagem stratagem) => new(null, stratagem, false);

    public Macro? Macro { get; }

    public Stratagem? Stratagem { get; }

    public bool IsRemove => Macro is null && Stratagem is null;

    /// <summary>Этот пункт сейчас и висит на кнопке.</summary>
    public bool IsCurrent { get; }

    public string Label => Macro?.Name ?? Stratagem?.Name ?? AppText.Instance["RemoveMacro"];

    /// <summary>Вторая строка: группа и код стрелками — у стратагем, «свой макрос» — у прочих.</summary>
    public string Details => Stratagem is { } stratagem
        ? $"{AppText.Instance["StratagemGroup" + stratagem.Group]}  {stratagem.Arrows}"
        : Macro is null ? string.Empty : AppText.Instance["OwnMacro"];

    public bool HasDetails => Details.Length > 0;

    /// <summary>Значок стратагемы; у своих макросов и у снятия привязки его нет.</summary>
    public string? IconUri => Stratagem?.IconUri;

    public bool HasIcon => Stratagem is not null;
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

    /// <summary>
    /// Пункты списка под строкой поиска: снятие привязки, макросы профиля, затем стратагемы,
    /// которых в профиле ещё нет. Выбор стратагемы сам создаёт её макрос.
    /// </summary>
    public ObservableCollection<MacroChoice> MacroChoices { get; } = [];

    [ObservableProperty]
    private string _searchText = string.Empty;

    partial void OnSearchTextChanged(string value) => RebuildChoices();

    [ObservableProperty]
    private MouseHeroButtonViewModel? _selectedButton;

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
        RefreshAssignments();
        OnPropertyChanged(nameof(ProfileName));
    }

    /// <summary>Подсветка кнопок и всё, что зависит от макроса на выбранной кнопке.</summary>
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
        OnPropertyChanged(nameof(AssignedMacroTitle));
        OnPropertyChanged(nameof(AssignedIconUri));
        OnPropertyChanged(nameof(HasAssignedIcon));
        NotifyPlaybackSettingsChanged();
        RebuildChoices();
    }

    private void RebuildChoices()
    {
        var profile = _editor.CurrentProfile;
        var assigned = AssignedMacro;
        string query = SearchText;

        MacroChoices.Clear();
        if (profile is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(query) && assigned is not null)
        {
            MacroChoices.Add(MacroChoice.Remove());
        }

        var inProfile = new HashSet<Stratagem>();
        foreach (var macro in profile.Macros)
        {
            if (StratagemCatalog.ForMacro(macro) is { } stratagem)
            {
                inProfile.Add(stratagem);
            }

            if (StratagemCatalog.Matches(macro.Name, query))
            {
                MacroChoices.Add(MacroChoice.ForMacro(macro, ReferenceEquals(macro, assigned)));
            }
        }

        foreach (var stratagem in StratagemCatalog.Search(query).Where(s => !inProfile.Contains(s)))
        {
            MacroChoices.Add(MacroChoice.ForStratagem(stratagem));
        }

        OnPropertyChanged(nameof(HasNoMatches));
    }

    /// <summary>Макрос, назначенный на выбранную кнопку.</summary>
    public Macro? AssignedMacro =>
        SelectedButton is null || _editor.CurrentProfile is null
            ? null
            : MacroAssignment.Find(_editor.CurrentProfile, SelectedButton.ButtonId);

    /// <summary>Что висит на кнопке сейчас — строкой над поиском.</summary>
    public string AssignedMacroTitle => AssignedMacro?.Name ?? AppText.Instance["NotAssigned"];

    /// <summary>Значок стратагемы на кнопке, если на ней стратагема.</summary>
    public string? AssignedIconUri => AssignedMacro is { } macro ? StratagemCatalog.ForMacro(macro)?.IconUri : null;

    public bool HasAssignedIcon => AssignedIconUri is not null;

    public bool HasNoMatches => MacroChoices.Count == 0 && _editor.CurrentProfile is not null;

    /// <summary>
    /// Назначение по клику на пункт списка. Стратагема, которой ещё нет в профиле, сначала
    /// становится обычным макросом профиля — дальше его можно править в Настройках.
    /// </summary>
    [RelayCommand]
    private void AssignChoice(MacroChoice? choice)
    {
        if (choice is null || SelectedButton is null || _editor.CurrentProfile is not { } profile)
        {
            return;
        }

        var macro = choice.Macro;
        if (macro is null && choice.Stratagem is { } stratagem)
        {
            macro = StratagemCatalog.GetOrAddMacro(profile, stratagem);
        }

        MacroAssignment.Assign(profile, SelectedButton.ButtonId, macro);
        _editor.SaveCurrentProfile();
        SearchText = string.Empty;
        RefreshAssignments();
    }

    /// <summary>Enter в строке поиска назначает первое, что нашлось.</summary>
    [RelayCommand]
    private void AssignFirstMatch()
    {
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            AssignChoice(MacroChoices.FirstOrDefault(c => !c.IsRemove));
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
        SearchText = string.Empty;
        RefreshAssignments();
    }
}
