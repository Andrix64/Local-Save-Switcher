using System.Collections.ObjectModel;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lss.App.Services;
using Lss.App.Views;

namespace Lss.App.ViewModels;

/// <summary>
/// Le partite di un gioco e le sue azioni: la logica di MainForm (WinForms).
/// Una istanza per gioco, con il suo SaveManager.
/// </summary>
public partial class GameViewModel : ObservableObject
{
    private readonly IDialogService dialogs;

    // Ricerca dopo la partita in corso (si annulla se il gioco riparte).
    private CancellationTokenSource? lookCancel;

    // Avviso su Steam Cloud: una sola volta per sessione.
    private bool cloudWarningAccepted;

    public GameViewModel(SaveManager manager, IDialogService dialogs)
    {
        Manager = manager;
        this.dialogs = dialogs;
        UserdataNotice = Loc.F(Strings.OnlyUserdataNotice, manager.Game.Name);
        SaveFolderText = SaveFolderLine(manager);
        ShowUserdataNotice = manager.Game.Source == SaveSource.Userdata;
        ShowAutomaticFolder = manager.Game.Source is SaveSource.Chosen or SaveSource.Learned;
        RebuildSpotLines();
    }

    /// <summary>Cambia la cartella dei salvataggi del gioco (scelta o automatica): la lista va ricaricata.</summary>
    public event Action? SaveFolderChanged;

    public SaveManager Manager { get; }

    /// <summary>Dove sono i salvataggi del gioco, da dove li ha trovati LSS.</summary>
    public string SaveFolderText { get; }

    /// <summary>Solo per i giochi con la cartella in userdata: vale la nota su userdata.</summary>
    public bool ShowUserdataNotice { get; }

    /// <summary>Con una cartella scelta o imparata si può tornare a quella automatica.</summary>
    public bool ShowAutomaticFolder { get; }

    // Sessione di gioco in corso: quando è partita e con quale partita.
    public SessionTracker Session { get; } = new();

    public ToastViewModel Toast { get; } = new();

    public ObservableCollection<PlaythroughViewModel> Playthroughs { get; } = [];

    /// <summary>Le partite per persona, nell'ordine delle persone (modalità avanzata, P5).</summary>
    public ObservableCollection<PlayerGroup> PlayerGroups { get; } = [];

    // Nota sotto l'elenco: quali file LSS copia (vedi OnlyUserdataNotice).
    public string UserdataNotice { get; }

    // true mentre un'operazione sui file è in corso.
    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private bool gameRunning;

    [ObservableProperty]
    private CloudState cloud = CloudState.Unknown;

    [ObservableProperty]
    private bool showCloudBar;

    [ObservableProperty]
    private string cloudBarTitle = "";

    [ObservableProperty]
    private string cloudBarText = "";

    [ObservableProperty]
    private bool showGameBar;

    [ObservableProperty]
    private string gameBarTitle = "";

    [ObservableProperty]
    private string gameBarText = "";

    [ObservableProperty]
    private int deletedCount;

    [ObservableProperty]
    private string deletedLinkText = "";

    [ObservableProperty]
    private bool showDeletedLink;

    [ObservableProperty]
    private string emptyText = "";

    /// <summary>
    /// Testo della riga con la cartella dei salvataggi e la sua origine.
    /// </summary>
    private static string SaveFolderLine(SaveManager manager)
    {
        // Il primo posto può essere solo alcuni file della cartella (es. *.owprofile): va detto.
        SaveSpot? primary = manager.Game.Spots.FirstOrDefault(spot => spot.Id == 1);
        string folder = primary is { IsWholeFolder: false }
            ? Loc.F(Strings.SpotFiles, manager.SavePath, primary.Pattern)
            : manager.SavePath;

        return manager.Game.Source switch
        {
            SaveSource.Launcher => Loc.F(Strings.SaveFolderFromLauncher, folder, manager.LauncherName),
            SaveSource.Chosen => Loc.F(Strings.SaveFolderChosen, folder),
            SaveSource.Learned => Loc.F(Strings.SaveFolderLearned, folder),
            _ => Loc.F(Strings.SaveFolderFromSteam, folder),
        };
    }

    /// <summary>
    /// Apre la cartella dei salvataggi nel file manager.
    /// </summary>
    [RelayCommand]
    private Task OpenSaveFolderAsync() => OpenFolder(Manager.SavePath);

    /// <summary>
    /// Torna alla cartella trovata automaticamente: cancella la scelta in
    /// posti.ini e chiede di ricaricare la lista.
    /// </summary>
    [RelayCommand]
    private void UseAutomaticFolder()
    {
        new GameSaveSettings(Path.Combine(AppData.Root, Manager.Account.Provider, Manager.Account.Id, Manager.Game.Id))
        {
            Folder = "",
        }.Save();

        SaveFolderChanged?.Invoke();
    }

