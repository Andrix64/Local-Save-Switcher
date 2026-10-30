using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Lss.App.ViewModels;

namespace Lss.App.Views;

/// <summary>
/// Trova i salvataggi di un gioco. Il risultato è true se una cartella è stata salvata.
/// Mentre il gioco è aperto, ogni due secondi si chiede se è ancora aperto.
/// </summary>
public partial class LearnSavesWindow : Window
{
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(2) };

    public LearnSavesWindow() => InitializeComponent();

    public LearnSavesWindow(LearnSavesViewModel viewModel) : this()
    {
        DataContext = viewModel;
        viewModel.Finished += saved => Close(saved);

        timer.Tick += async (_, _) =>
        {
            if (ViewModel.IsPlaying)
                await ViewModel.TickAsync();
        };

        Opened += (_, _) => timer.Start();
        Closed += (_, _) => timer.Stop();
    }

    private LearnSavesViewModel ViewModel => (LearnSavesViewModel)DataContext!;
}
