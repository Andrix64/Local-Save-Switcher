using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Lss.App.ViewModels;

namespace Lss.App.Views;

/// <summary>
/// Le cartelle proposte dopo una partita, tutte insieme (N11). Il risultato è true se
/// tutte sono state decise; chiuderla lascia le altre per dopo.
/// </summary>
public partial class SpotSuggestionsWindow : Window
{
    public SpotSuggestionsWindow() => InitializeComponent();

    public SpotSuggestionsWindow(SpotSuggestionsViewModel viewModel) : this()
    {
        DataContext = viewModel;
        viewModel.Finished += done => Close(done);
    }
}
