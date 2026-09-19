using SpathaMacroRecorder.Services;

namespace SpathaMacroRecorder.Tests;

/// <summary>
/// Разбор строки, которой Steam запускает программу вместо игры: «--game %command%».
/// </summary>
public class GameLaunchTests
{
    private const string GamePath = @"D:\SteamLibrary\steamapps\common\Helldivers 2\bin\helldivers2.exe";

    [Fact]
    public void Parse_TakesEverythingAfterTheFlagAsTheGameCommand()
    {
        var request = GameLaunch.Parse(["--game", GamePath, "-windowed", "some value"]);

        Assert.NotNull(request);
        Assert.Equal(GamePath, request!.ExecutablePath);
        Assert.Equal(new[] { "-windowed", "some value" }, request.Arguments);
    }

    [Fact]
    public void ProcessName_IsTheExeNameWithoutExtension() =>
        Assert.Equal("helldivers2", GameLaunch.Parse(["--game", GamePath])!.ProcessName);

    [Fact]
    public void Parse_WithoutAGameCommand_ReturnsNull()
    {
        // Обычный запуск вручную, самопроверка, флаг без команды, флаг с пустой командой.
        string[][] cases = [[], ["--self-test"], ["--game"], ["--game", "  "]];

        foreach (string[] args in cases)
        {
            Assert.Null(GameLaunch.Parse(args));
        }
    }

    [Fact]
    public void Parse_StripsStrayQuotes() =>
        Assert.Equal(GamePath, GameLaunch.Parse(["--game", $"\"{GamePath}\""])!.ExecutablePath);

    [Fact]
    public void SteamLaunchOptions_QuotesThePathAndKeepsTheSteamPlaceholder() =>
        Assert.Equal(
            "\"C:\\Spatha\\SpathaMacroRecorder.exe\" --game %command%",
            GameLaunch.SteamLaunchOptions(@"C:\Spatha\SpathaMacroRecorder.exe"));

    [Fact]
    public void Parse_PassesRendererFlagsOnToTheGame()
    {
        // Флаг рендера, дописанный в параметры запуска вручную, должен дойти до игры как есть.
        var request = GameLaunch.Parse(["--game", GamePath, "--use-d3d11"]);

        Assert.NotNull(request);
        Assert.Equal(GamePath, request!.ExecutablePath);
        Assert.Equal(new[] { "--use-d3d11" }, request.Arguments);
    }

    [Fact]
    public void WorkingDirectoryFor_KeepsTheFolderSteamStartedUsFrom()
    {
        // Steam запускает и программу, и игру из корня установки игры — exe лежит в bin.
        const string Root = @"D:\SteamLibrary\steamapps\common\Helldivers 2";

        Assert.Equal(Root, GameLaunch.WorkingDirectoryFor(GamePath, Root));
    }

    [Theory]
    // Программу открыли вручную: каталог чужой игре, поэтому его не берём.
    [InlineData(@"C:\Spatha", @"D:\SteamLibrary\steamapps\common\Helldivers 2")]
    [InlineData(null, @"D:\SteamLibrary\steamapps\common\Helldivers 2")]
    [InlineData("   ", @"D:\SteamLibrary\steamapps\common\Helldivers 2")]
    public void WorkingDirectoryFor_FallsBackToTheFolderAboveBin(string? currentDirectory, string expected) =>
        Assert.Equal(expected, GameLaunch.WorkingDirectoryFor(GamePath, currentDirectory));

    [Fact]
    public void WorkingDirectoryFor_WithoutABinFolder_TakesTheExeFolder() =>
        Assert.Equal(
            @"D:\Games\Some Game",
            GameLaunch.WorkingDirectoryFor(@"D:\Games\Some Game\game.exe", @"C:\Spatha"));

    [Theory]
    [InlineData(@"C:\Users\r4v3r_63\AppData\Local\Temp\Rar$EXa29292.11346.rartemp\SpathaMacroRecorder.exe", true)]
    [InlineData(@"C:\Spatha\SpathaMacroRecorder.exe", false)]
    public void LooksTemporary_SpotsTheArchiveTempFolder(string path, bool expected) =>
        Assert.Equal(expected, GameLaunch.LooksTemporary(path));
}
