using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using SpathaMacroRecorder.Native;
using SpathaMacroRecorder.Services;
using SpathaMacroRecorder.ViewModels;

namespace SpathaMacroRecorder;

public partial class App : Application
{
    private static readonly string AppDataDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SpathaMacroRecorder");

    private static readonly string CrashFilePath = Path.Combine(AppDataDirectory, "crash.txt");

    private IHost? _host;
    private bool _hostStarted;

    /// <summary>
    /// Процесс игры, вместе с которой живёт эта копия программы (её запустил Steam, либо она
    /// перезапущена обновлением); null — программа открыта вручную.
    /// </summary>
    internal static string? CompanionGame { get; private set; }

    private string? _gameLaunchError;
    private static bool _errorReported;

    protected override void OnStartup(StartupEventArgs e)
    {
        // Ставится первым делом: у WinExe нет консоли, а системный диалог об ошибке часто
        // подавлен — без этих обработчиков любое падение при старте выглядит как "ничего
        // не произошло", без единого сообщения.
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;

        base.OnStartup(e);

        bool selfTest = e.Args.Contains("--self-test", StringComparer.OrdinalIgnoreCase);

        // Запуск из Steam («--game %command%»): игра стартует первым делом, чтобы программа её
        // не задерживала — окно и хуки поднимаются уже параллельно с загрузкой игры.
        if (!selfTest && GameLaunch.Parse(e.Args) is { } game)
        {
            try
            {
                using var launched = GameLaunch.Start(game);
                CompanionGame = game.ProcessName;
            }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or FileNotFoundException)
            {
                _gameLaunchError = $"{game.ExecutablePath}\n\n{ex.Message}";
            }
        }

        if (!selfTest && GameLaunch.ParseWatch(e.Args) is { } watched)
        {
            CompanionGame = watched;
        }

        if (!selfTest)
        {
            TakeOverPreviousInstances();
            UpdateService.CleanupAfterUpdate();
        }

