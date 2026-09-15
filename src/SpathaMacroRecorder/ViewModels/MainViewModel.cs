using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SpathaMacroRecorder.Models;
using SpathaMacroRecorder.Services;
using SpathaMacroRecorder.Views;

namespace SpathaMacroRecorder.ViewModels;

/// <summary>
/// Корневая ViewModel главного окна: композиция левой (профили) и центральной (макросы/шаги)
/// панелей + правая панель (Запись/Стоп/Тест, режим воспроизведения) + статус-бар.
/// </summary>
internal partial class MainViewModel : ObservableObject
{
    private readonly MacroRecorder _macroRecorder;
    private readonly MacroPlayer _macroPlayer;
    private readonly TriggerBindingService _triggerBinding;
    private readonly AppSettingsService _settingsService;
    private readonly GlobalKeyboardHook _keyboardHook;
    private readonly GlobalMouseHook _mouseHook;
    private readonly GameWatcher _gameWatcher;
    private readonly InputDiagnostics _diagnostics;
    private readonly DispatcherTimer _diagnosticsTimer;
    private readonly Dispatcher _dispatcher;

    private EditorWindow? _editorWindow;
    private Macro? _recordingTarget;

    public ProfileListViewModel ProfileList { get; }
    public MacroEditorViewModel MacroEditor { get; }
    public MouseHeroViewModel MouseHero { get; }

    /// <summary>Список для ComboBox режимов — отдаём из кода, а не через ObjectDataProvider в XAML.</summary>
    public IReadOnlyList<PlaybackMode> PlaybackModes { get; } = Enum.GetValues<PlaybackMode>();

    [ObservableProperty]
    private string _statusText = AppText.Instance["StatusReady"];

    /// <summary>Строка диагностики внизу окна — см. InputDiagnostics.</summary>
    [ObservableProperty]
    private string _diagnosticsText = string.Empty;

