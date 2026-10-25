using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace Lss.App.Views;

/// <summary>
/// Primo avvio: semplice o avanzata. Il risultato è AppSettings.SimpleMode o
/// AppSettings.AdvancedMode; null se la finestra si chiude senza scegliere.
/// </summary>
public partial class ModeChoiceWindow : Window
{
    public ModeChoiceWindow() => InitializeComponent();

    /// <summary>Modalità scelta, o null.</summary>
    public string? Chosen { get; private set; }

    private void Simple_Click(object? sender, RoutedEventArgs e) => Choose(AppSettings.SimpleMode);

    private void Advanced_Click(object? sender, RoutedEventArgs e) => Choose(AppSettings.AdvancedMode);

    private void Choose(string mode)
    {
        Chosen = mode;
        Close();
    }
}
