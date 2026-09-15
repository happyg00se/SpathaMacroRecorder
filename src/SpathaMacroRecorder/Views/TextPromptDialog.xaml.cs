using System.Windows;

namespace SpathaMacroRecorder.Views;

/// <summary>
/// Простой модальный ввод одной строки — используется для создания/переименования/дублирования
/// профилей и макросов. Не претендует на роль полноценного диалога вроде TriggerPickerDialog
/// (Фаза 9) — только имя.
/// </summary>
public partial class TextPromptDialog : Window
{
    public TextPromptDialog()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            InputTextBox.SelectAll();
            InputTextBox.Focus();
        };
    }

    public string? ResultText { get; private set; }

    /// <summary>Показывает диалог модально; возвращает введённый текст или null при отмене/пустом вводе.</summary>
    public static string? Show(Window? owner, string title, string prompt, string initialValue = "")
    {
        var dialog = new TextPromptDialog
        {
            Title = title,
            Owner = owner,
        };
        dialog.PromptTextBlock.Text = prompt;
        dialog.InputTextBox.Text = initialValue;

        bool? accepted = dialog.ShowDialog();
        return accepted == true ? dialog.ResultText : null;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        ResultText = InputTextBox.Text.Trim();
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
