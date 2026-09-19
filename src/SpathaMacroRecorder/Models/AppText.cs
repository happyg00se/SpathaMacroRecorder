using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Markup;

namespace SpathaMacroRecorder.Models;

/// <summary>
/// Переводы интерфейса. Обращение через индексатор, чтобы смена языка обновляла экран
/// сразу, без перезапуска: сброс индексатора заставляет все привязки перечитать значение.
/// </summary>
public sealed class AppText : INotifyPropertyChanged
{
    public static AppText Instance { get; } = new();

    private string _language = "en";

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Language
    {
        get => _language;
        set
        {
            if (_language == value)
            {
                return;
            }

            _language = value;
            // "Item[]" — сигнал WPF, что все значения индексатора устарели.
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Language)));
        }
    }

    /// <summary>Имя кнопки каталога на языке интерфейса.</summary>
    public static string ButtonName(string? buttonId) => buttonId switch
    {
        "fwd" => Instance["BtnForward"],
        "back" => Instance["BtnBack"],
        "macro1" => Instance["BtnSide1"],
        "macro2" => Instance["BtnSide2"],
        "macro3" => Instance["BtnSide3"],
        "macro4" => Instance["BtnSide4"],
        "macro5" => Instance["BtnSide5"],
        "macro6" => Instance["BtnSide6"],
        _ => Instance["NotAssigned"],
    };

    public string this[string key] =>
        Strings.TryGetValue(key, out var pair)
            ? (_language == "ru" ? pair.Ru : pair.En)
            : key;

    private static readonly Dictionary<string, (string En, string Ru)> Strings = new(StringComparer.Ordinal)
    {
        // Главный экран
        ["Profile"] = ("Profile", "Профиль"),
        ["NoProfile"] = ("No profile", "Нет профиля"),
        ["Settings"] = ("⚙  Settings", "⚙  Настройки"),
        ["Preferences"] = ("Preferences", "Параметры"),
        ["MouseSetup"] = ("Mouse setup", "Настройка мыши"),
        ["Save"] = ("💾  Save", "💾  Сохранить"),
        ["SaveTip"] = ("Save button assignments to the profile", "Сохранить назначения кнопок в профиль"),
        ["Ready"] = ("Ready", "Готов"),
        ["MoveMarkers"] = ("Move markers", "Двигать метки"),
        ["Done"] = ("Done", "Готово"),
        ["Reset"] = ("Reset", "Сбросить"),
        ["ResetTip"] = ("Put the markers back where they started", "Вернуть метки на исходные места"),
        ["DragHint"] = ("Drag the markers onto the buttons", "Перетащите метки на кнопки"),
        ["FaqTip"] = ("How to set this up", "Как всё настроить"),

        // Панель назначения
        ["MacroOnButton"] = ("Macro on this button", "Макрос на этой кнопке"),
        ["RemoveMacro"] = ("— Remove macro —", "— Снять макрос —"),
        ["HowItPlays"] = ("How it plays", "Как проигрывать"),
        ["RepeatCount"] = ("Repeat count", "Число повторов"),
        ["AssignHint"] = ("Create and edit macros in Settings. Side buttons need F17-F22 assigned in Armoury Crate first.",
                          "Макросы создаются в Настройках. Боковым кнопкам нужно заранее назначить F17–F22 в Armoury Crate."),

        // Окно профилей и макросов
        ["EditorTitle"] = ("Profiles and macros", "Профили и макросы"),
        ["Profiles"] = ("Profiles", "Профили"),
        ["Macros"] = ("Macros", "Макросы"),
        ["Steps"] = ("Steps", "Шаги"),
        ["Recording"] = ("Recording", "Запись"),
        ["Record"] = ("● Record", "● Запись"),
        ["Stop"] = ("⏹ Stop", "⏹ Стоп"),
        ["Test"] = ("▶ Test", "▶ Тест"),
        ["RecordHint"] = ("Record keystrokes into the selected macro, then assign it to a mouse button on the main screen.",
                          "Запишите нажатия в выбранный макрос, затем назначьте его на кнопку мыши на главном экране."),
        ["SaveProfile"] = ("💾  Save", "💾  Сохранить"),
        ["SaveProfileTip"] = ("Write the current profile to disk", "Записать текущий профиль на диск"),
        ["Import"] = ("Import", "Импорт"),
        ["Export"] = ("Export", "Экспорт"),
        ["NewProfileTip"] = ("New profile", "Новый профиль"),
        ["RenameTip"] = ("Rename", "Переименовать"),
        ["DuplicateTip"] = ("Duplicate", "Дублировать"),
        ["DeleteTip"] = ("Delete", "Удалить"),
        ["ImportTip"] = ("Import a profile file", "Импортировать файл профиля"),
        ["ExportTip"] = ("Export to a file", "Экспортировать в файл"),
        ["NewMacroTip"] = ("New macro", "Новый макрос"),
        ["SelectAll"] = ("Select all", "Выделить всё"),
        ["SelectAllMacrosTip"] = ("Select all macros (Ctrl+A)", "Выделить все макросы (Ctrl+A)"),
        ["SelectAllStepsTip"] = ("Select all steps (Ctrl+A)", "Выделить все шаги (Ctrl+A)"),
        ["DeleteMacrosTip"] = ("Delete selected macros (Del)", "Удалить выделенные макросы (Del)"),
        ["DeleteStepsTip"] = ("Delete selected steps (Del)", "Удалить выделенные шаги (Del)"),
        ["Add"] = ("Add", "Добавить"),
        ["AddStepTip"] = ("Clone the selected step", "Клонировать выбранный шаг"),
        ["Delay0"] = ("Delay 0", "Задержка 0"),
        ["Delay0Tip"] = ("Set delay to 0 for the selected steps (or all of them)",
                         "Обнулить задержку у выделенных шагов (или у всех сразу)"),
        ["MoveUpTip"] = ("Move up", "Переместить вверх"),
        ["MoveDownTip"] = ("Move down", "Переместить вниз"),
        ["Delete"] = ("Delete", "Удалить"),
        ["ColNumber"] = ("#", "№"),
        ["ColType"] = ("Type", "Тип"),
        ["ColKey"] = ("Key / Button", "Клавиша"),
        ["ColAction"] = ("Action", "Действие"),
        ["ColDelay"] = ("Delay before step, ms", "Задержка перед шагом, мс"),

        // Параметры
        ["PreferencesTitle"] = ("Settings", "Параметры"),
        ["PanicKey"] = ("Panic key", "Клавиша аварийной остановки"),
        ["PanicKeyHint"] = ("Instantly stops playback and releases everything the macro is holding down.",
                            "Мгновенно останавливает воспроизведение и отпускает всё, что удерживает макрос."),
        ["PauseKey"] = ("Pause recording key", "Клавиша паузы записи"),
        ["PauseKeyHint"] = ("Pauses and resumes recording without stopping it. This key is never written into a macro.",
                            "Ставит запись на паузу и снимает с неё. В макрос эта клавиша не попадает."),
        ["PressKey"] = ("Press a key…", "Нажмите клавишу…"),
        ["Waiting"] = ("Waiting…", "Ожидание…"),
        ["PressAnyKey"] = ("Press any key", "Нажмите любую клавишу"),
        // Строка диагностики внизу главного окна.
        ["DiagHooksOn"] = ("input capture on", "перехват включён"),
        ["DiagHooksOff"] = ("INPUT CAPTURE OFF", "ПЕРЕХВАТ НЕ ВКЛЮЧЁН"),
        ["DiagCompanion"] = ("closes together with {0}", "закроется вместе с {0}"),
        ["DiagNoPress"] = ("no mouse button signal yet", "сигналов от кнопок мыши ещё не было"),
        ["DiagLastPress"] = ("last button: {0}, {1} s ago", "последняя кнопка: {0}, {1} с назад"),
        ["DiagInjected"] = (" (from software)", " (от программы)"),
        ["DiagUnbound"] = ("no marker", "не привязана к метке"),
        ["DiagNoMacro"] = ("no macro on this button", "на этой кнопке нет макроса"),
        ["DiagMacroStarted"] = ("macro “{0}” started", "запущен макрос «{0}»"),

        // Пауза между нажатиями при воспроизведении.
        ["KeyGap"] = ("Pause between key presses", "Пауза между нажатиями"),
        ["KeyGapHint"] = ("Games read the keyboard once per frame and miss a press and release sent in the same instant. 30 ms suits Helldivers 2; if stratagems still fail at low FPS, set 50. Larger delays from the steps table still apply. 0 turns it off.",
                          "Игры читают клавиатуру раз в кадр и не видят нажатие и отпускание, отправленные в одно мгновение. Для Helldivers 2 подходит 30 мс; если при низком FPS стратагемы всё равно не вводятся — поставьте 50. Задержки из таблицы шагов больше этого значения работают как обычно. 0 — выключить."),
        ["KeyGapUnit"] = ("ms", "мс"),

        // Определение кнопок и работа вместе с игрой.
        ["Detect"] = ("Detect", "Определить"),
        ["CodeNow"] = ("Responds to:", "Отзывается на:"),
        ["CodeUnknown"] = ("not detected yet", "ещё не определена"),
        ["DetectTip"] = ("Press this button on the mouse so the program learns which one it is",
                         "Нажмите эту кнопку на мыши — программа запомнит, какая она"),
        ["DetectHint"] = ("Press the selected button on the mouse\u2026", "Нажмите выбранную кнопку на мыши\u2026"),
        ["DetectCancel"] = ("Cancel", "Отмена"),
        ["DetectReset"] = ("Reset detection", "Сбросить определение"),
        ["DetectResetTip"] = ("Forget the detected buttons and go back to the default order",
                              "Забыть определённые кнопки и вернуться к порядку по умолчанию"),
        ["SteamSection"] = ("Start together with the game (Steam)", "Запуск вместе с игрой (Steam)"),
        ["SteamHint"] = ("Steam will start Helldivers 2 through this program: the program opens with the game and closes when the game closes. Nothing runs in the background and nothing is added to Windows startup, and you can still open the program yourself at any time. The line already ends with --use-d3d12, so the game runs on DirectX 12. In Steam: Library → Helldivers 2 → Properties → General → Launch options — paste this line:",
                         "Steam будет запускать Helldivers 2 через эту программу: программа откроется вместе с игрой и закроется, когда игра закроется. Ничего не висит в фоне и не прописывается в автозапуск Windows, а открыть программу самому можно в любой момент. В конце строки уже стоит --use-d3d12 — игра пойдёт на DirectX 12. В Steam: Библиотека → Helldivers 2 → Свойства → Общие → Параметры запуска — вставьте эту строку:"),
        ["SteamCopy"] = ("Copy", "Скопировать"),
        ["SteamCopied"] = ("Copied", "Скопировано"),
        ["SteamMoveNote"] = ("Need DirectX 11 instead — replace --use-d3d12 at the end of the line with --use-d3d11. If your launch options already had something else, keep it: add it to the end of the line, after %command%. If you move the program to another folder, copy the line again.",
                             "Нужен DirectX 11 — замените --use-d3d12 в конце строки на --use-d3d11. Если в параметрах запуска уже было что-то своё, не потеряйте это: допишите в конец строки, после %command%. Если перенесёте программу в другую папку — скопируйте строку заново."),
        ["SteamTempWarning"] = ("The program is running from a temporary folder, probably straight from the archive. Unpack it to a permanent folder such as C:\\Spatha, start it from there and copy the line again — otherwise Steam will not find it next time.",
                                "Программа запущена из временной папки — видимо, прямо из архива. Распакуйте её в постоянную папку, например C:\\Spatha, запустите оттуда и скопируйте строку заново — иначе в следующий раз Steam её не найдёт."),
        ["GameLaunchFailedTitle"] = ("Game launch", "Запуск игры"),
        ["GameLaunchFailed"] = ("Could not start the game:\n{0}", "Не удалось запустить игру:\n{0}"),
        ["NotSet"] = ("Not set", "Не задана"),
        ["Playback"] = ("Playback", "Воспроизведение"),
        ["DesktopMode"] = ("Desktop mode (absolute mouse positioning)",
                           "Режим рабочего стола (абсолютное позиционирование мыши)"),
        ["DesktopModeHint"] = ("Leave this off for games: they read raw relative movement. Turn it on only for macros outside of games.",
                               "Для игр держите выключенным: они читают относительное движение. Включайте только для макросов вне игр."),
        ["Startup"] = ("Startup", "Автозапуск"),
        ["Language"] = ("Language", "Язык интерфейса"),
        ["LanguageEnglish"] = ("English", "English"),
        ["LanguageRussian"] = ("Русский", "Русский"),
        ["LanguageNote"] = ("specially for r4v3r_63", "специально для r4v3r_63"),
        ["Cancel"] = ("Cancel", "Отмена"),
        ["SaveShort"] = ("Save", "Сохранить"),
        ["SettingsTip"] = ("Profiles, macros and steps", "Профили, макросы и шаги"),
        ["Side1"] = ("Side 1  →  F17", "Боковая 1  →  F17"),
        ["Side2"] = ("Side 2  →  F18", "Боковая 2  →  F18"),
        ["Side3"] = ("Side 3  →  F19", "Боковая 3  →  F19"),
        ["Side4"] = ("Side 4  →  F20", "Боковая 4  →  F20"),
        ["Side5"] = ("Side 5  →  F21", "Боковая 5  →  F21"),
        ["Side6"] = ("Side 6  →  F22", "Боковая 6  →  F22"),
        ["BtnSide1"] = ("Side 1", "Боковая 1"),
        ["BtnSide2"] = ("Side 2", "Боковая 2"),
        ["BtnSide3"] = ("Side 3", "Боковая 3"),
        ["BtnSide4"] = ("Side 4", "Боковая 4"),
        ["BtnSide5"] = ("Side 5", "Боковая 5"),
        ["BtnSide6"] = ("Side 6", "Боковая 6"),
        ["BtnForward"] = ("Forward", "Вперёд"),
        ["BtnBack"] = ("Back", "Назад"),
        ["NotAssigned"] = ("Not assigned", "Не назначено"),
        ["DeleteMacroQ"] = ("Delete macro \"{0}\"?", "Удалить макрос «{0}»?"),
        ["DeleteMacrosQ"] = ("Delete {0} selected macros?", "Удалить выделенные макросы ({0})?"),
        ["DeleteMacroTitle"] = ("Delete macros", "Удаление макросов"),
        ["DeleteProfileQ"] = ("Delete profile \"{0}\"? This cannot be undone.",
                              "Удалить профиль «{0}»? Действие необратимо."),
        ["DeleteProfileTitle"] = ("Delete profile", "Удаление профиля"),
        ["SavedTitle"] = ("Saved", "Сохранено"),
        ["SavedText"] = ("Profile \"{0}\" saved.", "Профиль «{0}» сохранён."),
        ["NewProfile"] = ("New profile", "Новый профиль"),
        ["ProfileName"] = ("Profile name:", "Имя профиля:"),
        ["RenameProfile"] = ("Rename profile", "Переименование профиля"),
        ["NewName"] = ("New name:", "Новое имя:"),
        ["DuplicateProfile"] = ("Duplicate profile", "Дублирование профиля"),
        ["CopyName"] = ("Copy name:", "Имя копии:"),
        ["CopySuffix"] = ("copy", "копия"),
        ["NewMacro"] = ("New macro", "Новый макрос"),
        ["MacroName"] = ("Macro name:", "Имя макроса:"),
        ["ErrorTitle"] = ("Error", "Ошибка"),
        ["ProfileFilter"] = ("SpathaMacroRecorder profile (*.json)|*.json",
                             "Профиль SpathaMacroRecorder (*.json)|*.json"),
        ["ImportFailed"] = ("Failed to import the file:", "Не удалось импортировать файл:"),
        ["ProfileBroken"] = ("Profile file is corrupted and was skipped:",
                             "Файл профиля повреждён и был пропущен:"),
        ["ProfileLoadError"] = ("Profile load error", "Ошибка загрузки профиля"),
        ["Close"] = ("Close", "Закрыть"),

        // Статусы и подписи
        ["StatusReady"] = ("Ready", "Готов"),
        ["StatusRecording"] = ("Recording", "Идёт запись"),
        ["StatusPaused"] = ("Recording paused", "Запись на паузе"),
        ["StatusPlaying"] = ("Playing", "Воспроизведение"),
        ["ModeOnce"] = ("Once", "Один раз"),
        ["ModeHold"] = ("Hold", "Пока зажата"),
        ["ModeRepeat"] = ("Repeat", "Повторять"),

        // Окно настройки мыши
        ["MouseSetupTitle"] = ("Mouse setup", "Настройка мыши"),
        ["MouseSetupHeader"] = ("One-time setup for the side buttons",
                                "Разовая настройка боковых кнопок"),
        ["MouseSetupP1"] = ("Windows lets an application see only five mouse buttons: left, right, wheel click, forward and back. The six extra buttons on the side block are sent by the mouse as its own codes, which no application can read directly.",
                            "Windows показывает приложению только пять кнопок мыши: левую, правую, нажатие колеса, вперёд и назад. Шесть дополнительных кнопок бокового блока мышь передаёт своими кодами, и напрямую их не прочитать."),
        ["MouseSetupP2"] = ("The way around it: assign the F17-F22 keys to those buttons in Armoury Crate. These keys exist in the HID specification, Windows recognises them, and no game uses them. The assignment is stored in the mouse itself, so Armoury Crate can be closed afterwards - and this app will keep seeing the buttons.",
                            "Обходной путь: назначьте на эти кнопки клавиши F17–F22 в Armoury Crate. Они есть в спецификации HID, Windows их распознаёт, и ни одна игра их не занимает. Назначение хранится в самой мыши, поэтому Armoury Crate потом можно закрыть — приложение продолжит видеть кнопки."),
        ["MouseSetupAssign"] = ("In Armoury Crate assign:", "Назначьте в Armoury Crate:"),
        ["MouseSetupDpi"] = ("DPI switch  →  F23  (optional)", "Переключатель DPI  →  F23  (по желанию)"),
        ["MouseSetupP3"] = ("The left, right, wheel, forward and back buttons need no setup - they work out of the box.",
                            "Левая, правая, колесо, вперёд и назад настройки не требуют — они работают сразу."),
        ["MouseSetupP4"] = ("If macros do not fire inside a game, try running this app as administrator: Windows blocks input sent to windows running with higher privileges.",
                            "Если макросы не срабатывают в игре, запустите приложение от имени администратора: Windows не пропускает ввод в окна с более высокими правами."),

        // Справка
        ["FaqTitle"] = ("How it works", "Как это работает"),
        ["FaqSubtitle"] = ("Record a sequence of keystrokes and fire it from a mouse button.",
                           "Запишите последовательность нажатий и запускайте её кнопкой мыши."),
        ["Faq1"] = ("1. One-time mouse setup", "1. Разовая настройка мыши"),
        ["Faq1Text"] = ("Windows only lets an application see five mouse buttons. The six side buttons send codes no application can read, so in Armoury Crate assign F17-F22 to them (Side 1 = F17 … Side 6 = F22). The assignment lives in the mouse, so Armoury Crate can be closed afterwards. Forward and Back need no setup.",
                        "Windows показывает приложению лишь пять кнопок мыши. Шесть боковых передают коды, которые не прочитать, поэтому назначьте на них F17–F22 в Armoury Crate (Боковая 1 = F17 … Боковая 6 = F22). Назначение хранится в мыши, Armoury Crate потом можно закрыть. Вперёд и Назад настройки не требуют."),
        ["Faq2"] = ("2. Create a macro — Settings", "2. Создание макроса — Настройки"),
        ["Faq2Text"] = ("Open Settings. Create a profile on the left, then a macro in the middle with +. Select the macro, press Record, type the keys you want, then press Stop. The steps appear in the table, where you can reorder them, insert pauses, set delays or delete them with Delete. Test replays the macro. Only keyboard input is recorded.",
                        "Откройте Настройки. Слева создайте профиль, в центре кнопкой + — макрос. Выберите макрос, нажмите Запись, наберите нужные клавиши, нажмите Стоп. Шаги появятся в таблице: их можно переставлять, вставлять паузы, задавать задержки и удалять клавишей Delete. Тест проигрывает макрос. Записывается только клавиатура."),
        ["Faq3"] = ("3. Put it on a button — main screen", "3. Назначение на кнопку — главный экран"),
        ["Faq3Text"] = ("Close Settings and click a marker on the mouse. Eight buttons can be configured: the six side ones plus Forward and Back. Pick a macro from the list, then press Save. A button with a macro on it glows red; an empty one stays grey.",
                        "Закройте Настройки и щёлкните метку на мыши. Настраиваются восемь кнопок: шесть боковых плюс Вперёд и Назад. Выберите макрос из списка и нажмите Сохранить. Кнопка с макросом светится красным, свободная остаётся серой."),
        ["Faq4"] = ("4. Using it", "4. Работа"),
        ["Faq4Text"] = ("Just press the button on the mouse — the macro runs in whatever window is in focus. The app must stay running, but the window can be closed to the taskbar. Keys are sent as scan codes, which is what games reading DirectInput actually accept.",
                        "Просто нажмите кнопку на мыши — макрос отработает в том окне, где сейчас фокус. Приложение должно быть запущено, но окно можно свернуть. Клавиши отправляются scan-кодами — именно их принимают игры, читающие DirectInput."),
        ["FaqTrouble"] = ("If something does not work", "Если что-то не работает"),
        ["FaqTroubleText"] = ("A macro does nothing inside a game: run this app as administrator — Windows blocks input sent to windows with higher privileges. A side button does nothing: check that F17-F22 are still assigned in Armoury Crate. Playback will not stop: press the panic key set in Preferences; it stops playback and releases everything held down.",
                              "Макрос не работает в игре: запустите приложение от имени администратора — Windows не пропускает ввод в окна с более высокими правами. Не работает боковая кнопка: проверьте, что F17–F22 назначены в Armoury Crate. Воспроизведение не останавливается: нажмите клавишу аварийной остановки из Параметров — она гасит проигрывание и отпускает всё зажатое."),
        ["GotIt"] = ("Got it", "Понятно"),
    };
}

/// <summary>
/// Разметка вида {loc:Tr Macros}. Возвращает привязку к индексатору, поэтому текст меняется
/// сразу при смене языка, а не только при следующем открытии окна.
/// </summary>
public sealed class TrExtension : MarkupExtension
{
    public TrExtension()
    {
    }

    public TrExtension(string key) => Key = key;

    public string Key { get; set; } = string.Empty;

    public override object? ProvideValue(IServiceProvider serviceProvider)
    {
        var binding = new Binding($"[{Key}]")
        {
            Source = AppText.Instance,
            Mode = BindingMode.OneWay,
        };

        return binding.ProvideValue(serviceProvider);
    }
}
