using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using Lss;
using Lss.App.Services;
using Lss.App.ViewModels;
using Lss.App.Views;

namespace Lss.UiScreenshots;

/// <summary>
/// Disegna le finestre dell'app con dati finti e le salva come PNG. Non tocca
/// Steam né i dati veri: tutto sta in una cartella temporanea, eliminata alla fine.
/// </summary>
internal static class Program
{
    private static string output = "";

    public static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Uso: UiScreenshots <cartella di uscita> [it|en]");
            return 1;
        }

        output = Path.GetFullPath(args[0]);
        Directory.CreateDirectory(output);

        var culture = new CultureInfo(args.Length > 1 && args[1] == "en" ? "en-US" : "it-IT");
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;

        string root = Path.Combine(Path.GetTempPath(), "lss_screenshots_" + Guid.NewGuid().ToString("N"));

        // Prima di qualsiasi altra cosa: i dati e il registro restano nella cartella temporanea.
        AppData.Root = Path.Combine(root, "LSS");
        Log.FilePath = Path.Combine(root, "log.txt");
        Platform.Current = new FakePlatform();

        AppBuilder.Configure<Lss.App.App>()
            .UseSkia()
            .WithInterFont()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .SetupWithoutStarting();

        try
        {
            Render(root);
        }
        finally
        {
            try
            {
                Directory.Delete(root, true);
            }
            catch
            {
                // Cartella temporanea: se resta, non è un problema.
            }
        }

        Console.WriteLine("Immagini in " + output);
        return 0;
    }

    private static void Render(string root)
    {
        // Un account con Control (supportato) e un gioco che salva altrove.
        string account = Path.Combine(root, "userdata", "111");
        string controlSaves = Path.Combine(account, GameInfo.ControlAppId);
        Write(Path.Combine(controlSaves, "remote", "slot.sav"), "partita");
        Write(Path.Combine(controlSaves, "remotecache.vdf"), "cache");
        Write(Path.Combine(account, "814380", "remotecache.vdf"), "cache");

        var steamAccount = new GameAccount("steam", "111", "Andrix", account, DateTime.Now);
        var control = new GameInfo("steam", GameInfo.ControlAppId, "Control Ultimate Edition", "", "", "", controlSaves, SupportState.Supported);
        var sekiro = new GameInfo("steam", "814380", "Sekiro™: Shadows Die Twice", "", "", "", Path.Combine(account, "814380"), SupportState.SavesElsewhere);

        // Dati finti nella lingua delle immagini: quelle in inglese finiscono nel README.
        bool english = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en";
        string second = english ? "Second run" : "Seconda partita";
        string first = english ? "Main story" : "Storia principale";
        string removed = english ? "Old test" : "Prova vecchia";
        string note = english ? "Polaris mission, before Ahti" : "Missione Polaris, prima di Ahti";
        string error = english ? "The file is in use by another program." : "Il file è in uso da un altro programma.";

        SaveManager manager = SaveManager.Open(AppData.Root, steamAccount, control).Value!;
        manager.CreateProfile(second, true);
        manager.SetNote(SaveManager.ProfileFolderName(second), note);
        manager.CreateProfile(first, true);
        manager.CreateProfile(removed, false);
        manager.ActivateProfile(SaveManager.ProfileFolderName(first));
        manager.CreateSavestate(english ? "Before the boss" : "Prima del boss");
        manager.DeleteProfile(SaveManager.ProfileFolderName(removed));

        var dialogs = new NoDialogs();
        var settings = new AppSettings(Path.Combine(AppData.Root, "lss.ini"));

        var main = new MainWindow
        {
            Width = 900,
            Height = 640,
            DataContext = new MainWindowViewModel([new LauncherGames(new SteamProvider(new FakePlatform()), steamAccount, [control, sekiro])], settings, dialogs)
        };
        Shot(main, "01-main");

        Shot(MessageWindow.Confirm(
            Loc.F(Strings.ConfirmDeleteHeading, second),
            Loc.F(Strings.ConfirmDeleteText, Loc.F(Strings.ConfirmDeleteKeepN, 20)),
            Strings.DeletePlaythrough, destructive: true, InfoKind.Warning), "02-confirm-delete");

        Shot(MessageWindow.Inform(Strings.TitleOperationFailed, error,
            InfoKind.Warning, Strings.OpenBackup), "03-inform-error");

        Shot(new NewPlaythroughWindow(new NewPlaythroughViewModel(_ => false)), "04-new-playthrough");
        Shot(new NewPlaythroughWindow(new NewPlaythroughViewModel(_ => false, first)), "05-rename");

        Shot(new TextPromptWindow(new TextPromptViewModel(Strings.NoteTitle,
            Loc.F(Strings.NoteCaption, first).ToUpperInvariant(), english ? "Polaris mission" : "Missione Polaris", Strings.NoteHint,
            SaveManager.MaxNoteLength, Strings.SaveButton)), "06-note");

        Shot(new HistoryWindow(new HistoryViewModel(manager, dialogs,
            () => Task.FromResult(true), () => Task.FromResult(false)), focusSave: false), "07-history");

        Shot(new SettingsWindow(new SettingsViewModel(manager, dialogs)), "08-settings");
        Shot(new FirstRunWindow(new FirstRunViewModel(manager, CloudState.Enabled)), "09-first-run");

        Shot(new AccountWindow(
        [
            steamAccount,
            new GameAccount("steam", "222", "", Path.Combine(root, "userdata", "222"), null)
        ], "111"), "10-account");

        Shot(new DeletedWindow(new DeletedViewModel(manager, dialogs)), "11-deleted");

        // Come su uno Steam Deck: nessun gioco gestibile, tanti giochi Proton che salvano altrove.
        GameInfo Elsewhere(string id, string name) =>
            new("steam", id, name, "", "", "", Path.Combine(account, id), SupportState.SavesElsewhere);

        var noSupported = new MainWindow
        {
            Width = 900,
            Height = 640,
            DataContext = new MainWindowViewModel([new LauncherGames(new SteamProvider(new FakePlatform()), steamAccount,
            [
                Elsewhere("1145360", "Hades"), Elsewhere("367520", "Hollow Knight"),
                Elsewhere("413150", "Stardew Valley"), Elsewhere("753640", "Outer Wilds"),
                Elsewhere("632470", "Disco Elysium"), Elsewhere("814380", "Sekiro™: Shadows Die Twice"),
                Elsewhere("1313140", "Cult of the Lamb")
            ])], settings, dialogs)
        };
        Shot(noSupported, "12-no-supported-games");

        RenderLearn(root, steamAccount, dialogs);
    }

    /// <summary>
    /// Trova i salvataggi: i quattro passi, tre cartelle trovate, nessuna cartella,
    /// e le cartelle che non sono salvataggi. I passi si impostano a mano; la
    /// ricerca finta restituisce le cartelle del disegno, senza leggere nulla.
    /// </summary>
    private static void RenderLearn(string root, GameAccount account, IDialogService dialogs)
    {
        var provider = new SteamProvider(new FakePlatform());
        var game = new GameInfo("steam", "367520", "Hollow Knight", "", "", "", "", SupportState.NotFound);
        string documents = Path.Combine(root, "home", "Documents");
        string dataFolder = Path.Combine(AppData.Root, "steam", account.Id, game.Id);

        LearnSavesViewModel Model(LearnResult result) => new(game, account, provider, dialogs, () => DateTime.UtcNow,
            (_, _) => Task.FromResult(result));

        Shot(new LearnSavesWindow(Model(new LearnResult([], false))), "13-learn-intro");

        LearnSavesViewModel playing = Model(new LearnResult([], false));
        playing.Step = LearnStep.Playing;
        Shot(new LearnSavesWindow(playing), "14-learn-playing");

        LearnSavesViewModel looking = Model(new LearnResult([], false));
        looking.Step = LearnStep.Looking;
        Shot(new LearnSavesWindow(looking), "15-learn-looking");

        // Tre cartelle: la prima consigliata, una dei salvataggi generici, una poco probabile.
        var docs = new SearchRoot(documents, 6, false);
        string saves = Path.Combine(documents, "My Games", "Hollow Knight");
        ChangedFile Changed(string path) => new(path, 2048, DateTime.UtcNow, docs);

        var best = new SaveCandidate(saves, 15,
        [
            Changed(Path.Combine(saves, "Saves", "user1.dat")),
            Changed(Path.Combine(saves, "Saves", "user2.dat")),
            Changed(Path.Combine(saves, "Saves", "settings.dat")),
        ],
        [
            new ScoreReason(ScoreReasonKind.GameName, 6, "Hollow Knight"),
            new ScoreReason(ScoreReasonKind.SaveLikeFile, 3, "user1.dat"),
            new ScoreReason(ScoreReasonKind.ChangedFiles, 3, "3"),
            new ScoreReason(ScoreReasonKind.SaveLikePath, 3, "Saves, My Games"),
        ]);

        string quick = Path.Combine(documents, "Saves");
        var second = new SaveCandidate(quick, 6,
        [
            Changed(Path.Combine(quick, "slot1.sav")),
        ],
        [
            new ScoreReason(ScoreReasonKind.SaveLikeFile, 3, "slot1.sav"),
            new ScoreReason(ScoreReasonKind.ChangedFiles, 1, "1"),
            new ScoreReason(ScoreReasonKind.SaveLikePath, 2, "Saves"),
        ]);

        string downloads = Path.Combine(documents, "Downloads");
        var weak = new SaveCandidate(downloads, 5,
        [
            Changed(Path.Combine(downloads, "progress.bin")),
            Changed(Path.Combine(downloads, "readme.txt")),
        ],
        [
            new ScoreReason(ScoreReasonKind.ChangedFiles, 2, "2"),
            new ScoreReason(ScoreReasonKind.SaveLikeFile, 3, "progress.bin"),
        ]);

        LearnSavesViewModel found = Model(new LearnResult([best, second, weak], false));
        found.Step = LearnStep.Playing;
        ((IAsyncRelayCommand)found.QuitDoneCommand).ExecuteAsync(null).GetAwaiter().GetResult();
        Shot(new LearnSavesWindow(found), "16-learn-result");

        LearnSavesViewModel nothing = Model(new LearnResult([], false));
        nothing.Step = LearnStep.Playing;
        ((IAsyncRelayCommand)nothing.QuitDoneCommand).ExecuteAsync(null).GetAwaiter().GetResult();
        Shot(new LearnSavesWindow(nothing), "17-learn-nothing");

        // Cartelle che non sono salvataggi: una per tutti i giochi, una per questo solo.
        var ignored = new IgnoredFolders(AppData.Root);
        ignored.AddGlobal("{WinAppDataLocal}/Discord");
        ignored.AddForGame(dataFolder, Path.Combine(documents, "Launcher Logs"));
        Shot(new IgnoredFoldersWindow(new IgnoredFoldersViewModel(dataFolder, game.Name)), "18-ignored-folders");
    }

    private static void Shot(Window window, string name)
    {
        window.Show();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();

        using Bitmap? frame = window.CaptureRenderedFrame();

        if (frame != null)
        {
            using FileStream file = File.Create(Path.Combine(output, name + ".png"));
            frame.Save(file, PngBitmapEncoderOptions.Default);
        }

        window.Close();
        Console.WriteLine(name);
    }

    private static void Write(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }
}

