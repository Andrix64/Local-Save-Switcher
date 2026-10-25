using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lss.App.Services;

namespace Lss.App.ViewModels;

/// <summary>Passi della procedura "Trova i salvataggi".</summary>
public enum LearnStep
{
    /// <summary>Il testo iniziale e il pulsante per avviare il gioco.</summary>
    Intro,

    /// <summary>Il gioco è aperto: si aspetta che venga chiuso.</summary>
    Playing,

    /// <summary>Si cercano i file cambiati sul disco.</summary>
    Looking,

    /// <summary>Le cartelle trovate, o il messaggio che non se ne sono trovate.</summary>
    Result,
}

/// <summary>
/// Trova la cartella dei salvataggi di un gioco: il gioco parte (o lo avvia
/// l'utente), poi viene chiuso, e LSS guarda quali file sono cambiati da quando
/// è partito. Il tempo e la ricerca sono parametri, così i test non aspettano.
/// </summary>
public sealed partial class LearnSavesViewModel : ObservableObject
{
    // Il gioco deve risultare chiuso per questo tempo prima di cercare.
    private static readonly TimeSpan QuitSettle = TimeSpan.FromSeconds(4);

    // Senza aver visto il gioco aperto, dopo questo tempo si chiede di premere il pulsante.
    private static readonly TimeSpan PressHint = TimeSpan.FromSeconds(60);

    // Si cercano file scritti da qualche secondo prima dell'avvio (vedi SaveFinder.FindChanged).
    private static readonly TimeSpan StartMargin = TimeSpan.FromSeconds(5);

    private readonly GameInfo game;
    private readonly GameAccount account;
    private readonly IGameProvider provider;
    private readonly IDialogService dialogs;
    private readonly Func<DateTime> utcNow;
    private readonly Func<DateTime, CancellationToken, Task<LearnResult>> scan;
    private readonly SaveContext context;
    private readonly string dataFolder;
    private readonly IgnoredFolders ignored;

    private DateTime startedUtc;
    private bool seenRunning;
    private DateTime? stoppedSince;
    private CancellationTokenSource? looking;
    private (LearnCandidate Item, int Index, bool ForAll, string Portable)? lastIgnored;

    /// <param name="utcNow">L'ora (UTC): quella vera, o una finta nei test.</param>
    /// <param name="scan">La ricerca dei file cambiati dopo l'ora data.</param>
    public LearnSavesViewModel(GameInfo game, GameAccount account, IGameProvider provider, IDialogService dialogs,
        Func<DateTime> utcNow, Func<DateTime, CancellationToken, Task<LearnResult>> scan)
    {
        this.game = game;
        this.account = account;
        this.provider = provider;
        this.dialogs = dialogs;
        this.utcNow = utcNow;
        this.scan = scan;

        context = SaveLookup.ContextFor(provider, account, game, Platform.Current.GetKnownFolders());
        dataFolder = SaveFolderChoice.DataFolder(game, account, provider);
        ignored = new IgnoredFolders(AppData.Root);

        // Fino all'avvio (StartAsync) l'ora di partenza è questa: così la ricerca non ha date impossibili.
        startedUtc = utcNow();
    }

    /// <summary>Chiede alla finestra di chiudersi. true se una cartella è stata salvata.</summary>
    public event Action<bool>? Finished;

    public string Title => Loc.F(Strings.LearnTitle, game.Name);

    public string IntroText => Loc.F(Strings.LearnIntroText, game.Name);

    /// <summary>false: il provider non sa avviare il gioco, e il testo chiede di avviarlo a mano.</summary>
    public bool CanLaunch => provider.CanLaunch(game);

    public string StartText => CanLaunch ? Strings.LearnStart : Strings.LearnStartedByHand;

    public string StartByHandText => Loc.F(Strings.LearnStartByHandText, game.Name);

    public string PlayingText => Loc.F(Strings.LearnPlayingText, game.Name);

    public string PressWhenDoneText => Strings.LearnPressWhenDone;

    public string LookingText => Loc.F(Strings.LearnLooking, "").TrimEnd();

    public string PartialText => Strings.LearnPartial;

    public string NothingFoundText => Strings.LearnNothingFound;

    public string IgnoredNoticeText => Strings.IgnoredToast;

    public string UndoText => Strings.IgnoreUndo;

    public bool IsIntro => Step == LearnStep.Intro;

    public bool IsPlaying => Step == LearnStep.Playing;

    public bool IsLooking => Step == LearnStep.Looking;

    public bool IsResult => Step == LearnStep.Result;

    [ObservableProperty]
    private LearnStep step = LearnStep.Intro;

    /// <summary>Messaggio sotto i pulsanti: gioco già aperto, o avvio non riuscito.</summary>
    [ObservableProperty]
    private string message = "";

    /// <summary>true se il testo chiede di premere il pulsante quando il gioco è chiuso.</summary>
    [ObservableProperty]
    private bool showPressWhenDone;

