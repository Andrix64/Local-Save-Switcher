using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using Lss.App.ViewModels;

namespace Lss.App.Views;

/// <summary>
/// Giocatori: nomi, colori, admin, PIN, ordine, aggiunta e rimozione. Le azioni
/// sono nel PlayersViewModel; qui si collegano i pulsanti e il menu ⋯ di ogni riga.
/// </summary>
public partial class PlayersWindow : Window
{
    public PlayersWindow() => InitializeComponent();

    public PlayersWindow(PlayersViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    private PlayersViewModel ViewModel => (PlayersViewModel)DataContext!;

    // Il pulsante o la voce di menu porta la riga nel Tag.
    private static PlayerRow? RowOf(object? sender) => (sender as Control)?.Tag as PlayerRow;

    private async void Add_Click(object? sender, RoutedEventArgs e) => await ViewModel.AddAsync();

    private async void Rename_Click(object? sender, RoutedEventArgs e)
    {
        if (RowOf(sender) is PlayerRow row)
            await ViewModel.RenameAsync(row.Person);
    }

    private void Admin_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is CheckBox { Tag: PlayerRow row } box)
        {
            // Il valore nuovo è quello della casella: se il modello dice no, la riga torna com'era.
            ViewModel.SetAdmin(row.Person, box.IsChecked == true);
        }
    }

    private async void Pin_Click(object? sender, RoutedEventArgs e)
    {
        if (RowOf(sender) is PlayerRow row)
            await ViewModel.SetPinAsync(row.Person);
    }

    private void RemovePin_Click(object? sender, RoutedEventArgs e)
    {
        if (RowOf(sender) is PlayerRow row)
            ViewModel.RemovePin(row.Person);
    }

    // Il colore è nel Tag del pallino; la riga è quella del pallino.
    private void Colour_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string color } swatch
            && swatch.FindAncestorOfType<ItemsControl>()?.DataContext is PlayerRow row)
        {
            ViewModel.SetColor(row.Person, color);
        }
    }

    private void Up_Click(object? sender, RoutedEventArgs e)
    {
        if (RowOf(sender) is PlayerRow row)
            ViewModel.Move(row.Person, -1);
    }

    private void Down_Click(object? sender, RoutedEventArgs e)
    {
        if (RowOf(sender) is PlayerRow row)
            ViewModel.Move(row.Person, 1);
    }

    private async void Remove_Click(object? sender, RoutedEventArgs e)
    {
        if (RowOf(sender) is PlayerRow row)
            await ViewModel.RemoveAsync(row.Person);
    }

    private void Close_Click(object? sender, RoutedEventArgs e) => Close();
}
