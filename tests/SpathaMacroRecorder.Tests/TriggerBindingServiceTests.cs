using SpathaMacroRecorder.Models;
using SpathaMacroRecorder.Native;
using SpathaMacroRecorder.Services;

namespace SpathaMacroRecorder.Tests;

public class TriggerBindingServiceTests
{
    [Fact]
    public void Resolve_MouseAndKeyboardEventsBothProduceCatalogButtonIds()
    {
        // "Вперёд" приходит с хука мыши, "Боковая 1" — с клавиатурного, но обе разрешаются
        // в обычный buttonId каталога, и вызывающий код разницы не видит.
        var fromMouse = TriggerBindingService.Resolve(
            MouseEvent(NativeMethods.WM_XBUTTONDOWN, NativeMethods.XBUTTON2));
        var fromKeyboard = TriggerBindingService.Resolve(
            KeyEvent(NativeMethods.WM_KEYDOWN, VirtualKeyCodes.F17));

        Assert.Equal(("fwd", true, false), fromMouse);
        Assert.Equal(("macro1", true, false), fromKeyboard);

        Assert.NotNull(MouseButtonCatalog.Find(fromMouse!.Value.ButtonId));
        Assert.NotNull(MouseButtonCatalog.Find(fromKeyboard!.Value.ButtonId));
    }

    [Theory]
    [InlineData(VirtualKeyCodes.F17, "macro1")]
    [InlineData(VirtualKeyCodes.F18, "macro2")]
    [InlineData(VirtualKeyCodes.F19, "macro3")]
    [InlineData(VirtualKeyCodes.F20, "macro4")]
    [InlineData(VirtualKeyCodes.F21, "macro5")]
    [InlineData(VirtualKeyCodes.F22, "macro6")]
    public void Resolve_SideButtonsMapToF17ThroughF22(int virtualKey, string expectedButtonId)
    {
        var down = TriggerBindingService.Resolve(KeyEvent(NativeMethods.WM_KEYDOWN, virtualKey));
        var up = TriggerBindingService.Resolve(KeyEvent(NativeMethods.WM_KEYUP, virtualKey));

        Assert.Equal((expectedButtonId, true, false), down);
        Assert.Equal((expectedButtonId, false, false), up);
    }

    // Настраиваются только 8 кнопок, поэтому обычные ЛКМ/ПКМ/колесо в путь триггеров не
    // попадают вовсе: иначе каждый клик в системе гонял бы лишнюю работу через хук.
    [Theory]
    [InlineData(NativeMethods.WM_LBUTTONDOWN)]
    [InlineData(NativeMethods.WM_RBUTTONDOWN)]
    [InlineData(NativeMethods.WM_MBUTTONDOWN)]
    [InlineData(NativeMethods.WM_MOUSEWHEEL)]
    public void Resolve_IgnoresButtonsThatCannotBeAssigned(int message)
    {
        Assert.Null(TriggerBindingService.Resolve(MouseEvent(message, mouseData: 120)));
    }

    [Fact]
    public void Resolve_ForwardAndBackAreTheOnlyMouseTriggers()
    {
        var fwd = TriggerBindingService.Resolve(
            MouseEvent(NativeMethods.WM_XBUTTONDOWN, NativeMethods.XBUTTON2));
        var back = TriggerBindingService.Resolve(
            MouseEvent(NativeMethods.WM_XBUTTONDOWN, NativeMethods.XBUTTON1));

        Assert.Equal(("fwd", true, false), fwd);
        Assert.Equal(("back", true, false), back);
    }

    [Fact]
    public void Resolve_IgnoresUnrelatedEvents()
    {
        Assert.Null(TriggerBindingService.Resolve(MouseEvent(NativeMethods.WM_MOUSEMOVE, 0)));
        Assert.Null(TriggerBindingService.Resolve(KeyEvent(NativeMethods.WM_KEYDOWN, VirtualKeyCodes.A)));
    }

    [Fact]
    public void Catalog_ExposesOnlyTheEightAssignableButtons()
    {
        // Настраиваемых кнопок восемь: 6 боковых плюс вперёд/назад.
        Assert.Equal(8, MouseButtonCatalog.Assignable.Count());
        Assert.Equal(2, MouseButtonCatalog.ForView(MouseView.Top).Count());
        Assert.Equal(6, MouseButtonCatalog.ForView(MouseView.Side).Count());
    }

    [Fact]
    public void CreateTrigger_FillsSourceAndCodeFromCatalog()
    {
        var trigger = MouseButtonCatalog.CreateTrigger("fwd");

        Assert.NotNull(trigger);
        Assert.Equal("fwd", trigger!.ButtonId);
        Assert.Equal(TriggerSource.MouseHook, trigger.Source);
        Assert.Equal("XBUTTON2", trigger.Code);
    }

    [Fact]
    public void DisplayName_NeverExposesRawCodes()
    {
        foreach (var definition in MouseButtonCatalog.All)
        {
            Assert.DoesNotContain("XBUTTON", definition.DisplayName, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("F1", definition.DisplayName, StringComparison.Ordinal);
            Assert.DoesNotContain("F2", definition.DisplayName, StringComparison.Ordinal);
        }
    }

    private static RawInputEvent MouseEvent(int message, int mouseData) =>
        new(RawInputSource.Mouse, message, VirtualKey: 0, ScanCode: 0, ExtendedKey: false,
            MouseX: 0, MouseY: 0, MouseData: mouseData, TimestampMs: 0);

    private static RawInputEvent KeyEvent(int message, int virtualKey) =>
        new(RawInputSource.Keyboard, message, virtualKey, ScanCode: 0, ExtendedKey: false,
            MouseX: 0, MouseY: 0, MouseData: 0, TimestampMs: 0);
}