    /// <summary>
    /// Ricostruisce l'elenco delle cartelle dei salvataggi: il primo posto è la riga
    /// sopra, gli altri (aggiunti da LSS o trovati dalle regole) sono qui.
    /// </summary>
    private void RebuildSpotLines()
    {
        SpotLines.Clear();

        foreach (SaveSpot spot in Manager.Game.Spots.Where(spot => spot.Id != 1))
        {
            string text = spot.IsWholeFolder ? spot.Folder : Loc.F(Strings.SpotFiles, spot.Folder, spot.Pattern);
            bool added = spot.Source is SaveSource.Chosen or SaveSource.Learned or SaveSource.Imported;
            IAsyncRelayCommand? remove = added ? new AsyncRelayCommand(() => RemoveSpotAsync(spot.Id)) : null;

            SpotLines.Add(new SpotLine(spot.Id, text, spot.Folder,
                new AsyncRelayCommand(() => OpenFolder(spot.Folder)), remove));
        }
    }

    /// <summary>Cartelle dei salvataggi oltre la prima, una riga per posto.</summary>
    public ObservableCollection<SpotLine> SpotLines { get; } = [];

    /// <summary>
    /// Aggiunge un'altra cartella ai posti del gioco: si chiede la cartella, si
    /// chiede cosa fare nelle altre partite (se ci sono) e si aggiunge.
    /// </summary>
    [RelayCommand]
    private async Task AddSpotAsync()
    {
        string? folder = await dialogs.PickFolderAsync(Loc.F(Strings.AddSpot, Manager.Game.Name), Manager.SavePath);

        if (folder == null)
            return;

        await AddSpotWithChoiceAsync(new SaveSpot(0, folder, "", true, Manager.PortableFolder(folder), SaveSource.Chosen));
    }

    /// <summary>
    /// Aggiunge un posto ai posti del gioco. Con altre partite si chiede se danno una
    /// copia dei file o partono vuote (vedi ChooseFillAsync). true se è stato aggiunto.
    /// Usato anche dall'importazione di una configurazione (N9c).
    /// </summary>
    public async Task<bool> AddSpotWithChoiceAsync(SaveSpot spot)
    {
        NewSpotFill? fill = await ChooseFillAsync(spot.Folder);

        if (fill == null)
            return false;

        Result result = Manager.AddSpot(spot, fill.Value);

        if (!result.Success)
            await dialogs.InformAsync(Strings.TitleOperationFailed, result.Message, InfoKind.Warning);

        RebuildSpotLines();

        return result.Success;
    }

    /// <summary>
    /// Cosa fanno le altre partite con una cartella nuova: una copia dei file attuali
    /// o una partenza vuota. Con una sola partita basta confermare. Null se si annulla.
    /// </summary>
    private async Task<NewSpotFill?> ChooseFillAsync(string folder)
    {
        string playthrough = Manager.HasActiveProfile ? SaveManager.ProfileDisplayName(Manager.State.ActiveProfile) : "";
        string text = Loc.F(Strings.AddSpotText, folder, playthrough);

        if (Manager.GetProfiles().Count <= 1)
            return await dialogs.ConfirmAsync(Strings.AddSpot, text, Strings.AddSpot) ? NewSpotFill.CopyCurrent : null;

        if (await dialogs.ConfirmAsync(Strings.AddSpot, text, Strings.AddSpotCopy))
            return NewSpotFill.CopyCurrent;

        return await dialogs.ConfirmAsync(Strings.AddSpot, text, Strings.AddSpotEmpty) ? NewSpotFill.Empty : null;
    }

    /// <summary>Toglie un posto aggiunto, dopo la conferma. Le copie nelle partite restano.</summary>
    private async Task RemoveSpotAsync(int id)
    {
        bool confirmed = await dialogs.ConfirmAsync(Strings.RemoveSpot, Strings.RemoveSpotText, Strings.RemoveSpot,
            destructive: true);

        if (!confirmed)
            return;

        Result result = Manager.RemoveSpot(id);

        if (!result.Success)
            await dialogs.InformAsync(Strings.TitleOperationFailed, result.Message, InfoKind.Warning);

        RebuildSpotLines();
    }

    /// <summary>
    /// Esporta la configurazione dei salvataggi di questo gioco: la finestra mostra
    /// cosa c'è dentro, con le note, e scrive il file solo se si preme Salva.
    /// </summary>
    [RelayCommand]
    private async Task ExportConfigAsync() =>
        await dialogs.ShowAsync<bool>(new ExportConfigWindow(new ExportConfigViewModel(Manager, dialogs)));

    /// <summary>Le impostazioni dell'app: CercaDopoPartita dice se dopo una partita si cercano altre cartelle.</summary>
    internal AppSettings? Settings { get; set; }

    /// <summary>Proposte di cartelle dei salvataggi in attesa (vedi ReviewSuggestionsAsync).</summary>
    [ObservableProperty]
    private int pendingSuggestions;

    public string SuggestionsPendingText => Loc.F(Strings.SuggestionsPending, PendingSuggestions);