    /// <summary>true se la ricerca si è fermata presto.</summary>
    [ObservableProperty]
    private bool partial;

    /// <summary>true se la ricerca non ha trovato nessuna cartella.</summary>
    [ObservableProperty]
    private bool nothingFound;

    /// <summary>Avviso di una cartella segnata come "non di salvataggi", con Annulla. Vuoto se nessuno.</summary>
    [ObservableProperty]
    private string ignoredNotice = "";

    /// <summary>Le cartelle proposte, la migliore per prima.</summary>
    public ObservableCollection<LearnCandidate> Candidates { get; } = [];

    public bool HasMessage => Message != "";

    public bool HasIgnoredNotice => IgnoredNotice != "";

    partial void OnMessageChanged(string value) => OnPropertyChanged(nameof(HasMessage));

    partial void OnIgnoredNoticeChanged(string value) => OnPropertyChanged(nameof(HasIgnoredNotice));

    partial void OnStepChanged(LearnStep value)
    {
        OnPropertyChanged(nameof(IsIntro));
        OnPropertyChanged(nameof(IsPlaying));
        OnPropertyChanged(nameof(IsLooking));
        OnPropertyChanged(nameof(IsResult));
    }

    /// <summary>
    /// Avvia il gioco (o prende atto che l'utente l'ha avviato). Il gioco non deve
    /// essere già aperto. Da qui l'ora di partenza è quella di questo momento.
    /// </summary>
    [RelayCommand]
    private async Task StartAsync()
    {
        Message = "";

        if (await Task.Run(() => provider.IsGameRunning(game)))
        {
            Message = Loc.F(Strings.LearnCloseFirst, game.Name);
            return;
        }

        startedUtc = utcNow();

        if (CanLaunch)
        {
            Result started = provider.Launch(game);

            if (!started.Success)
            {
                Message = started.Message;
                return;
            }
        }

        seenRunning = false;
        stoppedSince = null;
        ShowPressWhenDone = !provider.CanDetectRunning(game);
        Step = LearnStep.Playing;
    }

    /// <summary>
    /// Il pulsante "Ho chiuso il gioco": si cerca subito.
    /// </summary>
    [RelayCommand]
    private async Task QuitDoneAsync()
    {
        if (Step == LearnStep.Playing)
            await LookAsync();
    }

    /// <summary>
    /// Un giro del controllo (ogni due secondi): quando il gioco è stato aperto e
    /// poi resta chiuso per qualche secondo, si cerca. Dopo un minuto senza averlo
    /// visto aperto, si chiede di premere il pulsante.
    /// </summary>
    public async Task TickAsync()
    {
        if (Step != LearnStep.Playing)
            return;

        bool running = await Task.Run(() => provider.IsGameRunning(game));

        if (Step != LearnStep.Playing)
            return;

        DateTime now = utcNow();

        if (running)
        {
            seenRunning = true;
            stoppedSince = null;
        }
        else if (seenRunning)
        {
            stoppedSince ??= now;

            if (now - stoppedSince.Value >= QuitSettle)
            {
                await LookAsync();
                return;
            }
        }

        ShowPressWhenDone = !provider.CanDetectRunning(game) || (!seenRunning && now - startedUtc >= PressHint);
    }

    /// <summary>
    /// Annulla la ricerca in corso (il risultato sarà parziale), o chiude la procedura.
    /// </summary>
    [RelayCommand]
    private void Cancel()
    {
        if (Step == LearnStep.Looking && looking != null)
            looking.Cancel();
        else
            Finished?.Invoke(false);
    }

    /// <summary>
    /// Torna al primo passo.
    /// </summary>
    [RelayCommand]
    private void TryAgain()
    {
        Step = LearnStep.Intro;
        Message = "";
        ShowPressWhenDone = false;
        Partial = false;
        NothingFound = false;
        IgnoredNotice = "";
        lastIgnored = null;
        Candidates.Clear();
    }

    /// <summary>
    /// Scelta a mano della cartella: come in Altri giochi, con la conferma.
    /// </summary>
    [RelayCommand]
    private async Task ChooseMyselfAsync()
    {
        string? folder = await dialogs.PickFolderAsync(
            Loc.F(Strings.ChooseSaveFolderTitle, game.Name),
            SaveFolderChoice.StartFolder(game, account, provider));

        if (folder != null && await SaveFolderChoice.ConfirmAndSaveAsync(dialogs, folder, game, account, provider))
            Finished?.Invoke(true);
    }

    /// <summary>
    /// Un'altra cartella in cui cercare questo gioco: si ricorda e si torna al primo passo.
    /// </summary>
    [RelayCommand]
    private async Task LookElsewhereAsync()
    {
        string? folder = await dialogs.PickFolderAsync(Strings.LearnLookElsewhere, SaveFolderChoice.StartFolder(game, account, provider));

        if (folder == null)
            return;

        new GameSaveSettings(dataFolder).AddExtraFolder(folder);
        TryAgain();
    }