    public string PanicKeyLabel =>
        $"Panic key: {SettingsView.DescribeKey(_settingsService.Settings.PanicKeyVirtualKey)}";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartRecordingCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopCommand))]
    [NotifyCanExecuteChangedFor(nameof(TestCommand))]
    private bool _isRecording;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartRecordingCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopCommand))]
    [NotifyCanExecuteChangedFor(nameof(TestCommand))]
    private bool _isPlaying;

    public MainViewModel(
        ProfileListViewModel profileList,
        MacroEditorViewModel macroEditor,
        MacroRecorder macroRecorder,
        MacroPlayer macroPlayer,
        TriggerBindingService triggerBinding,
        AppSettingsService settingsService,
        GlobalKeyboardHook keyboardHook,
        GlobalMouseHook mouseHook,
        GameWatcher gameWatcher,
        InputDiagnostics diagnostics,
        MouseHeroViewModel mouseHero)
    {
        ProfileList = profileList;
        MacroEditor = macroEditor;
        MouseHero = mouseHero;
        _macroRecorder = macroRecorder;
        _macroPlayer = macroPlayer;
        _triggerBinding = triggerBinding;
        _settingsService = settingsService;
        _keyboardHook = keyboardHook;
        _mouseHook = mouseHook;
        _gameWatcher = gameWatcher;
        _diagnostics = diagnostics;
        _dispatcher = Application.Current.Dispatcher;

        _diagnosticsTimer = new DispatcherTimer(
            TimeSpan.FromMilliseconds(500), DispatcherPriority.Background, (_, _) => RefreshDiagnostics(), _dispatcher);
        RefreshDiagnostics();

        _macroPlayer.PlaybackCompleted += OnPlaybackCompleted;

        // Panic-клавиша дёргает плеер напрямую из callback'а хука — без похода в UI-поток,
        // иначе реакция зависела бы от занятости интерфейса (тех. пункт 6 задания).
        _keyboardHook.PanicRequested = _macroPlayer.Stop;
        _keyboardHook.PauseRecordingRequested = ToggleRecordingPause;
        ApplyHotkeys();

        ProfileList.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ProfileListViewModel.SelectedProfile))
            {
                MacroEditor.SetProfile(ProfileList.SelectedProfile);
            }
        };
        MacroEditor.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MacroEditorViewModel.SelectedMacro))
            {
                StartRecordingCommand.NotifyCanExecuteChanged();
                TestCommand.NotifyCanExecuteChanged();
            }
        };

        _triggerBinding.TriggerPressed += OnTriggerPressed;
        _triggerBinding.TriggerReleased += OnTriggerReleased;

        MacroEditor.SetProfile(ProfileList.SelectedProfile);
    }

    /// <summary>
    /// Физическая кнопка нажата: ищем в текущем профиле макрос с таким триггером и запускаем.
    /// Во время записи триггеры игнорируются — иначе запись сама себя запускала бы.
    /// </summary>
    private void OnTriggerPressed(string buttonId)
    {
        if (IsRecording)
        {
            return;
        }

        // BeginInvoke, а не Invoke: событие приходит из потока обработки ввода, и блокировать
        // его до ответа интерфейса нельзя. Поиск макроса тоже уходит в UI-поток — коллекция
        // Macros связана с интерфейсом, обходить её из чужого потока небезопасно.
        _dispatcher.BeginInvoke(() =>
        {
            if (IsRecording || MacroEditor.CurrentProfile is not { } profile)
            {
                return;
            }

            var macro = profile.Macros.FirstOrDefault(m => m.Trigger?.ButtonId == buttonId);
            if (macro is null || macro.Steps.Count == 0)
            {
                _diagnostics.ReportTrigger(buttonId, null);
                return;
            }

            _diagnostics.ReportTrigger(buttonId, macro.Name);

            IsPlaying = true;
            StatusText = AppText.Instance["StatusPlaying"];
            _macroPlayer.Start(macro, _settingsService.Settings.UseAbsoluteMovement, _settingsService.Settings.KeyGapMs);
        });
    }

    /// <summary>
    /// Собирает строку диагностики: сборка · перехват · закроется ли вместе с игрой · последний сигнал кнопки
    /// мыши и во что он превратился · запущен ли макрос.
    /// </summary>
    private void RefreshDiagnostics()
    {
        var text = AppText.Instance;
        var parts = new List<string>
        {
            AppInfo.ReleaseName,
            _keyboardHook.IsInstalled && _mouseHook.IsInstalled ? text["DiagHooksOn"] : text["DiagHooksOff"],
        };

        if (_gameWatcher.ProcessName is { } game)
        {
            parts.Add(string.Format(text["DiagCompanion"], game));
        }

        if (_diagnostics.LastPress is { } press)
        {
            string key = press.IsMouse ? $"XBUTTON{press.VirtualKey}" : SettingsView.DescribeKey(press.VirtualKey);
            if (press.Injected)
            {
                key += text["DiagInjected"];
            }

            string target = press.ButtonId is null ? text["DiagUnbound"] : AppText.ButtonName(press.ButtonId);
            int secondsAgo = Math.Max(0, (int)(DateTime.UtcNow - press.AtUtc).TotalSeconds);
            parts.Add(string.Format(text["DiagLastPress"], $"{key} → {target}", secondsAgo));

            if (_diagnostics.LastOutcome is { } outcome && outcome.AtUtc >= press.AtUtc.AddSeconds(-1))
            {
                parts.Add(outcome.MacroName is null
                    ? text["DiagNoMacro"]
                    : string.Format(text["DiagMacroStarted"], outcome.MacroName));
            }
        }
        else
        {
            parts.Add(text["DiagNoPress"]);
        }

        DiagnosticsText = string.Join("  ·  ", parts);
    }

    /// <summary>Отпускание триггера гасит только режим hold — он крутится, пока кнопка зажата.</summary>
    private void OnTriggerReleased(string buttonId)
    {
        _dispatcher.BeginInvoke(() =>
        {
            var macro = MacroEditor.CurrentProfile?.Macros
                .FirstOrDefault(m => m.Trigger?.ButtonId == buttonId);
            if (macro is { PlaybackMode: PlaybackMode.Hold })
            {
                _macroPlayer.Stop();
            }
        });
    }

    [RelayCommand(CanExecute = nameof(CanStartRecording))]
    private void StartRecording()
    {
        if (MacroEditor.SelectedMacro is null)
        {
            return;
        }

        // Запоминаем макрос сразу: если во время записи переключиться на другой, шаги должны
        // лечь в тот, для которого запись начинали.
        _recordingTarget = MacroEditor.SelectedMacro;
        _macroRecorder.Start();
        IsRecording = true;
        StatusText = AppText.Instance["StatusRecording"];
    }

    [RelayCommand(CanExecute = nameof(CanStop))]
    private void Stop()
    {
        if (IsRecording)
        {
            var recorded = _macroRecorder.Stop();
            IsRecording = false;

            if (_recordingTarget is { } target)
            {
                target.Steps.Clear();
                foreach (var step in recorded)
                {
                    target.Steps.Add(step);
                }

                MacroEditor.SaveCurrentProfile();
                MouseHero.Refresh();
            }

            _recordingTarget = null;
        }

        if (IsPlaying)
        {
            _macroPlayer.Stop();
        }

        StatusText = AppText.Instance["StatusReady"];
    }

    [RelayCommand(CanExecute = nameof(CanTest))]
    private void Test()
    {
        if (MacroEditor.SelectedMacro is null)
        {
            return;
        }

        IsPlaying = true;
        StatusText = AppText.Instance["StatusPlaying"];
        _macroPlayer.Start(MacroEditor.SelectedMacro, _settingsService.Settings.UseAbsoluteMovement, _settingsService.Settings.KeyGapMs);
    }

    [RelayCommand]
    private void OpenMouseSetup() => MouseSetupView.Show(Application.Current.MainWindow);

    [RelayCommand]
    private void OpenFaq() => FaqView.Show(Application.Current.MainWindow);

    /// <summary>Окно профилей/макросов — одно на всё приложение, повторный клик поднимает его.</summary>
    [RelayCommand]
    private void OpenEditor()
    {
        if (_editorWindow is { IsLoaded: true })
        {
            _editorWindow.Activate();
            return;
        }

        _editorWindow = new EditorWindow(this) { Owner = Application.Current.MainWindow };
        _editorWindow.Closed += (_, _) =>
        {
            _editorWindow = null;
            MouseHero.Refresh();
        };
        _editorWindow.Show();
    }

    [RelayCommand]
    private void OpenSettings()
    {
        if (!SettingsView.Show(Application.Current.MainWindow, _settingsService.Settings))
        {
            return;
        }

        _settingsService.Save();
        ApplyHotkeys();
        OnPropertyChanged(nameof(PanicKeyLabel));
    }

    /// <summary>
    /// Раздаёт служебные клавиши: хуку — чтобы он их ловил, рекордеру — чтобы он их не записывал.
    /// </summary>
    private void ApplyHotkeys()
    {
        var settings = _settingsService.Settings;

        _keyboardHook.PanicVirtualKey = settings.PanicKeyVirtualKey;
        _keyboardHook.PauseRecordingVirtualKey = settings.PauseRecordingVirtualKey;

        _macroRecorder.PanicVirtualKey = settings.PanicKeyVirtualKey;
        _macroRecorder.PauseVirtualKey = settings.PauseRecordingVirtualKey;
    }

    /// <summary>Пауза/продолжение записи горячей клавишей. Приходит из callback'а хука.</summary>
    private void ToggleRecordingPause() => _dispatcher.BeginInvoke(() =>
    {
        if (!IsRecording)
        {
            return;
        }

        if (_macroRecorder.IsPaused)
        {
            _macroRecorder.Resume();
            StatusText = AppText.Instance["StatusRecording"];
        }
        else
        {
            _macroRecorder.Pause();
            StatusText = AppText.Instance["StatusPaused"];
        }
    });

    private void OnPlaybackCompleted()
    {
        // MacroPlayer вызывает событие из своего потока — в UI лезем только через Dispatcher.
        _dispatcher.BeginInvoke(() =>
        {
            IsPlaying = false;
            if (!IsRecording)
            {
                StatusText = AppText.Instance["StatusReady"];
            }
        });
    }

    private bool HasSelectedMacro => MacroEditor.SelectedMacro is not null;
    private bool CanStartRecording => !IsRecording && !IsPlaying && MacroEditor.SelectedMacro is not null;
    private bool CanStop => IsRecording || IsPlaying;
    private bool CanTest => !IsRecording && !IsPlaying && MacroEditor.SelectedMacro is not null;
}