    public bool HasPendingSuggestions => PendingSuggestions > 0;

    partial void OnPendingSuggestionsChanged(int value)
    {
        OnPropertyChanged(nameof(SuggestionsPendingText));
        OnPropertyChanged(nameof(HasPendingSuggestions));
    }

    [RelayCommand]
    private Task LookAtSuggestionsAsync() => ReviewSuggestionsAsync();

    /// <summary>
    /// Cerca altre cartelle dei salvataggi in background, con priorità bassa. Si ferma
    /// se il gioco riparte (vedi OnGameState).
    /// </summary>
    /// <summary>
    /// Dopo una partita dalla modalità semplice: la stessa ricerca di cartelle, senza domande
    /// (propone solo, con l'avviso).
    /// </summary>
    public void ScanAfterSession(DateTime startedLocal) => StartLookAfterSession(startedLocal);

    private void StartLookAfterSession(DateTime startedLocal)
    {
        if (Settings?.LookAfterPlaying == false)
            return;

        lookCancel?.Cancel();
        var cancel = new CancellationTokenSource();
        lookCancel = cancel;
        DateTime startedUtc = startedLocal.ToUniversalTime();

        var thread = new Thread(() =>
        {
            try
            {
                IReadOnlyList<SpotSuggestion> found = Manager.LookAfterSession(startedUtc, cancel.Token);
                Dispatcher.UIThread.Post(() => ShowSuggestions(found.Count));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                Log.Write("Ricerca di altre cartelle dopo la partita di " + Manager.Game.Name, ex);
            }
        })
        {
            IsBackground = true,
            Priority = ThreadPriority.BelowNormal,
        };

        thread.Start();
    }

    private void ShowSuggestions(int found)
    {
        PendingSuggestions = SessionScan.Pending(Manager.DataFolder).Count;

        if (found > 0)
        {
            Toast.Show(InfoKind.Info, Loc.F(Strings.SuggestionsFound, Manager.Game.Name),
                Strings.SuggestionsLook, () => _ = ReviewSuggestionsAsync());
        }
    }

    /// <summary>
    /// Tutte le proposte in una finestra: ognuna si aggiunge ai posti, si segna come non
    /// salvataggi (per questo gioco o per tutti), oppure si lascia per dopo (N11).
    /// </summary>
    public async Task ReviewSuggestionsAsync()
    {
        var model = new SpotSuggestionsViewModel(this, SessionScan.Pending(Manager.DataFolder));

        await dialogs.ShowAsync<bool>(new SpotSuggestionsWindow(model));

        PendingSuggestions = SessionScan.Pending(Manager.DataFolder).Count;
    }

    /// <summary>
    /// Aggiunge una proposta ai posti, con la scelta di copia o vuoto per le altre partite
    /// (come Aggiungi un'altra cartella). true se è stata aggiunta: allora non è più proposta.
    /// </summary>
    public async Task<bool> AddSuggestionAsync(SpotSuggestion suggestion)
    {
        var spot = new SaveSpot(0, suggestion.Folder, suggestion.Pattern, true,
            Manager.PortableFolder(suggestion.Folder), SaveSource.Learned);

        if (!await AddSpotWithChoiceAsync(spot))
            return false;

        SessionScan.Forget(Manager.DataFolder, suggestion.Folder, suggestion.Pattern);
        PendingSuggestions = SessionScan.Pending(Manager.DataFolder).Count;

        return true;
    }

    /// <summary>
    /// La proposta non sono salvataggi: per questo gioco, o per tutti i giochi. Non torna più.
    /// </summary>
    public void IgnoreSuggestion(SpotSuggestion suggestion, bool forAll)
    {
        // File sparsi nella cartella del gioco: si scarta la proposta, non la cartella
        // (ignorarla toglierebbe dalla ricerca tutta l'installazione).
        if (suggestion.Pattern != "")
        {
            SessionScan.Dismiss(Manager.DataFolder, suggestion.Folder, suggestion.Pattern);
            PendingSuggestions = SessionScan.Pending(Manager.DataFolder).Count;
            return;
        }

        var ignored = new IgnoredFolders(AppData.Root);

        if (forAll)
            ignored.AddGlobal(Manager.PortableFolder(suggestion.Folder));
        else
            ignored.AddForGame(Manager.DataFolder, suggestion.Folder);

        SessionScan.Forget(Manager.DataFolder, suggestion.Folder, suggestion.Pattern);
        PendingSuggestions = SessionScan.Pending(Manager.DataFolder).Count;
    }

    /// <summary>
    /// Avvisa, una volta, se all'apertura è stato rimesso a posto un cambio interrotto.
    /// </summary>
    public async Task NoticeInterruptedSwitchAsync()
    {
        if (Manager.SwitchInterruptedRestored)
        {
            await dialogs.InformAsync(Strings.SwitchInterruptedTitle,
                Loc.F(Strings.SwitchInterruptedRestored, Manager.Game.Name), InfoKind.Warning);
        }
    }

