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
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        PopupChrome.Register();
    }

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

        // Cartelle degli account sotto steam\ (dalle beta): prima di leggere i dati.
        V1Compat.MoveBetaLayout(AppData.Root); // V1Compat: cartelle dati della 1.0 e delle beta
        V1Compat.ConvertAppFiles(AppData.Root); // V1Compat: impostazioni .ini della 1.0 in JSON
        V1Compat.ConvertGameFiles(AppData.Root); // V1Compat: file .ini dei giochi della 1.0 in JSON

        Log.Write("Avvio versione " + Program.Version);

        if (LegacyMigration.Notice(moved, moveDetail) is (string text, bool isWarning))
            await ShowStandaloneAsync(text, isWarning ? InfoKind.Warning : InfoKind.Info);

        // Steam resta obbligatorio: i suoi controlli di cartella sono qui, prima degli altri launcher.
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

        var settings = new AppSettings(AppSettings.FileIn(AppData.Root));
        settings.Load();

        // Primo avvio (non c'era lss.json, nemmeno da convertire): semplice o avanzata.
        if (settings.FirstStart)
        {
            var modeWindow = new ModeChoiceWindow();
            await ShowStandaloneAsync(modeWindow);

            settings.Mode = modeWindow.Chosen ?? AppSettings.AdvancedMode;
            settings.Save();
        }

        // Un account per launcher si usa senza domande; con più account dello
        // stesso launcher si chiede quale usare. Annullare salta quel launcher.
        var launchers = new List<LauncherGames>();
        bool cancelled = false;

        foreach (IGameProvider provider in Providers.All(Platform.Current))
        {
            // Nessun gioco gestibile (per esempio tutti con i salvataggi in Proton):
            // la finestra si apre lo stesso e mostra i giochi con il motivo.
            IReadOnlyList<GameAccount> accounts = provider.FindAccounts();

            if (accounts.Count == 0)
                continue;

            GameAccount? account = accounts[0];

            if (accounts.Count > 1)
            {
                var accountWindow = new AccountWindow(accounts, settings.LastAccount, provider.DisplayName);
                await ShowStandaloneAsync(accountWindow);

                account = accountWindow.Chosen;
                cancelled |= account == null;

                if (account == null)
                    continue;

                settings.LastAccount = account.Key;
                settings.Save();
            }

            launchers.Add(new LauncherGames(provider, account, provider.FindGames(account)));
        }

        if (launchers.Count == 0)
        {
            // Uscita come prima: senza un account scelto, l'app si chiude in silenzio.
            if (!cancelled)
                await ShowStandaloneAsync(Strings.NoAccountWithGames, InfoKind.Warning);

            desktop.Shutdown();
            return;
        }

        // Le persone di LSS: una lista sola, condivisa da tutti i giochi.
        var people = new People(AppData.Root);
        people.Load();

        var window = new MainWindow
        {
            DataContext = new MainWindowViewModel(launchers, settings, new DialogService(() => desktop.MainWindow), people),
        };

        // La modalità semplice parte sempre a schermo intero (come una console); si esce con il pulsante o Esc.
        if (settings.Mode == AppSettings.SimpleMode)
            window.WindowState = WindowState.FullScreen;

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
        window.Opened += (_, _) => WindowFit.Apply(window);
        window.Show();

        await closed.Task;
    }
}
