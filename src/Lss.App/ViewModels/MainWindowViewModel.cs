using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lss.App.Services;
using Lss.App.Views;

namespace Lss.App.ViewModels;

/// <summary>
/// Finestra principale: la colonna dei giochi e il gioco selezionato, con la
/// sua GameViewModel (creata alla prima selezione).
/// </summary>
public partial class MainWindowViewModel : ObservableObject
{
    private readonly AppSettings settings;
    private readonly IDialogService dialogs;

    // Le persone di LSS: una sola lista per tutti i giochi, così una modifica vale ovunque.
    private readonly People? people;

    // Il giro di una partita dalla modalità semplice (null senza persone).
    private readonly SimplePlayFlow? simpleFlow;

    // Account e giochi di ogni launcher, come li ha letti l'avvio (o l'ultima ricarica).
    private IReadOnlyList<LauncherGames> launchers;

    // Una GameViewModel per gioco, per provider/id.
    private readonly Dictionary<string, GameViewModel> gameModels = [];

    // Giochi già aperti in lista: al primo ingresso si fa il primo avvio e il controllo delle cartelle.
    private readonly HashSet<string> checkedGames = [];

    // Dopo l'apertura della finestra le selezioni fanno subito il primo avvio (vedi OnOpenedAsync).
    private bool opened;