    /// <summary>
    /// Rilegge stato e partite dal disco e ricostruisce le schede.
    /// </summary>
    public void Refresh()
    {
        try
        {
            Manager.ReloadState();
        }
        catch (Exception ex)
        {
            Toast.Show(InfoKind.Error, Loc.F(Strings.CannotReadState, ex.Message));
        }

        Cloud = Manager.GetCloudState();
        Playthroughs.Clear();

        try
        {
            string active = Manager.State.ActiveProfile;

            // La partita nel gioco per prima, poi le altre in ordine di nome.
            foreach (string profile in Manager.GetProfiles().OrderBy(p => p != active).ThenBy(p => p))
            {
                Playthroughs.Add(new PlaythroughViewModel(new CardData(
                    profile,
                    profile == active,
                    Manager.GetInfo(profile),
                    Manager.GetProfileLastModified(profile),
                    Manager.CountSavestates(profile)), this));
            }
        }
        catch (Exception ex)
        {
            Toast.Show(InfoKind.Error, Loc.F(Strings.CannotReadProfiles, ex.Message));
        }

        RebuildPlayerGroups();

        int deleted = 0;

        try
        {
            deleted = Manager.GetDeletedProfiles().Count;
        }
        catch
        {
            // Senza l'elenco il collegamento resta nascosto.
        }

        DeletedCount = deleted;
        DeletedLinkText = Loc.F(Strings.DeletedLink, deleted);
        ShowDeletedLink = deleted > 0;

        UpdateState();
    }

    /// <summary>
    /// true se il gioco è di Steam: gli avvisi sul cloud nominano Steam Cloud.
    /// </summary>
    private bool IsSteamGame => Manager.Game.Provider == "steam";

    /// <summary>
    /// Aggiorna le barre, il testo di elenco vuoto e lo stato delle schede.
    /// </summary>
    public void UpdateState()
    {
        EmptyText = Playthroughs.Count == 0 ? Strings.NoPlaythroughs : "";

        ShowCloudBar = Cloud == CloudState.Enabled;

        if (IsSteamGame)
        {
            CloudBarTitle = Loc.F(Strings.CloudBarTitle, Manager.Game.Name);
            CloudBarText = Loc.F(Strings.CloudBarText, Manager.Game.Name);
        }
        else
        {
            CloudBarTitle = Loc.F(Strings.CloudBarTitleGeneric, Manager.Game.Name);
            CloudBarText = Loc.F(Strings.CloudBarGeneric, Manager.Game.Name, Manager.LauncherName);
        }

        if (GameRunning)
        {
            string active = Manager.HasActiveProfile
                ? SaveManager.ProfileDisplayName(Manager.State.ActiveProfile)
                : "";

            string text = Manager.State.SaveOnGameExit && active != ""
                ? Strings.GameBarSaveOnExit + " " + Loc.F(Strings.GameBarExit, Manager.Game.Name)
                : Loc.F(Strings.GameBarExit, Manager.Game.Name);

            if (Session.Running && DateTime.Now - Session.Start is TimeSpan length && length >= TimeSpan.FromMinutes(1))
                text += "\n" + Loc.F(Strings.GameBarSession, CardText.FormatPlayTime(length));

            GameBarTitle = active == "" ? Loc.F(Strings.GameBarOpen, Manager.Game.Name) : Loc.F(Strings.GameBarPlaying, active);
            GameBarText = text;
            ShowGameBar = true;
        }
        else
        {
            ShowGameBar = false;
        }

        foreach (PlaythroughViewModel card in Playthroughs)
            card.NotifyStateChanged();
    }

    /// <summary>
    /// Carica una partita senza chiedere conferme: i progressi non salvati di
    /// quella nel gioco vengono salvati prima, ed entrambe le operazioni fanno
    /// una copia. Poi avvia il gioco, oppure offre "Annulla".
    /// </summary>
    /// <param name="offerUndo">false per il cambio fatto da "Annulla", che non si annulla.</param>
    public async Task PlayAsync(string profileName, bool launch, bool offerUndo = true)
    {
        if (IsBusy || GameRunning || !await ConfirmCloudAsync(launch))
            return;

        string name = SaveManager.ProfileDisplayName(profileName);
        string previous = Manager.State.ActiveProfile;
        bool hadPrevious = Manager.HasActiveProfile && previous != profileName;
        string previousName = SaveManager.ProfileDisplayName(previous);

        Result result = await RunBusyAsync(Loc.F(Strings.BusyLoading, name), () =>
        {
            bool saved = false;

            if (Manager.HasUnsavedProgress())
            {
                Result save = Manager.SaveProgress();

                if (!save.Success)
                    return save with { Message = Loc.F(Strings.SwitchSaveFailed, previousName, save.Message) };

                saved = true;
            }

            Result activation = Manager.ActivateProfile(profileName);

            if (!activation.Success)
                return activation;

            return activation with
            {
                Message = saved
                    ? Loc.F(Strings.LoadedAndSaved, name, previousName)
                    : Loc.F(Strings.Loaded, name)
            };
        });

        if (!result.Success)
        {
            await ShowError(result);
            return;
        }

        if (launch)
        {
            Result launched = Manager.LaunchGame();

            if (launched.Success)
                Toast.Show(InfoKind.Info, Loc.F(Strings.StartingWith, Manager.Game.Name, name));
            else
                await ShowError(launched);

            return;
        }

        if (offerUndo && hadPrevious)
        {
            Toast.Show(InfoKind.Success, result.Message, Strings.Undo,
                () => _ = PlayAsync(previous, launch: false, offerUndo: false));
        }
        else
        {
            Toast.Show(InfoKind.Success, result.Message);
        }
    }

