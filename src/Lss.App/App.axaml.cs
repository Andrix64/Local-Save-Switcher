using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Lss.App.Services;
using Lss.App.ViewModels;
using Lss.App.Views;

namespace Lss.App;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Le finestre di avviso si chiudono prima che l'app sia pronta: non deve uscire.
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            Dispatcher.UIThread.UnhandledException += (_, e) =>
            {
                Log.Write("Errore imprevisto", e.Exception);
                e.Handled = true;

                _ = ShowStandaloneAsync(Loc.F(Strings.UnexpectedErrorWithLog, e.Exception.Message, Log.FilePath), InfoKind.Error);
            };

            _ = StartSafelyAsync(desktop);
        }

        base.OnFrameworkInitializationCompleted();
    }

    // Un errore nell'avvio non deve lasciare l'app aperta senza finestre.
    private static async Task StartSafelyAsync(IClassicDesktopStyleApplicationLifetime desktop)
    {
        try
        {
            await StartAsync(desktop);
        }
        catch (Exception ex)
        {
            Log.Write("Avvio fallito", ex);
            await ShowStandaloneAsync(Loc.F(Strings.UnexpectedErrorWithLog, ex.Message, Log.FilePath), InfoKind.Error);
            desktop.Shutdown();
        }
    }

    private static async Task StartAsync(IClassicDesktopStyleApplicationLifetime desktop)
    {
        if (desktop.Args?.Contains("--already-running") == true)
        {
            await ShowStandaloneAsync(Strings.AppAlreadyRunning, InfoKind.Info);
            desktop.Shutdown();
            return;
        }

        // Compatibilità con Control Save Manager (vedi LegacyMigration).
        (LegacyMigration.MoveResult moved, string moveDetail) = LegacyMigration.Run();

        Log.Write("Avvio versione " + Program.Version);

        if (LegacyMigration.Notice(moved, moveDetail) is (string text, bool isWarning))
            await ShowStandaloneAsync(text, isWarning ? InfoKind.Warning : InfoKind.Info);

        // Fino alla K7 c'è un solo launcher: Steam.
        var steamProvider = new SteamProvider(Platform.Current);
        string? steam = steamProvider.FindRoot();

        if (steam == null)
        {
            await ShowStandaloneAsync(Strings.SteamNotFound, InfoKind.Error);
            desktop.Shutdown();
            return;
        }

        string userData = SteamProvider.UserDataFolder(steam);

        if (!Directory.Exists(userData))
        {
            await ShowStandaloneAsync(Strings.UserDataNotFound, InfoKind.Error);
            desktop.Shutdown();
            return;
        }

        // Prima di cercare gli account: una cartella rimasta come 870780.vecchio le farebbe sparire.
        SaveManager.RecoverAllGameFolders(userData);

        IGameProvider provider = steamProvider;

        // Nessun gioco gestibile (per esempio tutti con i salvataggi in Proton):
        // la finestra si apre lo stesso e mostra i giochi con il motivo.
        IReadOnlyList<GameAccount> accounts = provider.FindAccounts();

        if (accounts.Count == 0)
        {
            await ShowStandaloneAsync(Strings.NoAccountWithGames, InfoKind.Warning);
            desktop.Shutdown();
            return;
        }

        var settings = new AppSettings(Path.Combine(AppData.Root, "lss.ini"));
        settings.Load();

        GameAccount account = accounts[0];

        if (accounts.Count > 1)
        {
            var accountWindow = new AccountWindow(accounts, settings.LastAccount);
            await ShowStandaloneAsync(accountWindow);

            if (accountWindow.Chosen == null)
            {
                desktop.Shutdown();
                return;
            }

            account = accountWindow.Chosen;
            settings.LastAccount = account.Id;
            settings.Save();
        }

        IReadOnlyList<GameInfo> games = provider.FindGames(account);

        var window = new MainWindow
        {
            DataContext = new MainWindowViewModel(account, games, settings, new DialogService(() => desktop.MainWindow)),
        };

        window.Closed += (_, _) => desktop.Shutdown();
        desktop.MainWindow = window;

        // Dopo l'avvio del framework assegnare MainWindow non basta: va mostrata.
        window.Show();
    }

    // Messaggio senza finestra proprietaria (all'avvio).
    private static Task ShowStandaloneAsync(string text, InfoKind kind) =>
        ShowStandaloneAsync(MessageWindow.Inform("", text, kind));

    // Finestra senza proprietario: al centro dello schermo, nella barra delle applicazioni.
    // Ritorna quando la finestra viene chiusa.
    private static async Task ShowStandaloneAsync(Window window)
    {
        window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        window.ShowInTaskbar = true;

        var closed = new TaskCompletionSource();
        window.Closed += (_, _) => closed.TrySetResult();
        window.Show();

        await closed.Task;
    }
}
