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
            _ = ViewModel?.SelectPlayerAsync(tile.Person);
    }

    private void MyGames_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: SimpleGameTile tile } && ViewModel != null)
            _ = ViewModel.OpenMyGamesAsync(tile);
    }

    private void Game_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: SimpleGameTile tile } && ViewModel != null)
            _ = ViewModel.ActivateAsync(tile);
    }

    // La piastrella "Aggiungi un gioco" è nella stessa griglia dei giochi.
    private void AddGame_Click(object? sender, RoutedEventArgs e) => ViewModel?.AddGameCommand.Execute(null);
}
