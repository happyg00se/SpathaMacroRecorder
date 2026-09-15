using System.Globalization;
using System.Windows;
using System.Windows.Input;
using SpathaMacroRecorder.Models;
using SpathaMacroRecorder.Services;

namespace SpathaMacroRecorder.Views;

internal partial class SettingsView : Window
{
    private int _panicVirtualKey;
    private int _pauseVirtualKey;

    /// <summary>Какое поле сейчас ждёт нажатия: ни одно, panic или пауза записи.</summary>
    private enum Capturing { None, Panic, Pause }

    private Capturing _capturing;

    private SettingsView(AppSettings settings)
    {
        InitializeComponent();

        _panicVirtualKey = settings.PanicKeyVirtualKey;
        _pauseVirtualKey = settings.PauseRecordingVirtualKey;
        LangRuRadio.IsChecked = settings.Language == "ru";
        LangEnRadio.IsChecked = settings.Language != "ru";
        LangRuRadio.Checked += (_, _) => AppText.Instance.Language = "ru";
        LangEnRadio.Checked += (_, _) => AppText.Instance.Language = "en";

        string? exePath = Environment.ProcessPath;
        SteamLaunchBox.Text = GameLaunch.SteamLaunchOptions(exePath);
        SteamTempWarning.Visibility = GameLaunch.LooksTemporary(exePath) ? Visibility.Visible : Visibility.Collapsed;
        KeyGapBox.Text = settings.KeyGapMs.ToString(CultureInfo.InvariantCulture);

        UpdateKeyTexts();
    }

    /// <summary>Показывает настройки; при сохранении применяет их к переданному объекту.</summary>
    internal static bool Show(Window? owner, AppSettings settings)
    {
        var dialog = new SettingsView(settings) { Owner = owner };
        if (dialog.ShowDialog() != true)
        {
            // Отмена — возвращаем язык, каким он был до открытия окна.
            AppText.Instance.Language = settings.Language;
            return false;
        }

        settings.PanicKeyVirtualKey = dialog._panicVirtualKey;
        settings.PauseRecordingVirtualKey = dialog._pauseVirtualKey;
        settings.Language = dialog.LangRuRadio.IsChecked == true ? "ru" : "en";

        // Непонятное значение не сохраняем — остаётся прежнее, а не случайный ноль.
        if (int.TryParse(dialog.KeyGapBox.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int keyGap))
        {
            settings.KeyGapMs = Math.Clamp(keyGap, 0, 1000);
        }

        return true;
    }

    private void UpdateKeyTexts()
    {
        PanicKeyText.Text = DescribeKey(_panicVirtualKey);
        PauseKeyText.Text = DescribeKey(_pauseVirtualKey);
    }

    internal static string DescribeKey(int virtualKey)
    {
        if (virtualKey == 0)
        {
            return AppText.Instance["NotSet"];
        }

        var key = KeyInterop.KeyFromVirtualKey(virtualKey);
        return key == Key.None ? $"Key {virtualKey}" : key.ToString();
    }

    private void Capture_Click(object sender, RoutedEventArgs e)
    {
        _capturing = Capturing.Panic;
        CaptureButton.Content = "Waiting…";
        PanicKeyText.Text = "Press any key";
    }

    private void CapturePause_Click(object sender, RoutedEventArgs e)
    {
        _capturing = Capturing.Pause;
        CapturePauseButton.Content = "Waiting…";
        PauseKeyText.Text = "Press any key";
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (_capturing == Capturing.None)
        {
            base.OnPreviewKeyDown(e);
            return;
        }

        // System-клавиши (Alt+…) приходят как Key.System — настоящая клавиша лежит в SystemKey.
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        int virtualKey = KeyInterop.VirtualKeyFromKey(key);

        if (_capturing == Capturing.Panic)
        {
            _panicVirtualKey = virtualKey;
        }
        else
        {
            _pauseVirtualKey = virtualKey;
        }

        _capturing = Capturing.None;
        CaptureButton.Content = "Press a key…";
        CapturePauseButton.Content = "Press a key…";
        UpdateKeyTexts();

        e.Handled = true;
    }

    private void CopySteam_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(SteamLaunchBox.Text);
            CopySteamButton.Content = AppText.Instance["SteamCopied"];
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            // Буфер обмена занят другой программой — строку можно выделить и скопировать вручную.
            SteamLaunchBox.Focus();
            SteamLaunchBox.SelectAll();
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
