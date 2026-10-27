using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Lss.App.ViewModels;

namespace Lss.App.Views;

/// <summary>
/// Impostazioni di un gioco. Il risultato è true se sono state salvate.
/// </summary>
public partial class GameSettingsWindow : Window
{
    public GameSettingsWindow() => InitializeComponent();

    public GameSettingsWindow(GameSettingsViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    private GameSettingsViewModel ViewModel => (GameSettingsViewModel)DataContext!;

    private async void Save_Click(object? sender, RoutedEventArgs e)
    {
        if (await ViewModel.TrySaveAsync())
            Close(true);
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(false);
}