    /// <summary>
    /// "Gioca" su una scheda: se è la partita nel gioco avvia solo il gioco,
    /// altrimenti la carica e avvia.
    /// </summary>
    public async Task PlayCardAsync(PlaythroughViewModel card)
    {
        if (IsBusy)
            return;

        if (card.IsActive)
            await LaunchOnly();
        else
            await PlayAsync(card.ProfileName, Manager.State.LaunchGame);
    }

    /// <summary>
    /// "Gioca" sulla partita già nel gioco: avvia solo il gioco.
    /// </summary>
    public async Task LaunchOnly()
    {
        if (IsBusy || GameRunning || !await ConfirmCloudAsync(launching: true))
            return;

        Result launched = Manager.LaunchGame();

        if (launched.Success)
        {
            Toast.Show(InfoKind.Info, Loc.F(Strings.StartingWith, Manager.Game.Name,
                SaveManager.ProfileDisplayName(Manager.State.ActiveProfile)));
        }
        else
        {
            await ShowError(launched);
        }
    }

    public async Task ExportAsync(string profile)
    {
        string name = SaveManager.ProfileDisplayName(profile);

        string? path = await dialogs.PickZipToSaveAsync(Strings.ExportTitle, name + ".zip",
            Loc.F(Strings.ZipFileType, Manager.Game.Name));

        if (path == null)
            return;

        Result result = await RunBusyAsync(Loc.F(Strings.BusyExporting, name),
            () => Manager.ExportProfile(profile, path));

        await ShowResult(result);
    }

    [RelayCommand]
    public async Task ImportAsync()
    {
        if (IsBusy)
            return;

        string? path = await dialogs.PickZipToOpenAsync(Strings.ImportTitle,
            Loc.F(Strings.ZipFileType, Manager.Game.Name));

        if (path == null)
            return;

        // Il proprietario si sceglie qui: con una sola persona è lei, senza domande.
        Person? owner = await PickPlayerAsync(Strings.ImportPlayerPick);

        if (owner == null)
            return;

        string imported = "";

        Result result = await RunBusyAsync(Strings.BusyImporting, () =>
        {
            Result<string> import = Manager.ImportProfile(path, owner.Id);

            if (!import.Success)
                return Result.Fail(import.Message);

            imported = import.Value!;
            return Result.Ok(Loc.F(Strings.Imported, SaveManager.ProfileDisplayName(imported)));
        });

        await ShowResult(result);
    }

    /// <summary>
    /// Elimina una partita: finisce tra le copie automatiche, da cui si può recuperare.
    /// </summary>
    public async Task DeleteAsync(string profile)
    {
        string name = SaveManager.ProfileDisplayName(profile);
        string text = Loc.F(Strings.ConfirmDeleteText, Manager.State.BackupsToKeep == 0
            ? Strings.ConfirmDeleteKeepAll
            : Loc.F(Strings.ConfirmDeleteKeepN, Manager.State.BackupsToKeep));

        int points = Manager.CountSavestates(profile);

        if (points > 0)
            text += "\n\n" + Loc.F(Strings.ConfirmDeletePoints, points);

        if (!await dialogs.ConfirmAsync(Loc.F(Strings.ConfirmDeleteHeading, name), text,
                Strings.DeletePlaythrough, destructive: true, kind: InfoKind.Warning))
            return;

        Result result = await RunBusyAsync(Strings.BusyDeleting, () => Manager.DeleteProfile(profile));

        await ShowResult(result);
    }

    [RelayCommand]
    public async Task NewAsync()
    {
        if (IsBusy)
            return;

        // Di default la partita va alla persona di quella in gioco (o alla prima).
        string defaultOwner = Manager.HasActiveProfile ? Manager.GetInfo(Manager.State.ActiveProfile).Owner : "";

        var window = new NewPlaythroughWindow(new NewPlaythroughViewModel(
            name => Manager.ProfileExists(SaveManager.ProfileFolderName(name)),
            players: Manager.Players?.All,
            defaultPlayerId: defaultOwner == "" ? Manager.DefaultOwnerId : defaultOwner)
        {
            GameName = Manager.Game.Name,
        });

        NewPlaythroughResult? choice = await dialogs.ShowAsync<NewPlaythroughResult>(window);

        if (choice == null)
            return;

        string owner = choice.PlayerId == "" ? Manager.DefaultOwnerId : choice.PlayerId;

        Result result = await RunBusyAsync(Strings.BusyCreating,
            () => Manager.CreateProfile(choice.Name, choice.CopyCurrentSaves, owner));

        await ShowResult(result);

        if (result.Success && choice.ActivateNow)
            await PlayAsync(SaveManager.ProfileFolderName(choice.Name), Manager.State.LaunchGame);
    }