    internal MainWindowViewModel(IReadOnlyList<LauncherGames> launchers, AppSettings settings, IDialogService dialogs,
        People? people = null)
    {
        this.launchers = WithoutDuplicates(launchers);
        this.settings = settings;
        this.dialogs = dialogs;
        this.people = people;

        FillGames(this.launchers);

        ShowSidebar = Games.Count > 1 || OtherGames.Count > 0;
        AccountText = string.Join("  ·  ", launchers.Select(l => l.Account.DisplayName));

        // I giochi già usati restano controllati anche quando non sono selezionati.
        foreach (GameItemViewModel item in Games)
        {
            if (File.Exists(Path.Combine(AppData.Root, item.Account.Provider, item.Account.Id, item.Info.Id, "stato.ini")))
                GameFor(item);
        }

        Monitor = new GameMonitor(() => gameModels.Values);

        isSimple = settings.Mode == AppSettings.SimpleMode;

        Mode = new ModeSwitchViewModel(isSimple, SwitchToSimpleAsync, SwitchToAdvancedAsync, CanSwitchMode);
        FullScreen.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(FullScreenViewModel.IsFullScreen))
                RememberFullScreen();
        };

        if (people != null)
        {
            Simple = new SimpleViewModel(people, dialogs, Games,
                item => ManagerOrNull(item),
                item => FindFolderAsync(item),
                item => PlaySimpleAsync(item),
                () => AddGameAsync())
            {
                Mode = Mode,
                FullScreen = FullScreen,
                MyGames = OpenMyGamesAsync,
            };

            simpleFlow = new SimplePlayFlow(dialogs, people, () => Simple?.Unlocked ?? false);
            Simple.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(SimpleViewModel.IsPlaying))
                    Mode.Refresh();
            };
        }
        SelectedGame = Games.FirstOrDefault(g => AppSettings.Refers(settings.LastGame, g.Info.Provider, g.Info.Id)) ?? Games.FirstOrDefault();
    }

    public ObservableCollection<GameItemViewModel> Games { get; } = [];

    public ObservableCollection<GameItemViewModel> OtherGames { get; } = [];

    /// <summary>Le righe della colonna: il titolo di ogni launcher (SidebarHeader) prima dei suoi giochi.</summary>
    public ObservableCollection<object> Rows { get; } = [];

    /// <summary>Come Rows, per Altri giochi.</summary>
    public ObservableCollection<object> OtherRows { get; } = [];

    /// <summary>
    /// La riga selezionata nella colonna: il gioco selezionato. Un titolo non si può selezionare:
    /// si rimette il gioco di prima. Null (la lista che si ricostruisce) non cambia niente.
    /// </summary>
    public object? SelectedRow
    {
        get => SelectedGame;
        set
        {
            if (value is GameItemViewModel game)
                SelectedGame = game;
            else if (value is SidebarHeader)
                OnPropertyChanged(nameof(SelectedRow));
        }
    }

    public GameMonitor Monitor { get; }

    [ObservableProperty]
    private GameItemViewModel? selectedGame;

    [ObservableProperty]
    private GameViewModel? currentGame;

    [ObservableProperty]
    private bool showSidebar;

    /// <summary>La modalità semplice (chi gioca sceglie il gioco): al suo posto la vista semplice.</summary>
    [ObservableProperty]
    private bool isSimple;

    /// <summary>La vista semplice, o null se non ci sono persone.</summary>
    public SimpleViewModel? Simple { get; }

    /// <summary>L'interruttore Semplice | Avanzata, uguale nelle due intestazioni.</summary>
    public ModeSwitchViewModel Mode { get; }

    /// <summary>Lo schermo intero della modalità semplice (R7a): il pulsante e la finestra che comanda.</summary>
    public FullScreenViewModel FullScreen { get; } = new();

    /// <summary>La vista dà la finestra quando è pronta.</summary>
    internal void AttachWindow(IWindowControl window) => FullScreen.Window = window;

    // Ricorda lo schermo intero solo nella modalità semplice: l'avanzata riparte sempre in finestra.
    private void RememberFullScreen()
    {
        settings.FullScreen = FullScreen.IsFullScreen && IsSimple;
        settings.Save();
    }

    partial void OnIsSimpleChanged(bool value) => Mode.IsSimple = value;

    /// <summary>Il cambio di modalità è spento mentre si gioca dalla modalità semplice e durante un'operazione.</summary>
    private bool CanSwitchMode() => Simple is not { IsPlaying: true } && !IsAnyBusy;

    /// <summary>"Semplice" dall'interruttore: non serve nessuna autorizzazione.</summary>
    private Task SwitchToSimpleAsync()
    {
        if (Simple == null || !CanSwitchMode())
            return Task.CompletedTask;

        SetMode(AppSettings.SimpleMode);
        IsSimple = true;
        Simple.Refresh();
        return Task.CompletedTask;
    }

    /// <summary>"Avanzata" dall'interruttore della vista semplice: chiede un admin (il PIN se ce l'ha).</summary>
    private async Task SwitchToAdvancedAsync()
    {
        if (people == null || !CanSwitchMode())
            return;

        Person? admin = await dialogs.ChooseAdminAsync(people, Simple?.Current, Simple?.Unlocked ?? false);

        if (admin == null)
            return;

        FullScreen.Leave();
        SetMode(AppSettings.AdvancedMode);
        IsSimple = false;
    }

    private void SetMode(string mode)
    {
        settings.Mode = mode;
        settings.Save();
    }

    // Un gioco senza gestore (non si apre) resta senza partite nella vista semplice.
    private SaveManager? ManagerOrNull(GameItemViewModel item)
    {
        try
        {
            return GameFor(item).Manager;
        }
        catch (InvalidOperationException ex)
        {
            Log.Write("Gioco non aperto nella modalità semplice: " + ex.Message);
            return null;
        }
    }

    /// <summary>Gioca un gioco pronto con la partita preferita di chi gioca.</summary>
    private Task PlaySimpleAsync(GameItemViewModel item) =>
        RunSimpleAsync(item, (manager, player) => SimplePlay.PrepareAsync(manager, people!, player, new SimpleQuestions(dialogs)));

    /// <summary>"Le mie partite": si sceglie una partita (o una nuova) e si gioca.</summary>
    private async Task OpenMyGamesAsync(GameItemViewModel item)
    {
        if (Simple?.Current is not Person player)
            return;

        SaveManager? manager = ManagerOrNull(item);

        if (manager == null)
            return;

        MyGamesChoice? choice = await dialogs.ShowAsync<MyGamesChoice>(
            new MyGamesWindow(new MyGamesViewModel(manager, player)
            {
                Tile = Simple.Games.FirstOrDefault(tile => tile.Item == item),
            }));

        if (choice == null)
            return;

        string? profile = choice.ProfileName;

        await RunSimpleAsync(item, profile == null
            ? (m, p) => SimplePlay.PrepareNewAsync(m, p)
            : (m, p) => SimplePlay.PrepareWithAsync(m, p, profile));
    }

    /// <summary>
    /// Il giro di una partita: prepara, avvia, mostra il pannello, aspetta la fine, salva.
    /// Chi gioca è quello scelto nella vista semplice.
    /// </summary>
    private async Task RunSimpleAsync(GameItemViewModel item, Func<SaveManager, Person, Task<Result>> prepare)
    {
        if (Simple is not { } simple || simple.Current is not { } player || simpleFlow == null)
            return;

        SaveManager? manager = ManagerOrNull(item);

        if (manager == null)
            return;

        PlaySession? session = await simpleFlow.StartAsync(manager, item, player, () => prepare(manager, player));

        if (session == null)
        {
            simple.Refresh();
            return;
        }

        // A schermo intero LSS si riduce a icona mentre gira il gioco, senza prendere il primo piano,
        // e torna davanti (a schermo intero) a gioco chiuso e salvataggio fatto.
        Result done = Result.Ok("");

        await FullScreen.WhileAwayAsync(async () =>
        {
            Task finished = simple.BeginPlaying(session);
            await simpleFlow.WaitForEndAsync(session, finished);

            done = await simpleFlow.FinishAsync(session);
            simple.EndPlaying();
        });

        simple.Message = done.Message;

        // Come nella modalità avanzata: dopo la partita si cercano altre cartelle dei salvataggi.
        GameFor(item).ScanAfterSession(session.Started);
    }

    // Senza gioco gestibile si mostra il messaggio al suo posto.
    [ObservableProperty]
    private bool showNoGames = true;

    public bool HasOtherGames => OtherGames.Count > 0;

    // Senza giochi gestibili "Altri giochi" è l'unica cosa da vedere: già aperto.
    public bool OtherGamesExpanded => Games.Count == 0;

    partial void OnCurrentGameChanged(GameViewModel? value) => ShowNoGames = value == null;

    public string AccountText { get; private set; } = "";

    /// <summary>Sottotitolo dell'intestazione, con la versione.</summary>
    public string HeaderCaption => Strings.AppSubtitle + "  ·  v" + Program.Version;

    /// <summary>true se un'operazione è in corso in un gioco qualsiasi.</summary>
    public bool IsAnyBusy => gameModels.Values.Any(g => g.IsBusy);

    partial void OnSelectedGameChanged(GameItemViewModel? value)
    {
        OnPropertyChanged(nameof(SelectedRow));

        if (value == null)
        {
            CurrentGame = null;
            return;
        }

        GameViewModel game = GameFor(value);
        CurrentGame = game;
        settings.LastGame = value.Info.Key;
        settings.Save();

        // Alla prima selezione di un gioco (quelle prima dell'apertura le fa OnOpenedAsync).
        if (checkedGames.Add(value.Info.Key) && opened)
            _ = OpenGameAsync(game);
    }

    /// <summary>
    /// Finestra aperta: primo avvio e cartelle ignorate per il gioco selezionato.
    /// </summary>
    public async Task OnOpenedAsync()
    {
        opened = true;

        if (CurrentGame != null)
            await OpenGameAsync(CurrentGame);
    }

    private static async Task OpenGameAsync(GameViewModel game)
    {
        await game.NoticeInterruptedSwitchAsync();
        await game.OfferFirstRunAsync();
        game.ReportIgnoredProfileFolders();
    }

    /// <summary>
    /// Chiude le sessioni di gioco aperte alla chiusura dell'app.
    /// </summary>
    public void CloseSessions()
    {
        foreach (GameViewModel game in gameModels.Values)
            game.CloseSession();
    }

    /// <summary>
    /// Toglie i giochi che un launcher prima dell'altro ha già mostrato (vedi DuplicateGames).
    /// </summary>
    private static IReadOnlyList<LauncherGames> WithoutDuplicates(IReadOnlyList<LauncherGames> list)
    {
        IReadOnlyList<bool> keep = DuplicateGames.Keep(list.SelectMany(l => l.Games).ToList());
        var result = new List<LauncherGames>(list.Count);
        int index = 0;

        foreach (LauncherGames launcher in list)
        {
            var games = new List<GameInfo>();

            foreach (GameInfo game in launcher.Games)
            {
                if (keep[index])
                    games.Add(game);

                index++;
            }

            result.Add(new LauncherGames(launcher.Provider, launcher.Account, games));
        }

        return result;
    }

    /// <summary>
    /// Riempie le due colonne dai giochi di ogni launcher: gestibili a sinistra,
    /// gli altri in Altri giochi. Con più launcher ogni gruppo ha la sua intestazione.
    /// </summary>
    private void FillGames(IReadOnlyList<LauncherGames> list)
    {
        bool headers = list.Count > 1;

        foreach (LauncherGames launcher in list)
        {
            bool firstSupported = true;
            bool firstOther = true;

            foreach (GameInfo game in launcher.Games)
            {
                var item = new GameItemViewModel(game, launcher.Account, launcher.Provider, ChooseFolderAsync, FindFolderAsync);
                _ = item.LoadIconAsync(AppData.Root);

                if (game.Support == SupportState.Supported)
                {
                    if (headers && firstSupported)
                    {
                        item.LauncherHeader = launcher.Provider.DisplayName;
                        Rows.Add(SidebarHeader.For(item.LauncherHeader));
                    }

                    firstSupported = false;
                    Games.Add(item);
                    Rows.Add(item);
                }
                else
                {
                    if (headers && firstOther)
                    {
                        item.LauncherHeader = launcher.Provider.DisplayName;
                        OtherRows.Add(SidebarHeader.For(item.LauncherHeader));
                    }

                    firstOther = false;
                    OtherGames.Add(item);
                    OtherRows.Add(item);
                }
            }
        }
    }

    /// <summary>
    /// Rilegge i giochi di ogni launcher e ricostruisce le colonne. Resta
    /// selezionato il gioco di prima (keepKey). changedKey è il gioco la cui
    /// cartella è cambiata: la sua GameViewModel, con il vecchio SaveManager, va buttata.
    /// </summary>
    private void ReloadGames(string? keepKey, string? changedKey)
    {
        launchers = WithoutDuplicates(launchers
            .Select(l => new LauncherGames(l.Provider, l.Account, l.Provider.FindGames(l.Account)))
            .ToList());

        if (changedKey != null && gameModels.TryGetValue(changedKey, out GameViewModel? stale) && !stale.IsBusy)
            gameModels.Remove(changedKey);

        Games.Clear();
        OtherGames.Clear();
        Rows.Clear();
        OtherRows.Clear();
        FillGames(launchers);

        ShowSidebar = Games.Count > 1 || OtherGames.Count > 0;
        SelectedGame = Games.FirstOrDefault(g => g.Info.Key == keepKey) ?? Games.FirstOrDefault();
        OnPropertyChanged(nameof(SelectedRow));
    }

    /// <summary>
    /// Aggiunge a mano un gioco (Questo computer): si sceglie il programma (obbligatorio)
    /// e il nome, poi si apre subito la ricerca della cartella dei salvataggi.
    /// </summary>
    [RelayCommand]
    private async Task AddGameAsync()
    {
        string? id = await new AddGameFlow(dialogs).AddAsync(AppData.Root, UserHome());

        if (id is null)
            return;

        // Il launcher "Questo computer" entra nell'elenco la prima volta.
        if (!launchers.Any(l => l.Provider.Id == "manual"))
        {
            var manual = new ManualProvider(Platform.Current);
            var account = new GameAccount("manual", "local", manual.DisplayName, ManualGames.AccountFolder(AppData.Root), null);
            launchers = [.. launchers, new LauncherGames(manual, account, [])];
        }

        string key = "manual/" + id;
        ReloadGames(key, null);

        GameItemViewModel? added = OtherGames.FirstOrDefault(g => g.Info.Key == key) ?? Games.FirstOrDefault(g => g.Info.Key == key);

        if (added is not null)
            await FindFolderAsync(added);
    }

    private static string UserHome() => Platform.Current.GetKnownFolders().Home;

    /// <summary>
    /// Sceglie a mano la cartella dei salvataggi di un gioco di Altri giochi;
    /// se viene salvata, la lista si ricarica.
    /// </summary>
    private async Task ChooseFolderAsync(GameItemViewModel item)
    {
        GameItemViewModel? selected = SelectedGame;
        bool saved = await SaveFolderChoice.ChooseAsync(dialogs, item.Info, item.Account, item.Provider);

        if (saved)
            ReloadGames(selected?.Info.Key, item.Info.Key);
    }

    /// <summary>
    /// Apre "Trova i salvataggi" per un gioco di Altri giochi; se una cartella
    /// viene salvata, la lista si ricarica.
    /// </summary>
    private async Task FindFolderAsync(GameItemViewModel item)
    {
        GameItemViewModel? selected = SelectedGame;

        var model = new LearnSavesViewModel(item.Info, item.Account, item.Provider, dialogs, () => DateTime.UtcNow,
            (since, cancel) => LearnScanner.ScanAsync(item.Info, item.Account, item.Provider, since, cancel));

        bool? saved = await dialogs.ShowAsync<bool>(new LearnSavesWindow(model));

        if (saved == true)
            ReloadGames(selected?.Info.Key, item.Info.Key);
    }

    /// <summary>
    /// Importa un file di configurazione dei salvataggi: per ogni gioco che c'è qui si
    /// mostra cosa succede, e si scrive solo quello che l'utente conferma.
    /// </summary>
    [RelayCommand]
    private async Task ImportConfigAsync()
    {
        string? path = await dialogs.PickConfigToOpenAsync(Strings.ImportConfigTitle);

        if (path == null)
            return;

        Result<SaveConfigFile> read;

        try
        {
            read = SaveConfig.Read(File.ReadAllText(path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            read = Result<SaveConfigFile>.Fail(Loc.F(Strings.ConfigInvalid, ex.Message));
        }

        if (!read.Success)
        {
            await dialogs.InformAsync(Strings.ImportConfigTitle, read.Message, InfoKind.Warning);
            return;
        }

        SaveConfigFile file = read.Value!;
        IReadOnlyList<ConfigGamePlan> plans = SaveConfigImport.Plan(file, Games.Select(item => item.Info).ToList(),
            info => ContextOf(info), info => LauncherFoldersOf(info), AppData.Root);

        var model = new ImportConfigViewModel(file, plans, info => ItemOf(info)!.Provider.DisplayName, ApplyConfigAsync);

        if (await dialogs.ShowAsync<bool>(new ImportConfigWindow(model)) != true)
            return;

        await dialogs.InformAsync(Strings.ImportConfigTitle, Loc.F(Strings.ConfigImported, model.Applied), InfoKind.Info);
        ReloadGames(SelectedGame?.Info.Key, SelectedGame?.Info.Key);
    }

    /// <summary>
    /// Scrive i posti importabili di un gioco (vedi SaveConfigImport.ApplyAsync). Con partite
    /// ogni posto chiede copia o vuoto; la scheda del gioco si ricrea alla prossima apertura.
    /// </summary>
    private async Task<int> ApplyConfigAsync(ConfigGamePlan plan)
    {
        GameItemViewModel item = ItemOf(plan.Game!)!;
        string dataFolder = Path.Combine(AppData.Root, item.Account.Provider, item.Account.Id, plan.Game!.Id);
        bool playthroughs = SaveFolderChoice.HasPlaythroughs(dataFolder);
        GameViewModel game = GameFor(item);

        int added = await SaveConfigImport.ApplyAsync(plan, dataFolder, playthroughs, game.AddSpotWithChoiceAsync);

        // Il gioco cambia: la sua scheda, con i posti di prima, va ricreata.
        if (added > 0 && !game.IsBusy)
            gameModels.Remove(item.Info.Key);

        return added;
    }

    /// <summary>
    /// Esporta in un solo file la configurazione di ogni gioco che ha almeno un posto
    /// scelto, imparato o importato (N9c). Il file si scrive solo se si sceglie dove.
    /// </summary>
    public async Task ExportAllConfigsAsync()
    {
        var games = new List<SaveConfigGame>();

        foreach (GameItemViewModel item in Games)
        {
            SaveManager manager = GameFor(item).Manager;

            if (manager.Game.Spots.Any(spot => spot.Source is SaveSource.Chosen or SaveSource.Learned or SaveSource.Imported))
                games.Add(manager.ConfigGame("").Game);
        }

        if (games.Count == 0)
        {
            await dialogs.InformAsync(Strings.ExportAllConfigs, Strings.ConfigExportNone, InfoKind.Info);
            return;
        }

        string platform = OperatingSystem.IsLinux() ? "Linux" : "Windows";
        string? path = await dialogs.PickConfigToSaveAsync(Strings.ExportAllConfigs, $"LSS - {platform}.lss-config.json");

        if (path == null)
            return;

        try
        {
            File.WriteAllText(path, SaveConfig.Write(SaveConfig.CurrentFile(games)));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            await dialogs.InformAsync(Strings.TitleOperationFailed, ex.Message, InfoKind.Warning);
        }
    }

    private GameItemViewModel? ItemOf(GameInfo info) => Games.FirstOrDefault(game => game.Info.Key == info.Key);

    private SaveContext ContextOf(GameInfo info)
    {
        GameItemViewModel item = ItemOf(info)!;
        return SaveLookup.ContextFor(item.Provider, item.Account, info, Platform.Current.GetKnownFolders());
    }

    private IReadOnlyList<string> LauncherFoldersOf(GameInfo info)
    {
        GameItemViewModel item = ItemOf(info)!;
        return item.Provider.LauncherFolders(item.Account);
    }

    /// <summary>"Giocatori…": la finestra con le persone di LSS.</summary>
    [RelayCommand]
    private async Task OpenPlayersAsync()
    {
        if (people == null)
            return;

        await dialogs.ShowAsync<bool>(new PlayersWindow(new PlayersViewModel(people, dialogs, RemovePlayer)));

        // Nomi e colori compaiono nelle partite di ogni gioco aperto.
        foreach (GameViewModel game in gameModels.Values)
            game.Refresh();
    }

    /// <summary>
    /// Toglie una persona da LSS: le sue partite di tutti i giochi vanno a <paramref name="giveTo"/>,
    /// o si eliminano (null). Prima si controlla tutto (regole di People, partite nel gioco
    /// aperto), poi si cambia.
    /// </summary>
    internal Result RemovePlayer(Person person, string? giveTo)
    {
        if (people == null)
            return Result.Fail(Strings.PlayerNotFound);

        if (people.All.Count == 1)
            return Result.Fail(Strings.LastPlayer);

        if (person.IsAdmin && people.Admins.Count == 1)
            return Result.Fail(Strings.LastAdmin);

        var owned = new List<(GameViewModel Game, string Profile)>();

        foreach (GameItemViewModel item in Games.ToList())
        {
            GameViewModel game;

            try
            {
                game = GameFor(item);
            }
            catch (InvalidOperationException ex)
            {
                // Un gioco che non si apre non ha partite da spostare: si salta.
                Log.Write("Gioco saltato togliendo una persona: " + ex.Message);
                continue;
            }

            foreach (string profile in game.Manager.GetProfilesOf(person.Id))
                owned.Add((game, profile));
        }

        if (giveTo == null && owned.Any(item => item.Game.Manager.HasActiveProfile &&
                                                item.Game.Manager.State.ActiveProfile == item.Profile))
        {
            return Result.Fail(Strings.CannotDeleteActive);
        }

        foreach ((GameViewModel game, string profile) in owned)
        {
            Result moved = giveTo == null
                ? game.Manager.DeleteProfile(profile)
                : game.Manager.SetOwner(profile, giveTo);

            if (!moved.Success)
                return moved;
        }

        Result removed = people.Remove(person.Id);

        foreach (GameViewModel game in gameModels.Values)
            game.Refresh();

        return removed;
    }

    private GameViewModel GameFor(GameItemViewModel item)
    {
        if (gameModels.TryGetValue(item.Info.Key, out GameViewModel? existing))
            return existing;

        Result<SaveManager> open = SaveManager.Open(AppData.Root, item.Account, item.Info, item.Provider);

        if (!open.Success)
            throw new InvalidOperationException(open.Message);

        if (people != null)
            open.Value!.Players = people;

        var created = new GameViewModel(open.Value!, dialogs)
        {
            Settings = settings,
            ExportAll = ExportAllConfigsAsync,
            OpenPlayers = OpenPlayersAsync,
        };
        created.Refresh();

        // Tornare alla cartella automatica cambia il gioco: la lista si rilegge (vedi ReloadGames).
        created.SaveFolderChanged += () => ReloadGames(SelectedGame?.Info.Key, item.Info.Key);

        created.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(GameViewModel.IsBusy))
                Mode.Refresh();
        };

        gameModels[item.Info.Key] = created;
        return created;
    }
}
