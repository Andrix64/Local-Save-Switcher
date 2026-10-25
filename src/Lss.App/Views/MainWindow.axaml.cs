using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Threading;
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
        ViewModel.Monitor.Start();
        _ = ViewModel.OnOpenedAsync();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == WindowStateProperty && OperatingSystem.IsLinux()
            && WindowState == WindowState.Maximized)
        {
            Dispatcher.UIThread.Post(KeepInsideWorkingArea, DispatcherPriority.Background);
        }
    }

    /// <summary>
    /// Su Linux (Steam Deck in modalità desktop) Avalonia ingrandisce la finestra a tutto
    /// lo schermo, sotto la barra delle applicazioni, mentre le altre app si fermano sopra.
    /// Se la finestra ingrandita esce dall'area di lavoro, la si riporta dentro come
    /// finestra normale grande quanto l'area.
    /// </summary>
    private void KeepInsideWorkingArea()
    {
        if (WindowState != WindowState.Maximized)
            return;

        Screen? screen = Screens.ScreenFromWindow(this);

        if (screen is null)
            return;

        PixelRect work = screen.WorkingArea;
        double scale = screen.Scaling;
        Size frame = FrameSize ?? ClientSize;
        double frameHeight = frame.Height * scale;
        double frameWidth = frame.Width * scale;

        Log.Write($"Finestra ingrandita: cornice {frameWidth:0}x{frameHeight:0} in {Position}, " +
                  $"area di lavoro {work}, schermo {screen.Bounds}, scala {scale}");

        if (work == screen.Bounds || (frameHeight <= work.Height + 1 && frameWidth <= work.Width + 1))
            return;

        double decorationWidth = frame.Width - ClientSize.Width;
        double decorationHeight = frame.Height - ClientSize.Height;

        WindowState = WindowState.Normal;
        Position = work.Position;
        Width = Math.Max(MinWidth, work.Width / scale - decorationWidth);
        Height = Math.Max(MinHeight, work.Height / scale - decorationHeight);

        Log.Write($"Finestra riportata nell'area di lavoro: {Width:0}x{Height:0} in {Position}");
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
