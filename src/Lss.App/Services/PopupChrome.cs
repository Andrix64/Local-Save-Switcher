using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace Lss.App.Services;

/// <summary>
/// Il comportamento del telaio dei popup (Theme.axaml): la X chiude, l'intestazione sposta la finestra,
/// Esc chiude. Si registra una volta per tutte le finestre senza classe "main", così i popup non
/// hanno bisogno di codice proprio.
/// </summary>
internal static class PopupChrome
{
    public static void Register()
    {
        Button.ClickEvent.AddClassHandler<Button>(OnClick);
        InputElement.PointerPressedEvent.AddClassHandler<Border>(OnHeaderPressed);
        InputElement.KeyDownEvent.AddClassHandler<Window>(OnKeyDown);
    }

    private static bool IsPopup(Window? window) => window != null && !window.Classes.Contains("main");

    private static void OnClick(Button button, RoutedEventArgs e)
    {
        if (button.Name == "PART_PopupClose" && button.FindAncestorOfType<Window>() is { } window && IsPopup(window))
            window.Close();
    }

    private static void OnHeaderPressed(Border header, PointerPressedEventArgs e)
    {
        if (header.Name == "PART_PopupHeader" && header.FindAncestorOfType<Window>() is { } window && IsPopup(window)
            && e.GetCurrentPoint(header).Properties.IsLeftButtonPressed)
            window.BeginMoveDrag(e);
    }

    private static void OnKeyDown(Window window, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && !e.Handled && IsPopup(window))
        {
            e.Handled = true;
            window.Close();
        }
    }
}
