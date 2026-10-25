using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Lss.App.ViewModels;

namespace Lss.App.Views;

/// <summary>
/// "Chi sei?": il risultato è l'amministratore scelto, o null se si annulla.
/// </summary>
public partial class AdminPickerWindow : Window
{
    public AdminPickerWindow() => InitializeComponent();

    public AdminPickerWindow(AdminPickerViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    private AdminPickerViewModel ViewModel => (AdminPickerViewModel)DataContext!;

    /// <summary>Amministratore scelto, o null.</summary>
    public Person? Chosen => ViewModel.Chosen;

    private void Tile_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: AdminTile tile } && ViewModel.Choose(tile.Person))
            Close();
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close();
}
