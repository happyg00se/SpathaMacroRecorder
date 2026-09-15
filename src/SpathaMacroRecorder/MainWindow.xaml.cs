using System.Windows;
using SpathaMacroRecorder.Services;
using SpathaMacroRecorder.ViewModels;

namespace SpathaMacroRecorder;

internal partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();

        // По заголовку на скриншоте видно, какая сборка запущена.
        Title = $"Spatha Macro Recorder — {AppInfo.ReleaseName}";
    }

    /// <summary>Закрыть программу — по сигналу более новой запущенной копии.</summary>
    internal void ExitApplication() => Application.Current.Shutdown();
}
