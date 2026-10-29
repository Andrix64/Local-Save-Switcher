using Avalonia.Controls;
using Lss.App.ViewModels;

namespace Lss.App.Views;

/// <summary>
/// Impostazioni di LSS: ogni modifica vale subito, la finestra si chiude con la X.
/// </summary>
public partial class LssSettingsWindow : Window
{
    public LssSettingsWindow() => InitializeComponent();

    public LssSettingsWindow(LssSettingsViewModel viewModel) : this()
    {
        DataContext = viewModel;

        // Cambiata la modalità la finestra ha fatto il suo lavoro: si chiude.
        if (viewModel.Mode is { } mode)
        {
            mode.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ModeSwitchViewModel.IsSimple))
                    Close();
            };
        }
    }
}
