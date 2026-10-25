using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Lss.App.ViewModels;

namespace Lss.App.Views;

/// <summary>
/// Giocatori: nomi, colori, admin, ordine, aggiunta e rimozione. Le azioni
/// sono nel PlayersViewModel; qui si collegano i pulsanti di ogni riga.
/// </summary>
public partial class PlayersWindow : Window
{
    public PlayersWindow() => InitializeComponent();

    public PlayersWindow(PlayersViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    private PlayersViewModel ViewModel => (PlayersViewModel)DataContext!;

    private void Rename_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: PlayerRow row })
            ViewModel.Rename(row.Person, row.EditName);
    }

    private void Admin_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is CheckBox { Tag: PlayerRow row } box)
        {
            // Il valore nuovo è quello della casella: se il modello dice no, la riga torna com'era.
            ViewModel.SetAdmin(row.Person, box.IsChecked == true);
        }
    }

    private void Colour_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: PlayerRow row })
            ViewModel.NextColor(row.Person);
    }

    private void Up_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: PlayerRow row })
            ViewModel.Move(row.Person, -1);
    }

    private void Down_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: PlayerRow row })
            ViewModel.Move(row.Person, 1);
    }

    private async void Remove_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: PlayerRow row })
            await ViewModel.RemoveAsync(row.Person);
    }

    private void Add_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.Add(NewName.Text ?? "").Success)
            NewName.Text = "";
    }
}
