using Avalonia.Controls;
using Avalonia.Interactivity;
using Lss.App.ViewModels;

namespace Lss.App.Views;

/// <summary>
/// Impostazioni di LSS. Il risultato è true se sono state salvate.
/// </summary>
public partial class LssSettingsWindow : Window
{
    public LssSettingsWindow() => InitializeComponent();

    public LssSettingsWindow(LssSettingsViewModel viewModel) : this()
    {
        DataContext = viewModel;

        // Cambiata la modalità la finestra ha fatto il suo lavoro: salva e si chiude.
        if (viewModel.Mode is { } mode)
        {
            mode.PropertyChanged += async (_, e) =>
            {
                if (e.PropertyName == nameof(ModeSwitchViewModel.IsSimple) && await viewModel.TrySaveAsync())
                    Close(true);
            };
        }
    }

    private LssSettingsViewModel ViewModel => (LssSettingsViewModel)DataContext!;

    private async void Save_Click(object? sender, RoutedEventArgs e)
    {
        if (await ViewModel.TrySaveAsync())
            Close(true);
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(false);
}
