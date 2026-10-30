using Avalonia.Controls;
using Lss.App.ViewModels;

namespace Lss.App.Views;

/// <summary>
/// Impostazioni di un gioco: ogni modifica vale subito, la finestra si chiude con la X.
/// </summary>
public partial class GameSettingsWindow : Window
{
    public GameSettingsWindow() => InitializeComponent();

    public GameSettingsWindow(GameSettingsViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }
}