    /// <summary>
    /// Le partite raggruppate per persona, nell'ordine delle persone: prima la
    /// preferita, poi le altre dalla più recente. Senza partite di questo gioco una
    /// persona non compare.
    /// </summary>
    private void RebuildPlayerGroups()
    {
        PlayerGroups.Clear();

        if (Manager.Players == null)
            return;

        foreach (Person person in Manager.Players.All)
        {
            List<PlaythroughViewModel> cards = Playthroughs
                .Where(card => card.Owner == person.Id)
                .OrderByDescending(card => card.IsFavourite)
                .ThenByDescending(card => Manager.GetProfileLastModified(card.ProfileName) ?? DateTime.MinValue)
                .ThenBy(card => card.Name)
                .ToList();

            if (cards.Count == 0)
                continue;

            string initial = person.Name.Length > 0 ? char.ToUpperInvariant(person.Name[0]).ToString() : "?";
            PlayerGroups.Add(new PlayerGroup(person, initial,
                new SolidColorBrush(Color.Parse(person.Color)), cards));
        }
    }

    /// <summary>Sceglie la preferita della sua persona: la ★ di una scheda.</summary>
    public async Task SetFavouriteAsync(PlaythroughViewModel card)
    {
        if (IsBusy)
            return;

        Result result = Manager.SetFavourite(card.Owner, card.ProfileName);
        Refresh();

        if (!result.Success)
            await ShowResult(result);
    }

    /// <summary>
    /// Dà una partita a un'altra persona. Se era la preferita, la preferita passa alla
    /// sua partita più recente (regole di SaveManager).
    /// </summary>
    public async Task GiveToAsync(string profile, string personId)
    {
        Result result = await RunBusyAsync(Strings.GiveToBusy, () => Manager.SetOwner(profile, personId));
        Refresh();

        if (!result.Success)
            await ShowResult(result);
    }

    /// <summary>
    /// "Dai a…" di una scheda: si sceglie un'altra persona e la partita le va.
    /// </summary>
    public async Task GiveToMenuAsync(string profile)
    {
        if (IsBusy || Manager.Players == null)
            return;

        string owner = Manager.GetInfo(profile).Owner;
        IReadOnlyList<Person> others = Manager.Players.All.Where(person => person.Id != owner).ToList();

        if (others.Count == 0)
        {
            Toast.Show(InfoKind.Info, Strings.NoOtherPlayer);
            return;
        }

        Person? chosen = await dialogs.ChoosePersonAsync(
            Loc.F(Strings.GiveToPick, SaveManager.ProfileDisplayName(profile)), "", others);

        if (chosen != null)
            await GiveToAsync(profile, chosen.Id);
    }

    /// <summary>Chi è il proprietario di una partita nuova o importata: con una sola persona è lei,
    /// altrimenti si chiede. Null se si annulla.</summary>
    private async Task<Person?> PickPlayerAsync(string question)
    {
        IReadOnlyList<Person> people = Manager.Players?.All ?? [];

        if (people.Count <= 1)
            return people.FirstOrDefault();

        return await dialogs.ChoosePersonAsync(question, "", people);
    }

    // Il blocco delle cartelle dei salvataggi parte chiuso. Com'è lasciato resta per tutti i
    // giochi finché l'app è aperta (non si salva su disco).
    private static bool saveFoldersOpen;

    public bool SaveFoldersOpen
    {
        get => saveFoldersOpen;
        set
        {
            if (saveFoldersOpen == value)
                return;

            saveFoldersOpen = value;
            OnPropertyChanged();
        }
    }

    /// <summary>"Cartelle dei salvataggi (2)": il numero dei posti del gioco.</summary>
    public string SaveFoldersHeader => Loc.F(Strings.SaveFoldersCount, 1 + SpotLines.Count);

    [RelayCommand]
    private void ToggleSaveFolders() => SaveFoldersOpen = !SaveFoldersOpen;

    /// <summary>Un admin ha consentito che questo gioco (con le copie online) vada in modalità semplice.</summary>
    public bool CloudAllowedInSimple => Manager.State.CloudAllowedInSimple;

    [RelayCommand]
    private void AllowCloudInSimple() => SetCloudAllowedInSimple(true);

    [RelayCommand]
    private void UndoAllowCloudInSimple() => SetCloudAllowedInSimple(false);

