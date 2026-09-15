using System.Reflection;

namespace SpathaMacroRecorder.Services;

/// <summary>Название запущенной сборки («Тест 6») — задаётся в проекте как InformationalVersion.</summary>
internal static class AppInfo
{
    internal static string ReleaseName { get; } =
        Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? "dev";
}
