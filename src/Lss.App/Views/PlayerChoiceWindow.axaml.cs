using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Lss.App.ViewModels;

namespace Lss.App.Views;

/// <summary>
/// Sceglie una persona dall'elenco. Il risultato è la persona, o null se si annulla.
/// </summary>
public partial class PlayerChoiceWindow : Window
{
    public PlayerChoiceWindow() => InitializeComponent();

    public PlayerChoiceWindow(PlayerChoiceViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    private PlayerChoiceViewModel ViewModel => (PlayerChoiceViewModel)DataContext!;

    private void Tile_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: PersonTile tile } && ViewModel.Choose(tile.Person))
            Close(tile.Person);
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(null);
}
