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
    public MainWindow()
    {
        InitializeComponent();

        // I titoli dei launcher stanno nella lista ma non si selezionano né ricevono il focus.
        GamesList.ContainerPrepared += (_, e) =>
        {
            if (e.Container.DataContext is SidebarHeader)
            {
                e.Container.Focusable = false;
                e.Container.IsHitTestVisible = false;
            }
        };
    }

    private MainWindowViewModel ViewModel => (MainWindowViewModel)DataContext!;

    // Senza barra del titolo di sistema l'intestazione sposta la finestra.
    private void Header_PointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e) =>
        WindowDrag.Begin((Control)sender!, e);

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        ViewModel.AttachWindow(new WindowControl(this));
        WindowFit.Apply(this);
        ViewModel.Monitor.Start();
        _ = ViewModel.OnOpenedAsync();
    }

    // F11 entra e esce dallo schermo intero (modalità semplice); Esc ne esce. I dialoghi hanno le loro finestre:
    // con uno aperto i tasti non arrivano qui.
    protected override void OnKeyDown(Avalonia.Input.KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Handled || !ViewModel.IsSimple)
            return;

        if (e.Key == Avalonia.Input.Key.F11)
        {
            ViewModel.FullScreen.Toggle();
            e.Handled = true;
        }
        else if (e.Key == Avalonia.Input.Key.Escape && ViewModel.FullScreen.IsFullScreen)
        {
            ViewModel.FullScreen.Leave();
            e.Handled = true;
        }
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