        try
        {
            StartApplication(selfTest);
        }
        catch (Exception ex)
        {
            ReportFatalError("Failed to start the application", ex);
            Shutdown(1);
        }
    }

    private void StartApplication(bool selfTest)
    {
        Directory.CreateDirectory(AppDataDirectory);

        var loggerConfiguration = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                Path.Combine(AppDataDirectory, "logs", "log-.txt"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14);

        // Консольный sink — только когда консоль реально есть (запуск через dotnet run/из
        // терминала). У GUI-приложения, запущенного двойным кликом, консоли нет, и попытка
        // писать в неё (в частности, выставлять цвета) может выбросить исключение при первой
        // же записи в лог — то есть уронить приложение ещё до появления окна.
        if (NativeMethods.GetConsoleWindow() != IntPtr.Zero)
        {
            loggerConfiguration = loggerConfiguration.WriteTo.Console();
        }

        Log.Logger = loggerConfiguration.CreateLogger();

        // Пошаговые отметки: если приложение всё-таки упадёт, по логу будет видно,
        // на каком именно этапе запуска это произошло.
        Log.Information("Startup: logging initialized, {Release} from {Path}", AppInfo.ReleaseName, Environment.ProcessPath);

        _host = BuildHost();
        Log.Information("Startup: host built");

        _host.Start();
        _hostStarted = true;
        Log.Information("Startup: host started, hooks installed");

        // MacroRecorder — обычный подписчик того же потока событий, что и InputEventLogger,
        // а не отдельный reader канала (см. комментарий в InputEventDispatcher).
        var dispatcher = _host.Services.GetRequiredService<InputEventDispatcher>();
        var recorder = _host.Services.GetRequiredService<MacroRecorder>();
        dispatcher.EventCaptured += recorder.Feed;

        var settings = _host.Services.GetRequiredService<AppSettingsService>();
        Models.AppText.Instance.Language = settings.Settings.Language;
        // Прежние сборки прописывали себя в автозапуск Windows — теперь программа так не делает.
        settings.RemoveAutostart();

        if (_gameLaunchError is not null)
        {
            Log.Error("Failed to start the game: {Error}", _gameLaunchError);
            MessageBox.Show(
                string.Format(Models.AppText.Instance["GameLaunchFailed"], _gameLaunchError),
                Models.AppText.Instance["GameLaunchFailedTitle"],
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }

        // Соответствие «метка на снимке → код кнопки» задаётся определением на живой мыши.
        Models.MouseButtonCatalog.ApplyCodeOverrides(settings.Settings.ButtonCodes);
        InputSuppression.ModifierState = NativeMethods.ReadModifiers;

        // Узнать, что Windows сняла хук, нельзя, поэтому к началу игры хуки ставятся заново.
        var gameWatcher = _host.Services.GetRequiredService<GameWatcher>();
        var keyboardHook = _host.Services.GetRequiredService<GlobalKeyboardHook>();
        var mouseHook = _host.Services.GetRequiredService<GlobalMouseHook>();
        gameWatcher.GameStarted += () =>
        {
            keyboardHook.Reinstall();
            mouseHook.Reinstall();
        };

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        Log.Information("Startup: main window created");

        if (CompanionGame is { } game)
        {
            // Запущены вместе с игрой — закрываемся, когда она закроется. Окно открывается
            // свёрнутым и без фокуса: иначе оно выскакивало бы поверх загружающейся игры.
            gameWatcher.GameStopped += () => Dispatcher.BeginInvoke(() =>
            {
                Log.Information("The game has closed - exiting");
                Shutdown();
            });
            gameWatcher.Watch(game);

            mainWindow.ShowActivated = false;
            mainWindow.WindowState = WindowState.Minimized;
            mainWindow.Show();
            Log.Information("SpathaMacroRecorder started together with {Game}", game);
        }
        else
        {
            mainWindow.Show();
            Log.Information("SpathaMacroRecorder started");
        }

        if (!selfTest)
        {
            ListenForTakeover(mainWindow);
        }

        if (selfTest)
        {
            RunSelfTest(mainWindow);
        }
    }

    /// <summary>
    /// Прогоняет то, что нельзя проверить сборкой: разбор XAML, поиск ресурсов темы, раскладку
    /// и движок привязок — после чего закрывает окно и сообщает результат. Запуск: с ключом
    /// --self-test.
    /// </summary>
    private void RunSelfTest(Window mainWindow)
    {
        mainWindow.UpdateLayout();

        // Пустая операция с низким приоритетом: очередь диспетчера к этому моменту успевает
        // разобрать все отложенные привязки — то есть ошибки привязок тоже всплывут.
        Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);

        mainWindow.Close();

        string result = _errorReported
            ? "SELF-TEST FAILED - see crash.txt for details"
            : "SELF-TEST PASSED: window builds, theme and bindings work.";

        Log.Information("{Result}", result);

        try
        {
            File.WriteAllText(
                Path.Combine(AppDataDirectory, "selftest.txt"),
                $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss}] {result}{Environment.NewLine}");
        }
        catch
        {
            // Не критично — результат всё равно показывается диалогом.
        }

        MessageBox.Show(result, "SpathaMacroRecorder - self-test", MessageBoxButton.OK,
            _errorReported ? MessageBoxImage.Error : MessageBoxImage.Information);

        Shutdown(_errorReported ? 1 : 0);
    }

    private const string TakeoverSignalName = @"Local\SpathaMacroRecorder.Takeover";

    /// <summary>
    /// Уже запущенная копия программы мешает новой: висит в трее после автозапуска, держит
    /// свои хуки и перехватывает те же кнопки мыши. Поэтому последняя запущенная копия закрывает
    /// предыдущие — сначала сигналом (на него отвечают сборки начиная с «Тест 6»), а сборки
    /// постарше, которые сигнала не знают, — принудительно. Профили от этого не страдают: они
    /// сохраняются на диск сразу после каждого изменения.
    /// </summary>
    private static void TakeOverPreviousInstances()
    {
        using var current = Process.GetCurrentProcess();
        var others = Process.GetProcessesByName(current.ProcessName)
            .Where(p => p.Id != current.Id)
            .ToList();

        if (others.Count == 0)
        {
            return;
        }

        using var signal = new EventWaitHandle(false, EventResetMode.ManualReset, TakeoverSignalName);
        signal.Set();

        foreach (var other in others)
        {
            try
            {
                if (!other.WaitForExit(3000))
                {
                    other.Kill();
                    other.WaitForExit(2000);
                }
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                // Копия уже закрылась сама или запущена от администратора и нам недоступна.
            }
            finally
            {
                other.Dispose();
            }
        }

        // Сброс до того, как эта копия начнёт слушать сигнал сама, — иначе закрылась бы и она.
        signal.Reset();
    }

    private void ListenForTakeover(MainWindow mainWindow)
    {
        var signal = new EventWaitHandle(false, EventResetMode.ManualReset, TakeoverSignalName);
        var listener = new Thread(() =>
        {
            signal.WaitOne();
            Log.Information("A newer copy of the program has started - exiting");
            Dispatcher.BeginInvoke(mainWindow.ExitApplication);
        })
        {
            IsBackground = true,
            Name = "Takeover listener",
        };

        listener.Start();
    }

    private static IHost BuildHost() =>
        Host.CreateDefaultBuilder()
            .UseSerilog()
            // Проверяет на этапе Build(), что каждый зарегистрированный сервис вообще можно
            // создать. Без этого ошибка регистрации всплывает по одной за запуск и лишь тогда,
            // когда до сервиса дойдёт очередь; с этим — сразу и полным списком.
            .UseDefaultServiceProvider(options =>
            {
                options.ValidateOnBuild = true;
                options.ValidateScopes = true;
            })
            .ConfigureServices((_, services) =>
            {
                services.AddSingleton(_ => Stopwatch.StartNew());
                services.AddSingleton<InputEventQueue>();

                services.AddSingleton<GlobalKeyboardHook>();
                services.AddHostedService(sp => sp.GetRequiredService<GlobalKeyboardHook>());
                services.AddSingleton<GlobalMouseHook>();
                services.AddHostedService(sp => sp.GetRequiredService<GlobalMouseHook>());

                services.AddSingleton<InputEventDispatcher>();
                services.AddHostedService(sp => sp.GetRequiredService<InputEventDispatcher>());

                services.AddSingleton<TriggerBindingService>();
                services.AddHostedService(sp => sp.GetRequiredService<TriggerBindingService>());

                services.AddSingleton<GameWatcher>();
                services.AddHostedService(sp => sp.GetRequiredService<GameWatcher>());

                services.AddSingleton<InputDiagnostics>();
                services.AddHostedService(sp => sp.GetRequiredService<InputDiagnostics>());

                services.AddSingleton<MacroRecorder>();
                services.AddSingleton<MacroPlayer>();
                services.AddSingleton<ProfileManager>();
                services.AddSingleton<AppSettingsService>();
                services.AddSingleton<UpdateService>();

                services.AddSingleton<ProfileListViewModel>();
                services.AddSingleton<MacroEditorViewModel>();
                services.AddSingleton<MouseHeroViewModel>();
                services.AddSingleton<MainViewModel>();

                services.AddSingleton<MainWindow>();
            })
            .Build();

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        ReportFatalError("Unhandled UI error", e.Exception);
        e.Handled = true;
    }

    private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        ReportFatalError("Unhandled error", e.ExceptionObject as Exception);
    }

    /// <summary>
    /// Пишет причину падения тремя способами сразу: в crash.txt (работает, даже если Serilog
    /// не успел подняться), в обычный лог и на экран — чтобы ошибка не потерялась молча.
    /// </summary>
    private static void ReportFatalError(string context, Exception? exception)
    {
        _errorReported = true;

        string details =$"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss}] {context}{Environment.NewLine}{exception}{Environment.NewLine}{Environment.NewLine}";

        try
        {
            Directory.CreateDirectory(AppDataDirectory);
            File.AppendAllText(CrashFilePath, details);
        }
        catch
        {
            // Записать не удалось — не мешаем показать ошибку остальными способами.
        }

        try
        {
            Log.Fatal(exception, "{Context}", context);
            Log.CloseAndFlush();
        }
        catch
        {
            // Логгер мог не успеть инициализироваться.
        }

        try
        {
            MessageBox.Show(
                $"{context}.\n\n{exception?.Message}\n\nDetails saved to:\n{CrashFilePath}",
                "SpathaMacroRecorder - error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        catch
        {
            // Показать диалог невозможно (например, UI-поток уже мёртв) — файла достаточно.
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.Information("SpathaMacroRecorder is shutting down");
        Log.CloseAndFlush();

        if (_host is not null)
        {
            // StopAsync у незапущенного хоста падает изнутри (ему нечего останавливать),
            // и эта вторичная ошибка перекрывает настоящую причину сбоя при старте.
            if (_hostStarted)
            {
                try
                {
                    _host.StopAsync().GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    ReportFatalError("Error while stopping the host", ex);
                }
            }

            _host.Dispose();
        }

        base.OnExit(e);
    }
}
