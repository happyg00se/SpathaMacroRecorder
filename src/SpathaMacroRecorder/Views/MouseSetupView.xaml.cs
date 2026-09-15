using System.Windows;

namespace SpathaMacroRecorder.Views;

internal partial class MouseSetupView : Window
{
    private MouseSetupView()
    {
        InitializeComponent();
    }

    internal static void Show(Window? owner) =>
        new MouseSetupView { Owner = owner }.ShowDialog();

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