/// <summary>Nessun processo, nessuno Steam: per i disegni bastano valori fissi.</summary>
internal sealed class FakePlatform : IPlatform
{
    public IReadOnlyList<string> FindSteamRoots() => [];
    public KnownFolders GetKnownFolders() => new("", "", "", "", "", "", "", "", "", "", "");
    public bool IsGameRunning(GameInfo game) => false;
    public DateTime? GameStartTime(GameInfo game) => null;
    public bool IsSteamRunning() => false;
    public void OpenSteamUri(string uri) { }
    public void OpenUri(string uri) { }
    public IReadOnlyList<(int Id, DateTime? Start)> FindProcessesUnder(string folder) => [];
    public bool StartProgram(string exe, string arguments, string workingDir) => false;
    public bool IsProcessRunning(string name) => false;
    public string? ReadRegistryString(string key, string name) => null;
    public IReadOnlyList<string> RegistrySubKeys(string key) => [];
    public IDisposable? AcquireSingleInstance() => new MemoryStream();
}

/// <summary>Le finestre disegnate non aprono altri dialoghi.</summary>
internal sealed class NoDialogs : IDialogService
{
    public Task<bool> InformAsync(string heading, string text, InfoKind kind, string? actionText = null) => Task.FromResult(false);
    public Task<bool> ConfirmAsync(string heading, string text, string verb, bool destructive = false, InfoKind kind = InfoKind.Warning) => Task.FromResult(false);
    public Task<T?> ShowAsync<T>(Window dialog) => Task.FromResult(default(T));
    public Task<string?> PickZipToSaveAsync(string title, string suggestedName, string filterName) => Task.FromResult<string?>(null);
    public Task<string?> PickZipToOpenAsync(string title, string filterName) => Task.FromResult<string?>(null);
    public Task<string?> PickFileAsync(string title, string startFolder) => Task.FromResult<string?>(null);
    public Task<string?> PickFolderAsync(string title, string startFolder) => Task.FromResult<string?>(null);
    public Task<bool> OpenFolderAsync(string path) => Task.FromResult(false);
}