    private void SetCloudAllowedInSimple(bool allowed)
    {
        Result result = Manager.SetCloudAllowedInSimple(allowed);
        OnPropertyChanged(nameof(CloudAllowedInSimple));

        if (!result.Success)
            Toast.Show(InfoKind.Error, result.Message);
    }

    public async Task RenameAsync(string profile)
    {
        var window = new NewPlaythroughWindow(new NewPlaythroughViewModel(
            name => Manager.ProfileExists(SaveManager.ProfileFolderName(name)),
            SaveManager.ProfileDisplayName(profile)));

        NewPlaythroughResult? choice = await dialogs.ShowAsync<NewPlaythroughResult>(window);

        if (choice == null)
            return;

        string newName = choice.Name;

        Result result = await RunBusyAsync(Strings.BusyRenaming,
            () => Manager.RenameProfile(profile, newName));

        await ShowResult(result);
    }

    public async Task EditNoteAsync(string profile)
    {
        string name = SaveManager.ProfileDisplayName(profile);

        var window = new TextPromptWindow(new TextPromptViewModel(
            Strings.NoteTitle,
            Loc.F(Strings.NoteCaption, name).ToUpperInvariant(),
            Manager.GetInfo(profile).Note,
            Strings.NoteHint,
            SaveManager.MaxNoteLength,
            Strings.SaveButton));

        string? note = await dialogs.ShowAsync<string>(window);

        if (note == null)
            return;

        Result result = Manager.SetNote(profile, note);

        if (!result.Success)
            await ShowError(result);

        Refresh();
    }
    /// <summary>
    /// Impostazioni del gioco. true se sono state salvate: aggiorna le schede e avvisa.
    /// </summary>
    [RelayCommand]
    public async Task<bool> GameSettingsAsync()
    {
        if (IsBusy)
            return false;

        bool saved = await dialogs.ShowAsync<bool>(new GameSettingsWindow(new GameSettingsViewModel(Manager, dialogs)));

        if (saved)
        {
            Refresh();
            Toast.Show(InfoKind.Success, Strings.SettingSaved);
        }

        return saved;
    }

    /// <summary>
    /// Primo avvio: il gioco ha salvataggi ma nessuna partita. Si sceglie il nome
    /// della prima partita e si salvano i salvataggi attuali.
    /// </summary>
    public async Task OfferFirstRunAsync()
    {
        try
        {
            if (Manager.GetProfiles().Count > 0 || Manager.GameSavesAreEmpty())
                return;
        }
        catch
        {
            return;
        }

        string? name = await dialogs.ShowAsync<string>(new FirstRunWindow(new FirstRunViewModel(Manager, Cloud)));

        if (name == null)
            return;

        Result result = await RunBusyAsync(Strings.BusyCreating, () => Manager.AdoptGameSaves(name, Manager.DefaultOwnerId));

        await ShowResult(result);
    }

    /// <summary>
    /// Elenco delle partite eliminate da recuperare. Se qualcosa è cambiato, aggiorna le schede.
    /// </summary>
    [RelayCommand]
    public async Task ShowDeletedAsync()
    {
        var deleted = new DeletedViewModel(Manager, dialogs);

        await dialogs.ShowAsync<bool>(new DeletedWindow(deleted));

        if (deleted.Changed)
            Refresh();
    }

    public async Task ShowHistoryAsync(bool focusSave)
    {
        if (!Manager.HasActiveProfile)
            return;

        var history = new HistoryViewModel(Manager, dialogs,
            () => ConfirmCloudAsync(launching: false),
            GameSettingsAsync);

        await dialogs.ShowAsync<bool>(new HistoryWindow(history, focusSave));

        if (history.Changed)
            Refresh();
    }

    [RelayCommand]
    public async Task CloudHelpAsync()
    {
        if (!IsSteamGame)
        {
            await dialogs.InformAsync(Loc.F(Strings.CloudBarTitleGeneric, Manager.Game.Name),
                Loc.F(Strings.CloudOnGeneric, Manager.Game.Name, Manager.LauncherName), InfoKind.Info);
            return;
        }

        await dialogs.InformAsync(Loc.F(Strings.CloudHelpHeading, Manager.Game.Name),
            Loc.F(Strings.CloudHelpText, Manager.Game.Name), InfoKind.Info);

        Refresh();
    }

    /// <summary>
    /// Dice all'avvio quali cartelle in Profili non compaiono come partite e perché.
    /// </summary>
    public void ReportIgnoredProfileFolders()
    {
        IReadOnlyList<string> ignored;

        try
        {
            ignored = Manager.GetIgnoredProfileFolders();
        }
        catch
        {
            return;
        }

        if (ignored.Count == 0)
            return;

        string names = string.Join(", ", ignored);
        Log.Write("Cartelle ignorate in Profili (nome non valido): " + names);
        Toast.Show(InfoKind.Warning, Loc.F(Strings.IgnoredProfileFolders, names), autoHide: false);
    }

