using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Lss.App.ViewModels;

namespace Lss.App.Views;

/// <summary>
/// Anteprima e note della configurazione dei salvataggi (N9c). Il risultato è true se il file è stato scritto.
/// </summary>
public partial class ExportConfigWindow : Window
{
    public ExportConfigWindow() => InitializeComponent();

    public ExportConfigWindow(ExportConfigViewModel viewModel) : this()
    {
        DataContext = viewModel;
        viewModel.Finished += saved => Close(saved);
    }
}
