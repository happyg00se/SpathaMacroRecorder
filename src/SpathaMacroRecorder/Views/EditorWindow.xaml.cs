using System.Windows;
using SpathaMacroRecorder.ViewModels;

namespace SpathaMacroRecorder.Views;

/// <summary>
/// Профили, макросы и редактор шагов — всё, что раньше было главным окном. Открывается
/// кнопкой Settings с главного экрана.
/// </summary>
internal partial class EditorWindow : Window
{
    internal EditorWindow(MainViewModel viewModel)
    {
        DataContext = viewModel;

        InitializeComponent();
    }
}
