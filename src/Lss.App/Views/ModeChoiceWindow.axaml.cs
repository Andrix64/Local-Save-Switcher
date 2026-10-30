using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace Lss.App.Views;

/// <summary>
/// Primo avvio: semplice o avanzata. Si sceglie una scheda (Semplice all'inizio),
/// poi Continua. Il risultato è AppSettings.SimpleMode o AppSettings.AdvancedMode;
/// null se la finestra si chiude senza continuare.
/// </summary>
public partial class ModeChoiceWindow : Window
{
    private string selected = AppSettings.SimpleMode;

    public ModeChoiceWindow()
    {
        InitializeComponent();

        VersionText.Text = ("Local Save Switcher · v" + Program.Version).ToUpperInvariant();
        Select(AppSettings.SimpleMode);
    }

    /// <summary>Modalità scelta, o null.</summary>
    public string? Chosen { get; private set; }

    private void Simple_Click(object? sender, RoutedEventArgs e) => Select(AppSettings.SimpleMode);

    private void Advanced_Click(object? sender, RoutedEventArgs e) => Select(AppSettings.AdvancedMode);

    private void Select(string mode)
    {
        selected = mode;
        bool simple = mode == AppSettings.SimpleMode;

        SimpleCard.Classes.Set("selected", simple);
        AdvancedCard.Classes.Set("selected", !simple);
        ContinueButton.Content = Loc.F(Strings.ModeContinue, simple ? Strings.ModeSimpleTitle : Strings.ModeAdvancedTitle);
    }

    private void Continue_Click(object? sender, RoutedEventArgs e)
    {
        Chosen = selected;
        Close();
    }
}
