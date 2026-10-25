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

    // Account e giochi di ogni launcher, come li ha letti l'avvio (o l'ultima ricarica).
    private IReadOnlyList<LauncherGames> launchers;

    // Una GameViewModel per gioco, per provider/id.
    private readonly Dictionary<string, GameViewModel> gameModels = [];

    // Giochi già aperti in lista: al primo ingresso si fa il primo avvio e il controllo delle cartelle.
    private readonly HashSet<string> checkedGames = [];

    // Dopo l'apertura della finestra le selezioni fanno subito il primo avvio (vedi OnOpenedAsync).
    private bool opened;

    internal MainWindowViewModel(IReadOnlyList<LauncherGames> launchers, AppSettings settings, IDialogService dialogs)
    {
        this.launchers = launchers;
        this.settings = settings;
        this.dialogs = dialogs;

        FillGames(launchers);

        ShowSidebar = Games.Count > 1 || OtherGames.Count > 0;
        AccountText = string.Join("  ·  ", launchers.Select(l => l.Account.DisplayName));

        // I giochi già usati restano controllati anche quando non sono selezionati.
        foreach (GameItemViewModel item in Games)
        {
            if (File.Exists(Path.Combine(AppData.Root, item.Account.Provider, item.Account.Id, item.Info.Id, "stato.ini")))
                GameFor(item);
        }

        Monitor = new GameMonitor(() => gameModels.Values);
        SelectedGame = Games.FirstOrDefault(g => AppSettings.Refers(settings.LastGame, g.Info.Provider, g.Info.Id)) ?? Games.FirstOrDefault();
    }

    public ObservableCollection<GameItemViewModel> Games { get; } = [];

    public ObservableCollection<GameItemViewModel> OtherGames { get; } = [];

    public GameMonitor Monitor { get; }

    [ObservableProperty]
    private GameItemViewModel? selectedGame;

    [ObservableProperty]
    private GameViewModel? currentGame;

    [ObservableProperty]
    private bool showSidebar;

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

                if (game.Support == SupportState.Supported)
                {
                    if (headers && firstSupported)
                        item.LauncherHeader = launcher.Provider.DisplayName;

                    firstSupported = false;
                    Games.Add(item);
                }
                else
                {
                    if (headers && firstOther)
                        item.LauncherHeader = launcher.Provider.DisplayName;

                    firstOther = false;
                    OtherGames.Add(item);
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
        launchers = launchers
            .Select(l => new LauncherGames(l.Provider, l.Account, l.Provider.FindGames(l.Account)))
            .ToList();

        if (changedKey != null && gameModels.TryGetValue(changedKey, out GameViewModel? stale) && !stale.IsBusy)
            gameModels.Remove(changedKey);

        Games.Clear();
        OtherGames.Clear();
        FillGames(launchers);

        ShowSidebar = Games.Count > 1 || OtherGames.Count > 0;
        SelectedGame = Games.FirstOrDefault(g => g.Info.Key == keepKey) ?? Games.FirstOrDefault();
    }

    /// <summary>
    /// Aggiunge a mano un gioco (Questo computer): si sceglie il programma (facoltativo)
    /// e il nome, poi si apre subito la ricerca della cartella dei salvataggi.
    /// </summary>
    [RelayCommand]
    private async Task AddGameAsync()
    {
        string? exe = await dialogs.PickFileAsync(Strings.AddGamePickProgram, UserHome());

        string initial = exe is null ? "" : Path.GetFileNameWithoutExtension(exe);

        var window = new TextPromptWindow(new TextPromptViewModel(
            Strings.AddGameTitle,
            Strings.AddGameCaption,
            initial,
            Strings.AddGameHint,
            80,
            Strings.AddGameConfirm));

        string? name = await dialogs.ShowAsync<string>(window);

        if (name is null || name.Trim() == "")
            return;

        string id = ManualGames.Add(AppData.Root, new ManualGame(name.Trim(), exe ?? "", "", ""));

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

    private GameViewModel GameFor(GameItemViewModel item)
    {
        if (gameModels.TryGetValue(item.Info.Key, out GameViewModel? existing))
            return existing;

        Result<SaveManager> open = SaveManager.Open(AppData.Root, item.Account, item.Info, item.Provider);

        if (!open.Success)
            throw new InvalidOperationException(open.Message);

        var created = new GameViewModel(open.Value!, dialogs)
        {
            Settings = settings,
            ExportAll = ExportAllConfigsAsync,
        };
        created.Refresh();

        // Tornare alla cartella automatica cambia il gioco: la lista si rilegge (vedi ReloadGames).
        created.SaveFolderChanged += () => ReloadGames(SelectedGame?.Info.Key, item.Info.Key);

        gameModels[item.Info.Key] = created;
        return created;
    }
}
