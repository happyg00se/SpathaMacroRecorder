using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using SpathaMacroRecorder.Models;
using SpathaMacroRecorder.Native;
using SpathaMacroRecorder.Services;

namespace SpathaMacroRecorder.Tests;

public class DiContainerTests
{
    [Fact]
    public void AllNonUiServicesResolve()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(_ => Stopwatch.StartNew());
        services.AddSingleton<InputEventQueue>();
        services.AddSingleton<GlobalKeyboardHook>();
        services.AddHostedService(sp => sp.GetRequiredService<GlobalKeyboardHook>());
        services.AddSingleton<GlobalMouseHook>();
        services.AddHostedService(sp => sp.GetRequiredService<GlobalMouseHook>());
        services.AddSingleton<InputEventDispatcher>();
        services.AddHostedService(sp => sp.GetRequiredService<InputEventDispatcher>());
        services.AddSingleton<TriggerBindingService>();
        services.AddHostedService(sp => sp.GetRequiredService<TriggerBindingService>());
        services.AddSingleton<MacroRecorder>();
        services.AddSingleton<MacroPlayer>();
        services.AddSingleton<ProfileManager>();
        services.AddSingleton<AppSettingsService>();

        var provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

        Assert.Equal(4, provider.GetServices<IHostedService>().Count());
        Assert.NotNull(provider.GetRequiredService<AppSettingsService>());
    }

    [Fact]
    public void Settings_RoundTripThroughDisk()
    {
        string dir = Path.Combine(Path.GetTempPath(), "SpathaSettingsTest", Guid.NewGuid().ToString("N"));
        string file = Path.Combine(dir, "settings.json");
        try
        {
            var service = new AppSettingsService(NullLogger<AppSettingsService>.Instance, file);
            service.Settings.PanicKeyVirtualKey = 0x7B;
            service.Settings.UseAbsoluteMovement = true;
            service.Save();

            var reloaded = new AppSettingsService(NullLogger<AppSettingsService>.Instance, file);

            Assert.Equal(0x7B, reloaded.Settings.PanicKeyVirtualKey);
            Assert.True(reloaded.Settings.UseAbsoluteMovement);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Settings_CorruptedFileFallsBackToDefaults()
    {
        string dir = Path.Combine(Path.GetTempPath(), "SpathaSettingsTest", Guid.NewGuid().ToString("N"));
        string file = Path.Combine(dir, "settings.json");
        Directory.CreateDirectory(dir);
        File.WriteAllText(file, "{ broken");
        try
        {
            var service = new AppSettingsService(NullLogger<AppSettingsService>.Instance, file);
            Assert.Equal(VirtualKeyCodes.Pause, service.Settings.PanicKeyVirtualKey);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
