using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using SpathaMacroRecorder.Models;

namespace SpathaMacroRecorder.Services;

/// <summary>
/// Настройки приложения в %AppData%\SpathaMacroRecorder\settings.json + управление автозапуском
/// (ключ Run текущего пользователя — прав администратора не требует).
/// </summary>
internal sealed class AppSettingsService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "SpathaMacroRecorder";

    private static readonly JsonSerializerOptions JsonOptions = new(MacroJsonContext.Default.Options)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly ILogger<AppSettingsService> _logger;
    private readonly string _settingsPath;

    public AppSettingsService(ILogger<AppSettingsService> logger)
        : this(logger, Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SpathaMacroRecorder", "settings.json"))
    {
    }

    internal AppSettingsService(ILogger<AppSettingsService> logger, string settingsPath)
    {
        _logger = logger;
        _settingsPath = settingsPath;
        Settings = Load();
    }

    public AppSettings Settings { get; private set; }

    private AppSettings Load()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                string json = File.ReadAllText(_settingsPath);
                if (JsonSerializer.Deserialize(json, typeof(AppSettings), JsonOptions) is AppSettings loaded)
                {
                    return loaded;
                }
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            // Битые настройки не должны мешать запуску — откатываемся на значения по умолчанию.
            _logger.LogWarning(ex, "Settings file is unreadable - falling back to defaults");
        }

        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
            string json = JsonSerializer.Serialize(Settings, typeof(AppSettings), JsonOptions);
            File.WriteAllText(_settingsPath, json);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Failed to save settings");
        }
    }

    /// <summary>
    /// Убирает программу из автозапуска Windows. Прежние сборки прописывали себя туда в режиме
    /// «работать вместе с игрой». Теперь программа запускается вместе с игрой через Steam и в
    /// автозапуске не нуждается — оставшаяся запись поднимала бы её при каждом входе в Windows.
    /// </summary>
    public void RemoveAutostart()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            if (key?.GetValue(RunValueName) is not null)
            {
                key.DeleteValue(RunValueName, throwOnMissingValue: false);
                _logger.LogInformation("Removed the old Windows startup entry");
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            _logger.LogError(ex, "Failed to remove the autostart registry entry");
        }
    }
}
