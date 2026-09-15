using System.Windows;

namespace SpathaMacroRecorder.Views;

internal partial class FaqView : Window
{
    private FaqView()
    {
        InitializeComponent();
    }

    internal static void Show(Window? owner) => new FaqView { Owner = owner }.ShowDialog();

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
