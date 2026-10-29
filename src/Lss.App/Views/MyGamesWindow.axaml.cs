using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Lss.App.ViewModels;

namespace Lss.App.Views;

/// <summary>
/// "Le mie partite": il risultato è la scelta (una partita, o una nuova), o null se si annulla.
/// </summary>
public partial class MyGamesWindow : Window
{
    public MyGamesWindow() => InitializeComponent();

    public MyGamesWindow(MyGamesViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    private MyGamesViewModel ViewModel => (MyGamesViewModel)DataContext!;

    private void Row_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: MyGameRow row })
        {
            ViewModel.Choose(row);
            Close(ViewModel.Choice);
        }
    }

    private void New_Click(object? sender, RoutedEventArgs e)
    {
        ViewModel.ChooseNew();
        Close(ViewModel.Choice);
    }

}
