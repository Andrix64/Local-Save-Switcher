using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Lss.App.ViewModels;

namespace Lss.App.Views;

/// <summary>
/// "Chi sei?": il risultato è l'amministratore scelto, o null se si annulla.
/// Un admin con PIN apre il tastierino nella stessa finestra.
/// </summary>
public partial class AdminPickerWindow : Window
{
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(1) };

    public AdminPickerWindow() => InitializeComponent();

    public AdminPickerWindow(AdminPickerViewModel viewModel) : this()
    {
        DataContext = viewModel;

        // Il testo dell'attesa del PIN si aggiorna ogni secondo.
        timer.Tick += (_, _) => ViewModel.Pad?.Tick();
        timer.Start();
        Closed += (_, _) => timer.Stop();

        KeyDown += Window_KeyDown;
    }

    private AdminPickerViewModel ViewModel => (AdminPickerViewModel)DataContext!;

    /// <summary>Amministratore scelto, o null.</summary>
    public Person? Chosen => ViewModel.Chosen;

    private void Tile_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: PersonTile tile })
            return;

        ViewModel.Choose(tile.Person);

        if (ViewModel.Chosen != null)
            Close();
    }

    private void Digit_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string digit })
            PressDigit(digit);
    }

    private void Back_Click(object? sender, RoutedEventArgs e) => ViewModel.Pad?.Back();

    private void CancelPin_Click(object? sender, RoutedEventArgs e) => ViewModel.CancelPin();

    // Le cifre della tastiera valgono come quelle dei pulsanti.
    private void Window_KeyDown(object? sender, KeyEventArgs e)
    {
        if (!ViewModel.PadOpen)
            return;

        if (e.Key == Key.Back)
            ViewModel.Pad?.Back();
        else if (KeyDigit(e.Key) is string digit)
            PressDigit(digit);
    }

    private void PressDigit(string digit)
    {
        ViewModel.Pad?.Press(digit);

        if (ViewModel.CompletePin())
            Close();
    }

    private static string? KeyDigit(Key key) => key switch
    {
        >= Key.D0 and <= Key.D9 => ((char)('0' + (key - Key.D0))).ToString(),
        >= Key.NumPad0 and <= Key.NumPad9 => ((char)('0' + (key - Key.NumPad0))).ToString(),
        _ => null,
    };
}
