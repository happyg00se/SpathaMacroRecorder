using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SpathaMacroRecorder.Models;

namespace SpathaMacroRecorder.Services;

/// <summary>
/// CRUD профилей на диске (%AppData%\SpathaMacroRecorder\Profiles\&lt;имя&gt;.json) + импорт/экспорт.
/// Битый JSON никогда не роняет приложение: LoadAll логирует проблему, поднимает
/// ProfileLoadFailed (на это подпишется UI в Фазе 7, чтобы показать уведомление) и пропускает файл.
/// </summary>
internal sealed class ProfileManager
{
    private const int SupportedSchemaVersion = 1;

    private static readonly string DefaultProfilesDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SpathaMacroRecorder", "Profiles");

    private static readonly JsonSerializerOptions JsonOptions = new(MacroJsonContext.Default.Options)
    {
        // По умолчанию System.Text.Json экранирует не-ASCII (кириллица в именах профилей/макросов
        // превращается в \uXXXX) — с этим энкодером файл остаётся читаемым в текстовом редакторе.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly ILogger<ProfileManager> _logger;
    private readonly string _profilesDirectory;

    // Публичный — этот конструктор вызывает контейнер внедрения зависимостей, а он видит
    // только публичные. Конструктор ниже (с явным каталогом) остаётся internal: он нужен
    // только тестам, и DI не должен его выбирать.
    public ProfileManager(ILogger<ProfileManager> logger) : this(logger, DefaultProfilesDirectory)
    {
    }

    /// <summary>Конструктор с явной директорией — используется юнит-тестами (изолированный temp-каталог).</summary>
    internal ProfileManager(ILogger<ProfileManager> logger, string profilesDirectory)
    {
        _logger = logger;
        _profilesDirectory = profilesDirectory;
        Directory.CreateDirectory(_profilesDirectory);
    }

    /// <summary>Файл не удалось прочитать или разобрать — путь и причина, для уведомления в UI.</summary>
    internal event Action<string, Exception>? ProfileLoadFailed;

    internal IReadOnlyList<MacroProfile> LoadAll()
    {
        var profiles = new List<MacroProfile>();

        foreach (string path in Directory.EnumerateFiles(_profilesDirectory, "*.json"))
        {
            try
            {
                string json = File.ReadAllText(path);
                profiles.Add(DeserializeProfile(json));
            }
            catch (Exception ex) when (IsRecoverableFileError(ex))
            {
                _logger.LogWarning(ex, "Profile {Path} is corrupted or unreadable - skipping", path);
                ProfileLoadFailed?.Invoke(path, ex);
            }
        }

        return profiles;
    }

    internal MacroProfile Create(string profileName)
    {
        ValidateName(profileName);

        string path = PathFor(profileName);
        if (File.Exists(path))
        {
            throw new InvalidOperationException($"Profile \"{profileName}\" already exists.");
        }

        var profile = new MacroProfile { ProfileName = profileName };
        Save(profile);
        return profile;
    }

    internal void Save(MacroProfile profile)
    {
        string json = JsonSerializer.Serialize(profile, typeof(MacroProfile), JsonOptions);
        string path = PathFor(profile.ProfileName);

        // Пишем во временный файл и переименовываем атомарно — обрыв записи (сбой, выключение
        // питания) не должен оставить наполовину записанный, а значит битый профиль на диске.
        string tempPath = path + ".tmp";
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, path, overwrite: true);
    }

    internal MacroProfile Rename(MacroProfile profile, string newName)
    {
        ValidateName(newName);

        string newPath = PathFor(newName);
        if (!string.Equals(newName, profile.ProfileName, StringComparison.Ordinal) && File.Exists(newPath))
        {
            throw new InvalidOperationException($"Profile \"{newName}\" already exists.");
        }

        string oldPath = PathFor(profile.ProfileName);
        var renamed = profile with { ProfileName = newName };
        Save(renamed);

        if (File.Exists(oldPath) && !string.Equals(oldPath, newPath, StringComparison.Ordinal))
        {
            File.Delete(oldPath);
        }

        return renamed;
    }

    internal MacroProfile Duplicate(MacroProfile profile, string newName)
    {
        ValidateName(newName);

        if (File.Exists(PathFor(newName)))
        {
            throw new InvalidOperationException($"Profile \"{newName}\" already exists.");
        }

        var duplicate = profile with { ProfileName = newName };
        Save(duplicate);
        return duplicate;
    }

    internal void Delete(MacroProfile profile)
    {
        string path = PathFor(profile.ProfileName);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    internal void Export(MacroProfile profile, string destinationPath)
    {
        string json = JsonSerializer.Serialize(profile, typeof(MacroProfile), JsonOptions);
        File.WriteAllText(destinationPath, json);
    }

    /// <summary>Импортирует файл профиля; при конфликте имён добавляет суффикс " (2)", " (3)"...</summary>
    internal MacroProfile Import(string sourcePath)
    {
        string json = File.ReadAllText(sourcePath);
        var profile = DeserializeProfile(json);

        string targetName = profile.ProfileName;
        int suffix = 2;
        while (File.Exists(PathFor(targetName)))
        {
            targetName = $"{profile.ProfileName} ({suffix++})";
        }

        var imported = string.Equals(targetName, profile.ProfileName, StringComparison.Ordinal)
            ? profile
            : profile with { ProfileName = targetName };

        Save(imported);
        return imported;
    }

    private static MacroProfile DeserializeProfile(string json)
    {
        var profile = JsonSerializer.Deserialize(json, typeof(MacroProfile), JsonOptions) as MacroProfile
            ?? throw new JsonException("Profile deserialized to null.");

        if (profile.SchemaVersion != SupportedSchemaVersion)
        {
            throw new JsonException(
                $"Unsupported profile schema version: {profile.SchemaVersion} (expected {SupportedSchemaVersion}).");
        }

        return profile;
    }

    private static bool IsRecoverableFileError(Exception ex) =>
        ex is JsonException or IOException or UnauthorizedAccessException;

    private string PathFor(string profileName) =>
        Path.Combine(_profilesDirectory, $"{SanitizeFileName(profileName)}.json");

    private static string SanitizeFileName(string name)
    {
        foreach (char invalid in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(invalid, '_');
        }

        return name;
    }

    private static void ValidateName(string profileName)
    {
        if (string.IsNullOrWhiteSpace(profileName))
        {
            throw new ArgumentException("Profile name cannot be empty.", nameof(profileName));
        }

        // Раньше недопустимые символы молча заменялись на "_", и два разных имени могли
        // указать на один файл — второй профиль затирал первый. Лучше сказать сразу.
        char[] invalid = Path.GetInvalidFileNameChars();
        if (profileName.Any(invalid.Contains))
        {
            string shown = string.Join(" ", invalid.Where(c => !char.IsControl(c)));
            throw new ArgumentException(
                $"Profile name cannot contain any of these characters: {shown}", nameof(profileName));
        }
    }
}
