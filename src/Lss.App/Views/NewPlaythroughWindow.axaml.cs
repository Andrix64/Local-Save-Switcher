using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Lss.App.ViewModels;

namespace Lss.App.Views;

/// <summary>
/// Nuova partita o rinomina. Il risultato è null se annullata.
/// </summary>
public partial class NewPlaythroughWindow : Window
{
    public NewPlaythroughWindow() => InitializeComponent();

    public NewPlaythroughWindow(NewPlaythroughViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    private NewPlaythroughViewModel ViewModel => (NewPlaythroughViewModel)DataContext!;

    private void Create_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        Close(ViewModel.CreateResult());

}
