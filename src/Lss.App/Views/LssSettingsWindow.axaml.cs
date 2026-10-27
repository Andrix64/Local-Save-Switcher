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
    }

    private LssSettingsViewModel ViewModel => (LssSettingsViewModel)DataContext!;

    private async void Save_Click(object? sender, RoutedEventArgs e)
    {
        if (await ViewModel.TrySaveAsync())
            Close(true);
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(false);
}
