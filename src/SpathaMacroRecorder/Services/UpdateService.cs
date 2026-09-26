using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace SpathaMacroRecorder.Services;

/// <summary>Вышедший на GitHub релиз, который новее запущенной программы.</summary>
internal sealed record UpdateInfo(Version Version, string Title, Uri DownloadUrl, string? Sha256);

/// <summary>
/// Обновление одной кнопкой из раздела Releases репозитория на GitHub.
///
/// Последний релиз берётся из открытого API GitHub (без ключей — репозиторий публичный), из него —
/// файл SpathaMacroRecorder.exe. Скачанный файл сверяется с SHA-256, который GitHub сам считает
/// для каждого файла релиза. Запущенный exe Windows перезаписать не даёт, но переименовать —
/// даёт: старый файл становится «.old», новый встаёт на его место, программа запускает новую
/// копию и закрывается. «.old» удаляется при следующем запуске.
/// </summary>
internal sealed class UpdateService(ILogger<UpdateService> logger)
{
    internal const string Owner = "happyg00se";
    internal const string Repository = "SpathaMacroRecorder";
    internal const string AssetName = "SpathaMacroRecorder.exe";

    /// <summary>Передаётся новой копии: она запущена обновлением, а не пользователем.</summary>
    internal const string UpdatedArgument = "--updated";

    /// <summary>
    /// Версия запущенной программы — из номера сборки, который ставит релиз. Объявлена раньше
    /// Http: статические поля заполняются по порядку, а клиент пишет версию в User-Agent.
    /// </summary>
    internal static Version CurrentVersion { get; } =
        Normalize(Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(0, 0));

    private static readonly Uri LatestReleaseUri = new($"https://api.github.com/repos/{Owner}/{Repository}/releases/latest");

    private static readonly HttpClient Http = CreateClient();

    /// <summary>Новый релиз, если он есть; null — установлена последняя версия.</summary>
    internal async Task<UpdateInfo?> CheckAsync(CancellationToken cancellationToken = default)
    {
        string json = await Http.GetStringAsync(LatestReleaseUri, cancellationToken).ConfigureAwait(false);
        var release = ParseRelease(json);
        logger.LogInformation("Update check: latest {Latest}, running {Current}", release?.Version, CurrentVersion);
        return release is not null && IsNewer(release.Version, CurrentVersion) ? release : null;
    }

    /// <summary>
    /// Скачивает релиз рядом с программой, проверяет его и ставит на место запущенного exe.
    /// Возвращает путь к новому exe — его остаётся запустить.
    /// </summary>
    internal async Task<string> DownloadAndInstallAsync(
        UpdateInfo update,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        string exePath = Environment.ProcessPath
                         ?? throw new InvalidOperationException("The program's own path is unknown.");
        string downloadPath = exePath + ".download";

        try
        {
            using (var response = await Http.GetAsync(update.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                long? total = response.Content.Headers.ContentLength;

                await using var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                await using var target = new FileStream(downloadPath, FileMode.Create, FileAccess.Write, FileShare.None);

                var buffer = new byte[81920];
                long received = 0;
                int read;
                while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
                {
                    await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                    received += read;
                    if (total is > 0)
                    {
                        progress?.Report((double)received / total.Value);
                    }
                }
            }

            if (update.Sha256 is { } expected)
            {
                string actual = await ComputeSha256Async(downloadPath, cancellationToken).ConfigureAwait(false);
                if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException($"Checksum mismatch: expected {expected}, got {actual}.");
                }
            }

            Install(exePath, downloadPath);
            logger.LogInformation("Updated to {Version}", update.Version);
            return exePath;
        }
        finally
        {
            TryDelete(downloadPath);
        }
    }

    /// <summary>Запускает обновлённую программу. Старая копия закрывается сама — см. App.</summary>
    internal static void Restart(string exePath, string? watchedGame)
    {
        var info = new ProcessStartInfo(exePath) { UseShellExecute = false };
        info.ArgumentList.Add(UpdatedArgument);
        if (watchedGame is not null)
        {
            // Программа была запущена вместе с игрой — новая копия продолжает за ней следить
            // и закроется вместе с ней, но саму игру второй раз не запускает.
            info.ArgumentList.Add(GameLaunch.WatchArgument);
            info.ArgumentList.Add(watchedGame);
        }

        Process.Start(info)?.Dispose();
    }

    /// <summary>Убирает exe, оставшийся от прошлого обновления.</summary>
    internal static void CleanupAfterUpdate()
    {
        if (Environment.ProcessPath is { } exePath)
        {
            TryDelete(OldPath(exePath));
        }
    }

    /// <summary>Меняет файлы местами: запущенный exe — в «.old», скачанный — на его место.</summary>
    internal static void Install(string exePath, string downloadedPath)
    {
        string oldPath = OldPath(exePath);
        File.Move(exePath, oldPath, overwrite: true);
        try
        {
            File.Move(downloadedPath, exePath);
        }
        catch
        {
            // Новый файл встать не смог — возвращаем старый, программа должна остаться рабочей.
            File.Move(oldPath, exePath);
            throw;
        }
    }

    internal static string OldPath(string exePath) => exePath + ".old";

    /// <summary>Разбирает ответ GitHub о релизе; null — в релизе нет exe или непонятный номер.</summary>
    internal static UpdateInfo? ParseRelease(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (!root.TryGetProperty("tag_name", out var tag) || ParseTag(tag.GetString()) is not { } version)
        {
            return null;
        }

        if (!root.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var asset in assets.EnumerateArray())
        {
            if (!string.Equals(asset.GetProperty("name").GetString(), AssetName, StringComparison.OrdinalIgnoreCase)
                || !Uri.TryCreate(asset.GetProperty("browser_download_url").GetString(), UriKind.Absolute, out var url)
                || url.Scheme != Uri.UriSchemeHttps)
            {
                continue;
            }

            string? sha256 = asset.TryGetProperty("digest", out var digest)
                             && digest.GetString() is { } value
                             && value.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)
                ? value["sha256:".Length..]
                : null;

            string title = root.TryGetProperty("name", out var name) && !string.IsNullOrWhiteSpace(name.GetString())
                ? name.GetString()!
                : $"Release {version}";

            return new UpdateInfo(version, title, url, sha256);
        }

        return null;
    }

    /// <summary>«v3.0» → 3.0.0.0; то, что на номер версии не похоже, — null.</summary>
    internal static Version? ParseTag(string? tag)
    {
        string text = (tag ?? string.Empty).Trim().TrimStart('v', 'V');
        return Version.TryParse(text, out var version) ? Normalize(version) : null;
    }

    internal static bool IsNewer(Version candidate, Version current) => Normalize(candidate) > Normalize(current);

    /// <summary>Недостающие части номера — нули, иначе 3.0 оказалась бы «меньше» 3.0.0.0.</summary>
    private static Version Normalize(Version version) =>
        new(version.Major, Math.Max(version.Minor, 0), Math.Max(version.Build, 0), Math.Max(version.Revision, 0));

    private static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        byte[] hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hash);
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Файл ещё занят — уберётся при следующем запуске.
        }
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        // Без User-Agent GitHub API отвечает 403.
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(Repository, CurrentVersion.ToString()));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }
}
