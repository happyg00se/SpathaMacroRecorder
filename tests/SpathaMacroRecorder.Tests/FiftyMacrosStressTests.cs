using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using SpathaMacroRecorder.Models;
using SpathaMacroRecorder.Native;
using SpathaMacroRecorder.Services;

namespace SpathaMacroRecorder.Tests;

/// <summary>
/// Проверка на объёме: профиль с 50 макросами, назначения на все 8 кнопок, сохранение и
/// перезагрузка. Раньше всё проверялось на одном-двух макросах.
/// </summary>
public sealed class FiftyMacrosStressTests : IDisposable
{
    private const int MacroCount = 50;

    private readonly string _rootDirectory =
        Path.Combine(Path.GetTempPath(), "SpathaStress", Guid.NewGuid().ToString("N"));

    private ProfileManager CreateManager() =>
        new(NullLogger<ProfileManager>.Instance, Path.Combine(_rootDirectory, "profiles"));

    public void Dispose()
    {
        if (Directory.Exists(_rootDirectory))
        {
            Directory.Delete(_rootDirectory, recursive: true);
        }
    }

    private static MacroProfile BuildProfile(int macroCount = MacroCount)
    {
        var profile = new MacroProfile { ProfileName = "Stress" };

        for (int i = 1; i <= macroCount; i++)
        {
            var macro = new Macro { Id = Guid.NewGuid(), Name = $"Macro {i}" };

            // По 10 шагов на макрос — 500 шагов в профиле суммарно.
            for (int step = 0; step < 5; step++)
            {
                macro.Steps.Add(new KeyStep
                {
                    DelayBeforeMs = step * 7,
                    Action = KeyAction.Down,
                    VirtualKey = VirtualKeyCodes.A + (step % 20),
                    ScanCode = (ushort)(0x1E + step),
                    Extended = false,
                });
                macro.Steps.Add(new KeyStep
                {
                    DelayBeforeMs = 12,
                    Action = KeyAction.Up,
                    VirtualKey = VirtualKeyCodes.A + (step % 20),
                    ScanCode = (ushort)(0x1E + step),
                    Extended = false,
                });
            }

            profile.Macros.Add(macro);
        }

        return profile;
    }

    [Fact]
    public void Profile_With50Macros_SurvivesSaveAndReload()
    {
        var manager = CreateManager();
        var profile = BuildProfile();

        manager.Save(profile);
        var reloaded = Assert.Single(CreateManager().LoadAll());

        Assert.Equal(MacroCount, reloaded.Macros.Count);
        Assert.Equal(MacroCount * 10, reloaded.Macros.Sum(m => m.Steps.Count));
        Assert.Equal("Macro 1", reloaded.Macros[0].Name);
        Assert.Equal($"Macro {MacroCount}", reloaded.Macros[^1].Name);
        Assert.All(reloaded.Macros, m => Assert.All(m.Steps, s => Assert.IsType<KeyStep>(s)));
    }

    [Fact]
    public void AllEightButtons_CanBeAssignedFrom50Macros()
    {
        var profile = BuildProfile();
        var buttons = MouseButtonCatalog.Assignable.Select(b => b.ButtonId).ToList();

        Assert.Equal(8, buttons.Count);

        for (int i = 0; i < buttons.Count; i++)
        {
            MacroAssignment.Assign(profile, buttons[i], profile.Macros[i * 5]);
        }

        foreach (var (buttonId, index) in buttons.Select((b, i) => (b, i)))
        {
            Assert.Equal($"Macro {index * 5 + 1}", MacroAssignment.Find(profile, buttonId)!.Name);
        }

        // Остальные 42 макроса остались без привязки.
        Assert.Equal(8, profile.Macros.Count(m => m.Trigger is not null));
    }

    [Fact]
    public void Reassigning_LeavesExactlyOneMacroPerButton()
    {
        var profile = BuildProfile();

        // Одну и ту же кнопку 50 раз перевешиваем на разные макросы.
        foreach (var macro in profile.Macros)
        {
            MacroAssignment.Assign(profile, "macro1", macro);
        }

        Assert.Single(profile.Macros, m => m.Trigger?.ButtonId == "macro1");
        Assert.Equal($"Macro {MacroCount}", MacroAssignment.Find(profile, "macro1")!.Name);
    }

    [Fact]
    public void ClearingAssignment_DoesNotTouchOtherButtons()
    {
        var profile = BuildProfile();
        var buttons = MouseButtonCatalog.Assignable.Select(b => b.ButtonId).ToList();

        for (int i = 0; i < buttons.Count; i++)
        {
            MacroAssignment.Assign(profile, buttons[i], profile.Macros[i]);
        }

        MacroAssignment.Assign(profile, buttons[3], null);

        Assert.Null(MacroAssignment.Find(profile, buttons[3]));
        Assert.Equal(7, profile.Macros.Count(m => m.Trigger is not null));
    }

    [Fact]
    public void Assignments_SurviveSaveAndReload()
    {
        var manager = CreateManager();
        var profile = BuildProfile();
        var buttons = MouseButtonCatalog.Assignable.Select(b => b.ButtonId).ToList();

        for (int i = 0; i < buttons.Count; i++)
        {
            MacroAssignment.Assign(profile, buttons[i], profile.Macros[i * 6]);
        }

        manager.Save(profile);
        var reloaded = Assert.Single(CreateManager().LoadAll());

        for (int i = 0; i < buttons.Count; i++)
        {
            var macro = MacroAssignment.Find(reloaded, buttons[i]);
            Assert.NotNull(macro);
            Assert.Equal($"Macro {i * 6 + 1}", macro!.Name);
            // source/code должны приехать из каталога, а не потеряться при сериализации.
            Assert.Equal(MouseButtonCatalog.Find(buttons[i])!.Code, macro.Trigger!.Code);
        }
    }

    [Fact]
    public void TriggerResolution_FindsTheRightMacroAmong50()
    {
        var profile = BuildProfile();
        MacroAssignment.Assign(profile, "macro4", profile.Macros[37]);

        var resolved = TriggerBindingService.Resolve(new RawInputEvent(
            RawInputSource.Keyboard, NativeMethods.WM_KEYDOWN, VirtualKeyCodes.F20,
            ScanCode: 0, ExtendedKey: false, MouseX: 0, MouseY: 0, MouseData: 0, TimestampMs: 0));

        Assert.NotNull(resolved);
        Assert.Equal("macro4", resolved!.Value.ButtonId);
        Assert.Equal("Macro 38", MacroAssignment.Find(profile, resolved.Value.ButtonId)!.Name);
    }

    [Fact]
    public void NonAssignableButtons_AreRejected()
    {
        var profile = BuildProfile();

        MacroAssignment.Assign(profile, "lmb", profile.Macros[0]);

        Assert.Null(MacroAssignment.Find(profile, "lmb"));
        Assert.DoesNotContain(profile.Macros, m => m.Trigger is not null);
    }

    [Fact]
    public void SaveAndReload_Of50MacrosStaysFast()
    {
        var manager = CreateManager();
        var profile = BuildProfile();

        var stopwatch = Stopwatch.StartNew();
        manager.Save(profile);
        var reloaded = CreateManager().LoadAll();
        stopwatch.Stop();

        Assert.Single(reloaded);
        // Профиль сохраняется после каждой правки, поэтому запись обязана быть быстрой.
        Assert.True(stopwatch.ElapsedMilliseconds < 1500,
            $"сохранение и загрузка 50 макросов заняли {stopwatch.ElapsedMilliseconds} мс");
    }
}