    /// <summary>Annulla "non è una cartella di salvataggi": la cartella torna nella lista.</summary>
    [RelayCommand]
    private void UndoIgnored()
    {
        if (lastIgnored is not { } last)
            return;

        if (last.ForAll)
            ignored.RemoveGlobal(last.Portable);
        else
            ignored.RemoveForGame(dataFolder, last.Item.Folder);

        last.Item.ChoosingIgnore = false;
        Candidates.Insert(Math.Min(last.Index, Candidates.Count), last.Item);
        lastIgnored = null;
        IgnoredNotice = "";
    }

    /// <summary>
    /// Usa la cartella: si controlla, si chiede conferma e si salva come trovata da LSS.
    /// </summary>
    private async Task UseCandidateAsync(LearnCandidate item)
    {
        if (await SaveFolderChoice.ConfirmAndSaveAsync(dialogs, item.Folder, game, account, provider, SaveSource.Learned))
            Finished?.Invoke(true);
    }

    /// <summary>
    /// Segna la cartella come "non di salvataggi", per questo gioco o per tutti.
    /// La cartella attuale del gioco non si può segnare.
    /// </summary>
    private void Ignore(LearnCandidate item, bool forAll)
    {
        if (IsCurrentFolder(item.Folder))
        {
            Message = Loc.F(Strings.IgnoreCurrentFolder, game.Name);
            return;
        }

        string portable = SavePathResolver.ToPortable(item.Folder, context);

        if (forAll)
            ignored.AddGlobal(portable);
        else
            ignored.AddForGame(dataFolder, item.Folder);

        int index = Candidates.IndexOf(item);
        Candidates.Remove(item);
        lastIgnored = (item, index, forAll, portable);
        IgnoredNotice = Strings.IgnoredToast;
    }

    /// <summary>
    /// true se la cartella è quella attuale dei salvataggi del gioco.
    /// </summary>
    private bool IsCurrentFolder(string folder)
    {
        var settings = new GameSaveSettings(dataFolder);
        settings.Load();

        return string.Equals(folder, game.SaveFolder, StringComparison.OrdinalIgnoreCase)
            || string.Equals(folder, settings.Folder, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Cerca i file cambiati dall'avvio del gioco e mostra le cartelle trovate.
    /// </summary>
    private async Task LookAsync()
    {
        Step = LearnStep.Looking;
        looking = new CancellationTokenSource();

        LearnResult result;

        try
        {
            result = await scan(startedUtc.AddSeconds(-StartMargin.TotalSeconds), looking.Token);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or OperationCanceledException)
        {
            Log.Write("Ricerca dei salvataggi non riuscita: " + game.Name, ex);
            result = new LearnResult([], true);
        }
        finally
        {
            looking.Dispose();
            looking = null;
        }

        ShowResult(result);
    }

    private void ShowResult(LearnResult result)
    {
        Partial = result.Partial;
        Candidates.Clear();

        foreach (SaveCandidate candidate in result.Candidates)
        {
            Candidates.Add(new LearnCandidate(candidate, game.Name, Candidates.Count == 0, Ignore));
        }

        if (Candidates.Count > 0)
            Candidates[0].IsSelected = true;

        NothingFound = Candidates.Count == 0;
        Step = LearnStep.Result;
    }

    /// <summary>
    /// Usa le cartelle scelte: la prima è la cartella principale, come sempre. Le altre
    /// diventano posti extra se il gioco non ha ancora partite; con partite si aggiungono
    /// da "Aggiungi un'altra cartella" (ci vogliono le copie per le partite).
    /// </summary>
    [RelayCommand]
    private async Task UseSelectedAsync()
    {
        List<LearnCandidate> picked = Candidates.Where(candidate => candidate.IsSelected).ToList();

        if (picked.Count == 0)
            return;

        if (!await SaveFolderChoice.ConfirmAndSaveAsync(dialogs, picked[0].Folder, game, account, provider,
                SaveSource.Learned))
            return;

        if (picked.Count > 1 && SaveFolderChoice.HasPlaythroughs(dataFolder))
        {
            Log.Write("Cartelle extra non aggiunte a " + game.Name + ": il gioco ha partite, si aggiungono da Altra cartella.");
        }
        else if (picked.Count > 1)
        {
            var spots = new GameSpots(dataFolder);
            spots.Load();

            foreach (LearnCandidate extra in picked.Skip(1))
            {
                string portable = SaveLookup.PortableFolder(provider, account, game, extra.Folder);

                try
                {
                    spots.Add(extra.Folder, "", portable, SaveSource.Learned);
                }
                catch (ArgumentException ex)
                {
                    Log.Write("Cartella non aggiunta: " + extra.Folder, ex);
                }
            }

            spots.Save();
        }

        Finished?.Invoke(true);
    }
}
