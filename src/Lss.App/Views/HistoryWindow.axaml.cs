using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Lss.App.ViewModels;

namespace Lss.App.Views;

/// <summary>
/// Cronologia della partita nel gioco. Il risultato dice se qualcosa è cambiato
/// (lo legge chi l'ha aperta: vedi ViewModel.Changed).
/// </summary>
public partial class HistoryWindow : Window
{
    private readonly bool focusSave;

    public HistoryWindow() => InitializeComponent();

    public HistoryWindow(HistoryViewModel viewModel, bool focusSave) : this()
    {
        DataContext = viewModel;
        this.focusSave = focusSave;
    }

    private HistoryViewModel ViewModel => (HistoryViewModel)DataContext!;

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        ViewModel.Start();

        // "Salva un punto" porta il cursore nel nome del nuovo punto.
        if (focusSave)
            SaveNameBox.Focus();
    }

    // Non interrompere una copia a metà.
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);

        if (ViewModel.Busy)
            e.Cancel = true;
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        ViewModel.Stop();
    }

}
