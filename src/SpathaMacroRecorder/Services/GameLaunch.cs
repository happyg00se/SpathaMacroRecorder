using System.Diagnostics;

namespace SpathaMacroRecorder.Services;

/// <summary>
/// Запуск вместе с игрой через Steam.
///
/// В свойствах игры в Steam, в «Параметрах запуска», пишется строка
/// <c>"C:\Spatha\SpathaMacroRecorder.exe" --game %command%</c>. Steam подставляет вместо
/// %command% собственную команду запуска игры и запускает не игру, а программу. Программа сразу
/// запускает игру ровно этой командой, а когда игра закрывается — закрывается сама.
///
/// Так программа живёт ровно столько, сколько игра: ничего не висит в фоне и не прописывается
/// в автозапуск Windows. Узнать о запуске игры без уже работающей программы по-другому нельзя —
/// разве что через системные механизмы с правами администратора, которые здесь неуместны.
///
/// Всё, что стоит после %command%, Steam дописывает в конец команды игры, и программа передаёт это
/// игре как есть. Своих параметров рендера сюда не добавляем: Helldivers 2 и так идёт на
/// DirectX 12, а лишний нераспознанный флаг она встречает чёрным окном.
/// </summary>
internal static class GameLaunch
{
    internal const string GameArgument = "--game";

    /// <summary>Команда запуска игры, полученная от Steam.</summary>
    internal sealed record Request(string ExecutablePath, IReadOnlyList<string> Arguments)
    {
        /// <summary>
        /// Имя процесса игры — имя её exe без расширения. Разделители разбираются вручную, а не
        /// через Path: путь всегда виндовский, а тесты гоняются и на других системах.
        /// </summary>
        internal string ProcessName =>
            GameWatcher.NormalizeProcessName(ExecutablePath[(ExecutablePath.LastIndexOfAny(['\\', '/']) + 1)..]);
    }

    /// <summary>Всё, что стоит после --game, — команда игры: сначала exe, потом её параметры.</summary>
    internal static Request? Parse(IReadOnlyList<string> args)
    {
        for (int i = 0; i < args.Count; i++)
        {
            if (!string.Equals(args[i], GameArgument, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (i + 1 >= args.Count)
            {
                return null;
            }

            string executable = args[i + 1].Trim().Trim('"');
            return executable.Length == 0
                ? null
                : new Request(executable, args.Skip(i + 2).ToArray());
        }

        return null;
    }

    /// <summary>
    /// Запускает игру. Переменные окружения, которые выставил Steam, игра наследует от программы,
    /// поэтому для неё ничего не меняется — Steam по-прежнему считает её запущенной через себя.
    /// </summary>
    internal static Process? Start(Request request)
    {
        var info = new ProcessStartInfo(request.ExecutablePath)
        {
            UseShellExecute = false,
            WorkingDirectory = WorkingDirectoryFor(request.ExecutablePath, Environment.CurrentDirectory),
        };

        foreach (string argument in request.Arguments)
        {
            info.ArgumentList.Add(argument);
        }

        return Process.Start(info);
    }

    /// <summary>
    /// Рабочий каталог игры. Steam запускает игру из корня её установки, а exe лежит в подпапке
    /// (у Helldivers 2 — bin). Игра ищет свои данные относительно рабочего каталога, поэтому
    /// подсунуть ей папку exe нельзя: окно откроется, а дальше чёрного экрана дело не пойдёт.
    ///
    /// Каталог, из которого Steam запустил саму программу, — это и есть корень установки игры,
    /// так что он подходит как есть, если exe игры лежит внутри него. Иначе (программу открыли
    /// вручную) берём папку exe, а у папки bin — то, что над ней.
    /// </summary>
    internal static string WorkingDirectoryFor(string executablePath, string? currentDirectory)
    {
        if (currentDirectory is not null
            && currentDirectory.Trim().Length != 0
            && Contains(currentDirectory, executablePath))
        {
            return currentDirectory;
        }

        string folder = FolderOf(executablePath);
        return NameOf(folder).Equals("bin", StringComparison.OrdinalIgnoreCase) ? FolderOf(folder) : folder;
    }

    /// <summary>Лежит ли путь внутри каталога. Разделители, как и везде здесь, разбираются вручную.</summary>
    private static bool Contains(string directory, string path)
    {
        string prefix = directory.TrimEnd('\\', '/');
        return prefix.Length != 0
            && path.Length > prefix.Length
            && (path[prefix.Length] == '\\' || path[prefix.Length] == '/')
            && path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    private static string FolderOf(string path)
    {
        int separator = path.TrimEnd('\\', '/').LastIndexOfAny(['\\', '/']);
        return separator < 0 ? string.Empty : path[..separator];
    }

    private static string NameOf(string path) => path[(path.LastIndexOfAny(['\\', '/']) + 1)..];

    /// <summary>Строка для «Параметров запуска» в Steam.</summary>
    internal static string SteamLaunchOptions(string? executablePath) =>
        $"\"{executablePath}\" {GameArgument} %command%";

    /// <summary>
    /// Программа запущена из временной папки (обычно прямо из архива) — строка для Steam указала
    /// бы на файл, которого после закрытия архива уже не будет.
    /// </summary>
    internal static bool LooksTemporary(string? executablePath) =>
        executablePath is not null
        && (executablePath.Contains(@"\Temp\", StringComparison.OrdinalIgnoreCase)
            || executablePath.Contains("Rar$", StringComparison.OrdinalIgnoreCase));
}
