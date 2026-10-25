using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Lss.App.ViewModels;

namespace Lss.App.Views;

/// <summary>
/// Modalità semplice. I clic sulle piastrelle vanno al SimpleViewModel: qui si
/// collegano solo i pulsanti delle piastrelle (generati per ogni elemento).
/// </summary>
public partial class SimpleView : UserControl
{
    public SimpleView() => InitializeComponent();

    private SimpleViewModel? ViewModel => DataContext as SimpleViewModel;

    private void Player_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: PersonTile tile })
            ViewModel?.SelectPlayer(tile.Person);
    }

    private void Game_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: SimpleGameTile tile } && ViewModel != null)
            _ = ViewModel.ActivateAsync(tile);
    }
}
