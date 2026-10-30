using Avalonia.Controls;
using Avalonia.Input;

namespace Lss.App.Services;

/// <summary>L'intestazione fa da barra del titolo: si trascina per spostare la finestra, doppio clic per ingrandirla.</summary>
internal static class WindowDrag
{
    public static void Begin(Control header, PointerPressedEventArgs e)
    {
        if (TopLevel.GetTopLevel(header) is not Window window || window.WindowState == WindowState.FullScreen)
            return;

        if (!e.GetCurrentPoint(header).Properties.IsLeftButtonPressed)
            return;

        if (e.ClickCount == 2 && window.CanResize)
            window.WindowState = window.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        else
            window.BeginMoveDrag(e);
    }
}