    /// <summary>
    /// Il gioco è cambiato di stato (chiamato dal monitor, ogni 2 secondi).
    /// </summary>
    public async Task OnGameState(SessionEvent e)
    {
        GameRunning = Session.Running;

        switch (e.Change)
        {
            case SessionChange.Started:
                lookCancel?.Cancel();
                Toast.Hide();
                UpdateState();
                break;

            case SessionChange.Ended:
                await OnGameClosedAsync(e);
                StartLookAfterSession(e.Start);
                break;

            default:
                if (GameRunning && !IsBusy)
                    UpdateState();

                break;
        }
    }

    /// <summary>
    /// Chiusura dell'app con una sessione aperta: il tempo fin qui conta.
    /// </summary>
    public void CloseSession()
    {
        SessionEvent e = Session.EndNow(DateTime.Now);

        if (e.Change == SessionChange.Ended && e.Profile != "")
            Manager.AddPlayTime(e.Profile, e.Start, e.End);
    }

    /// <summary>
    /// Il gioco si è chiuso: tempo di gioco e, se attivo, salvataggio dei progressi.
    /// </summary>
    private async Task OnGameClosedAsync(SessionEvent e)
    {
        if (e.Profile != "")
            Manager.AddPlayTime(e.Profile, e.Start, e.End);

        string profile = e.Profile;

        if (IsBusy || !Manager.State.SaveOnGameExit || profile == "" ||
            Manager.State.ActiveProfile != profile || !Manager.HasActiveProfile)
        {
            Refresh();
            return;
        }

        string name = SaveManager.ProfileDisplayName(profile);

        Result result = await RunBusyAsync(Loc.F(Strings.BusySavingOnExit, name), () =>
            Manager.HasUnsavedProgress()
                ? Manager.SaveProgress(onGameExit: true)
                : Result.Ok(""));

        if (!result.Success)
            await ShowError(result);
        else if (result.Message != "")
            Toast.Show(InfoKind.Success, Loc.F(Strings.SavedOnExit, name));
    }

    /// <summary>
    /// Esegue un'operazione sui file in background, con la finestra bloccata.
    /// </summary>
    private async Task<Result> RunBusyAsync(string message, Func<Result> action)
    {
        IsBusy = true;
        Toast.Show(InfoKind.Info, message, autoHide: false);
        UpdateState();

        try
        {
            return await Task.Run(action);
        }
        catch (Exception ex)
        {
            Log.Write("Errore imprevisto (" + message + ")", ex);
            return Result.Fail(Loc.F(Strings.UnexpectedError, ex.Message, Log.FilePath));
        }
        finally
        {
            IsBusy = false;
            Toast.Hide();
            Refresh();
        }
    }

    private async Task ShowResult(Result result)
    {
        if (result.Success)
            Toast.Show(InfoKind.Success, result.Message);
        else
            await ShowError(result);
    }

    /// <summary>
    /// Errore in una finestra: cosa è successo e, se c'è, il backup da aprire.
    /// </summary>
    private async Task ShowError(Result result)
    {
        bool hasBackup = result.BackupPath != null && Directory.Exists(result.BackupPath);

        bool openBackup = await dialogs.InformAsync(
            result.Critical ? Strings.TitleSeriousError : Strings.TitleOperationFailed,
            result.Message,
            result.Critical ? InfoKind.Error : InfoKind.Warning,
            hasBackup ? Strings.OpenBackup : null);

        if (openBackup)
            await OpenFolder(result.BackupPath!);
    }

    private async Task OpenFolder(string path)
    {
        if (!await dialogs.OpenFolderAsync(path))
        {
            await dialogs.InformAsync(Strings.TitleOperationFailed,
                Loc.F(Strings.CannotOpenFolder, path), InfoKind.Warning);
        }
    }

    /// <summary>
    /// Avviso su Steam Cloud, una sola volta per sessione: prima di avviare il
    /// gioco, o prima di toccare i salvataggi con Steam aperto. Con il cloud
    /// disattivato per il gioco non serve.
    /// </summary>
    private async Task<bool> ConfirmCloudAsync(bool launching)
    {
        // Fuori da Steam l'avviso serve solo se il launcher dice che il cloud è attivo.
        if (cloudWarningAccepted || Cloud == CloudState.Disabled || (!IsSteamGame && Cloud != CloudState.Enabled))
            return true;

        if (!launching && !Manager.IsLauncherRunning())
            return true;

        cloudWarningAccepted = await dialogs.ConfirmAsync(
            IsSteamGame ? Strings.CloudConfirmHeading : Strings.CloudConfirmHeadingGeneric,
            Cloud == CloudState.Enabled
                ? Loc.F(IsSteamGame ? Strings.CloudConfirmOn : Strings.CloudOnGeneric, Manager.Game.Name, Manager.LauncherName)
                : Loc.F(Strings.CloudConfirmMaybe, Manager.Game.Name),
            launching ? Strings.StartAnyway : Strings.ContinueAnyway,
            kind: InfoKind.Warning);

        return cloudWarningAccepted;
    }
}
