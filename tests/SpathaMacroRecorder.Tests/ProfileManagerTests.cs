using Microsoft.Extensions.Logging.Abstractions;
using SpathaMacroRecorder.Models;
using SpathaMacroRecorder.Services;

namespace SpathaMacroRecorder.Tests;

// ProfileManager не трогает Windows API — только System.IO/System.Text.Json, поэтому эти тесты
// не требуют реального user32.dll (в отличие от остальных Services). Каждый тест работает
// в изолированном temp-каталоге, а не в реальном %AppData%.
public sealed class ProfileManagerTests : IDisposable
{
    private readonly string _rootDirectory =
        Path.Combine(Path.GetTempPath(), "SpathaMacroRecorderTests", Guid.NewGuid().ToString("N"));

    // Отдельно от каталога профилей: LoadAll() глобит *.json прямо в нём, и экспортированный файл
    // внутри того же каталога был бы по ошибке подхвачен как ещё один профиль.
    private string ProfilesDirectory => Path.Combine(_rootDirectory, "profiles");
    private string ExportDirectory => Path.Combine(_rootDirectory, "export");

    private ProfileManager CreateManager() =>
        new(NullLogger<ProfileManager>.Instance, ProfilesDirectory);

    public void Dispose()
    {
        if (Directory.Exists(_rootDirectory))
        {
            Directory.Delete(_rootDirectory, recursive: true);
        }
    }

    [Fact]
    public void Save_PersistsProfileAcrossRestart()
    {
        var manager = CreateManager();
        var profile = new MacroProfile
        {
            ProfileName = "Helldivers2",
            Macros = [new Macro { Id = Guid.NewGuid(), Name = "Ресупплай" }],
        };

        manager.Save(profile);

        // Новый ProfileManager на том же каталоге — имитация перезапуска приложения.
        var afterRestart = CreateManager();
        var loaded = afterRestart.LoadAll();

        var reloaded = Assert.Single(loaded);
        Assert.Equal("Helldivers2", reloaded.ProfileName);
        Assert.Single(reloaded.Macros);
        Assert.Equal("Ресупплай", reloaded.Macros[0].Name);
    }

    [Fact]
    public void Create_ThrowsWhenProfileAlreadyExists()
    {
        var manager = CreateManager();
        manager.Create("Профиль");

        Assert.Throws<InvalidOperationException>(() => manager.Create("Профиль"));
    }

    [Fact]
    public void Rename_MovesToNewFileAndRemovesOld()
    {
        var manager = CreateManager();
        var profile = manager.Create("Старое имя");

        var renamed = manager.Rename(profile, "Новое имя");

        var loaded = manager.LoadAll();
        var loadedProfile = Assert.Single(loaded);
        Assert.Equal("Новое имя", loadedProfile.ProfileName);
        Assert.Equal("Новое имя", renamed.ProfileName);
    }

    [Fact]
    public void Duplicate_CreatesSecondFileLeavingOriginalIntact()
    {
        var manager = CreateManager();
        var original = manager.Create("Оригинал");

        manager.Duplicate(original, "Копия");

        var loaded = manager.LoadAll();
        Assert.Equal(2, loaded.Count);
        Assert.Contains(loaded, p => p.ProfileName == "Оригинал");
        Assert.Contains(loaded, p => p.ProfileName == "Копия");
    }

    [Fact]
    public void Delete_RemovesProfileFile()
    {
        var manager = CreateManager();
        var profile = manager.Create("Удаляемый");

        manager.Delete(profile);

        Assert.Empty(manager.LoadAll());
    }

    [Fact]
    public void ExportThenImport_RoundTripsProfile()
    {
        var manager = CreateManager();
        var profile = manager.Create("Экспортный");
        Directory.CreateDirectory(ExportDirectory);
        string exportPath = Path.Combine(ExportDirectory, "export.json");

        manager.Export(profile, exportPath);
        manager.Delete(profile);
        var imported = manager.Import(exportPath);

        Assert.Equal("Экспортный", imported.ProfileName);
        Assert.Single(manager.LoadAll());
    }

    [Fact]
    public void Import_AddsSuffixOnNameConflict()
    {
        var manager = CreateManager();
        var existing = manager.Create("Дубликат");
        Directory.CreateDirectory(ExportDirectory);
        string exportPath = Path.Combine(ExportDirectory, "export.json");
        manager.Export(existing, exportPath);

        var imported = manager.Import(exportPath);

        Assert.Equal("Дубликат (2)", imported.ProfileName);
        Assert.Equal(2, manager.LoadAll().Count);
    }

    [Fact]
    public void LoadAll_SkipsCorruptedFileAndRaisesEvent_ButKeepsValidOnes()
    {
        var manager = CreateManager();
        manager.Create("Валидный");
        string corruptedPath = Path.Combine(ProfilesDirectory, "битый.json");
        File.WriteAllText(corruptedPath, "{ не json вообще");

        string? failedPath = null;
        manager.ProfileLoadFailed += (path, _) => failedPath = path;

        var loaded = manager.LoadAll();

        Assert.Single(loaded);
        Assert.Equal("Валидный", loaded[0].ProfileName);
        Assert.Equal(corruptedPath, failedPath);
    }

    [Fact]
    public void LoadAll_SkipsFileWithUnsupportedSchemaVersion()
    {
        var manager = CreateManager();
        string path = Path.Combine(ProfilesDirectory, "будущая-версия.json");
        File.WriteAllText(path, """{ "schemaVersion": 99, "profileName": "Будущее", "macros": [] }""");

        var loaded = manager.LoadAll();

        Assert.Empty(loaded);
    }
}
