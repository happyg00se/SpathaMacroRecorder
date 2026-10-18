using SpathaMacroRecorder.Services;

namespace SpathaMacroRecorder.Tests;

public sealed class UpdateServiceTests
{
    // Урезанный ответ GitHub API /releases/latest — поля те же, что приходят на самом деле.
    private const string ReleaseJson = """
        {
          "tag_name": "v3.0",
          "name": "Release 3.0",
          "assets": [
            {
              "name": "notes.txt",
              "browser_download_url": "https://github.com/happyg00se/SpathaMacroRecorder/releases/download/v3.0/notes.txt"
            },
            {
              "name": "SpathaMacroRecorder.exe",
              "browser_download_url": "https://github.com/happyg00se/SpathaMacroRecorder/releases/download/v3.0/SpathaMacroRecorder.exe",
              "digest": "sha256:482d4b6b87191094caabff64353635db9c4c0254b5409fcfa37ddf56ee74ece4"
            }
          ]
        }
        """;

    [Fact]
    public void StaticSetup_DoesNotThrow()
    {
        // HTTP-клиент пишет версию в User-Agent — версия должна быть готова раньше клиента.
        Assert.NotNull(UpdateService.CurrentVersion);
    }

    [Fact]
    public void ParseRelease_PicksTheExeAndItsChecksum()
    {
        var release = UpdateService.ParseRelease(ReleaseJson);

        Assert.NotNull(release);
        Assert.Equal(new Version(3, 0, 0, 0), release.Version);
        Assert.Equal("Release 3.0", release.Title);
        Assert.EndsWith("/v3.0/SpathaMacroRecorder.exe", release.DownloadUrl.AbsoluteUri);
        Assert.Equal("482d4b6b87191094caabff64353635db9c4c0254b5409fcfa37ddf56ee74ece4", release.Sha256);
    }

    [Fact]
    public void ParseRelease_WithoutExeIsNotAnUpdate()
    {
        const string json = """{ "tag_name": "v3.0", "name": "Release 3.0", "assets": [] }""";
        Assert.Null(UpdateService.ParseRelease(json));
    }

    [Fact]
    public void ParseRelease_RejectsPlainHttpDownload()
    {
        string json = ReleaseJson.Replace("https://github.com", "http://github.com", StringComparison.Ordinal);
        Assert.Null(UpdateService.ParseRelease(json));
    }

    [Theory]
    [InlineData("v3.0", 3, 0)]
    [InlineData("3.1", 3, 1)]
    [InlineData(" V10.2 ", 10, 2)]
    [InlineData("v3", 3, 0)]
    public void ParseTag_ReadsReleaseNumbers(string tag, int major, int minor)
    {
        Assert.Equal(new Version(major, minor, 0, 0), UpdateService.ParseTag(tag));
    }

    [Theory]
    [InlineData("test-7")]
    [InlineData("")]
    [InlineData(null)]
    public void ParseTag_IgnoresNonVersionTags(string? tag)
    {
        Assert.Null(UpdateService.ParseTag(tag));
    }

    [Theory]
    [InlineData("3.0", "2.0.0.0", true)]
    [InlineData("3.1", "3.0.0.0", true)]
    [InlineData("3.0", "3.0.0.0", false)]
    [InlineData("2.0", "3.0.0.0", false)]
    public void IsNewer_ComparesReleaseWithRunningVersion(string candidate, string current, bool expected)
    {
        Assert.Equal(expected, UpdateService.IsNewer(Version.Parse(candidate), Version.Parse(current)));
    }

    [Fact]
    public void Install_PutsNewFileInPlaceAndKeepsOldAside()
    {
        string dir = Directory.CreateTempSubdirectory("SpathaUpdateTest").FullName;
        try
        {
            string exe = Path.Combine(dir, "SpathaMacroRecorder.exe");
            string downloaded = exe + ".download";
            File.WriteAllText(exe, "old");
            File.WriteAllText(downloaded, "new");

            UpdateService.Install(exe, downloaded);

            Assert.Equal("new", File.ReadAllText(exe));
            Assert.Equal("old", File.ReadAllText(UpdateService.OldPath(exe)));
            Assert.False(File.Exists(downloaded));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Install_RestoresTheOldFileWhenTheNewOneIsMissing()
    {
        string dir = Directory.CreateTempSubdirectory("SpathaUpdateTest").FullName;
        try
        {
            string exe = Path.Combine(dir, "SpathaMacroRecorder.exe");
            File.WriteAllText(exe, "old");

            Assert.Throws<FileNotFoundException>(() => UpdateService.Install(exe, exe + ".download"));

            Assert.Equal("old", File.ReadAllText(exe));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Rollback_PutsTheOldFileBack()
    {
        string dir = Directory.CreateTempSubdirectory("SpathaUpdateTest").FullName;
        try
        {
            string exe = Path.Combine(dir, "SpathaMacroRecorder.exe");
            File.WriteAllText(exe, "new");
            File.WriteAllText(UpdateService.OldPath(exe), "old");

            UpdateService.Rollback(exe);

            Assert.Equal("old", File.ReadAllText(exe));
            Assert.False(File.Exists(UpdateService.OldPath(exe)));

            // Возвращать нечего — не ошибка.
            UpdateService.Rollback(exe);
            Assert.Equal("old", File.ReadAllText(exe));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task VerifyDownload_RejectsTruncatedAndNonExeFiles()
    {
        string dir = Directory.CreateTempSubdirectory("SpathaUpdateTest").FullName;
        try
        {
            string path = Path.Combine(dir, "download");
            byte[] exe = [(byte)'M', (byte)'Z', 1, 2, 3];
            File.WriteAllBytes(path, exe);

            string sha = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(exe));
            await UpdateService.VerifyDownloadAsync(path, exe.Length, sha);
            await UpdateService.VerifyDownloadAsync(path, null, null);

            await Assert.ThrowsAsync<InvalidDataException>(() => UpdateService.VerifyDownloadAsync(path, exe.Length + 1, null));
            await Assert.ThrowsAsync<InvalidDataException>(() => UpdateService.VerifyDownloadAsync(path, null, new string('0', 64)));

            File.WriteAllText(path, "<html>Not Found</html>");
            await Assert.ThrowsAsync<InvalidDataException>(() => UpdateService.VerifyDownloadAsync(path, null, null));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void ParseWatch_ReadsTheGameToFollowAfterRestart()
    {
        Assert.Equal("helldivers2", GameLaunch.ParseWatch(["--watch-game", "helldivers2.exe"]));
        Assert.Null(GameLaunch.ParseWatch([]));
        Assert.Null(GameLaunch.ParseWatch(["--watch-game"]));
    }
}
