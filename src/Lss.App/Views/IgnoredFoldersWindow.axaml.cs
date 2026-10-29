using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Lss.App.Views;

/// <summary>
/// Cartelle che non sono salvataggi, con "Proponila di nuovo" per ognuna.
/// </summary>
public partial class IgnoredFoldersWindow : Window
{
    public IgnoredFoldersWindow() => InitializeComponent();

    public IgnoredFoldersWindow(Lss.App.ViewModels.IgnoredFoldersViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

}
