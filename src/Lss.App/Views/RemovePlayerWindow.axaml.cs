using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Lss.App.ViewModels;

namespace Lss.App.Views;

/// <summary>"Rimuovere …?": il risultato è la scelta per le partite, o null se si annulla.</summary>
public partial class RemovePlayerWindow : Window
{
    public RemovePlayerWindow() => InitializeComponent();

    public RemovePlayerWindow(RemovePlayerViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    private void Remove_Click(object? sender, RoutedEventArgs e) => Close(((RemovePlayerViewModel)DataContext!).Choice);

}
