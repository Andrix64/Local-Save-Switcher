using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Lss.App.Views;

/// <summary>I tasti della finestra senza cornice di sistema: chiudi, schermo intero (via FullScreen), riduci.</summary>
public partial class WindowButtons : UserControl
{
    public WindowButtons() => InitializeComponent();

    private void Close_Click(object? sender, RoutedEventArgs e) => (TopLevel.GetTopLevel(this) as Window)?.Close();

    private void Minimize_Click(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is Window window)
            window.WindowState = WindowState.Minimized;
    }
}
