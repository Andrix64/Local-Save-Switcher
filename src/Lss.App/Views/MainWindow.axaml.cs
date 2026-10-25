using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Lss.App.Services;
using Lss.App.ViewModels;

namespace Lss.App.Views;

/// <summary>
/// Finestra principale: giochi a sinistra, partite del gioco selezionato.
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();

    private MainWindowViewModel ViewModel => (MainWindowViewModel)DataContext!;

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        WindowFit.Apply(this);
        ViewModel.Monitor.Start();
        _ = ViewModel.OnOpenedAsync();
    }

    // Non interrompere una copia a metà.
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);

        if (!ViewModel.IsAnyBusy)
            return;

        e.Cancel = true;
        _ = MessageWindow.Inform("", Strings.WaitForOperation, InfoKind.Info).ShowDialog<bool>(this);
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);

        // Sessione in corso alla chiusura dell'app: il tempo fin qui conta.
        ViewModel.Monitor.Stop();
        ViewModel.CloseSessions();
    }
}
