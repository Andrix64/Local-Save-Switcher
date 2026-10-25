using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Lss.App.ViewModels;

namespace Lss.App.Views;

/// <summary>
/// Importazione di una configurazione dei salvataggi (N9c). Il risultato è true se si è premuto Importa.
/// </summary>
public partial class ImportConfigWindow : Window
{
    public ImportConfigWindow() => InitializeComponent();

    public ImportConfigWindow(ImportConfigViewModel viewModel) : this()
    {
        DataContext = viewModel;
        viewModel.Finished += imported => Close(imported);
    }
}
