using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lss.App.Services;

namespace Lss.App.ViewModels;

/// <summary>
/// Un gioco nella modalità semplice: copertina (o piastrella generata), stato e
/// ultima partita del giocatore attuale.
/// </summary>
public sealed record SimpleGameTile(
    GameItemViewModel Item,
    string Name,
    string Launcher,
    string? CoverPath,
    string Initials,
    IBrush TileBrush,
    bool Ready,
    bool Running,
    string LastPlayText,
    string? CloudText,
    IImage? Cover)
{
    /// <summary>true se c'è la copertina; altrimenti la piastrella generata.</summary>
    public bool HasCover => Cover != null;

    /// <summary>Un gioco non pronto, o aperto da qualcuno, si vede sbiadito (come nel design).</summary>
    public double Opacity => Running ? 0.55 : !Ready ? 0.6 : HasCloud ? 0.75 : 1;

    public bool HasCloud => CloudText != null;

    /// <summary>Il nome del launcher sulla copertina, in maiuscolo.</summary>
    public string LauncherChip => Launcher.ToUpperInvariant();

    // Un'etichetta sola in alto a destra: in gioco, da preparare o cloud (in quest'ordine).
    public bool ShowRunningBadge => Running;
    public bool ShowSetupBadge => !Running && !Ready;
    public bool ShowCloudBadge => !Running && Ready && HasCloud;

    /// <summary>Il testo dell'etichetta in alto a destra, in maiuscolo; "" se non ce n'è.</summary>
    public string BadgeText => (Running ? Strings.SimplePlaying
        : !Ready ? Strings.SimpleNeedsSetup
        : HasCloud ? Strings.SimpleCloudLabel
        : "").ToUpper(CultureInfo.CurrentUICulture);

    /// <summary>La riga sotto il nome: una sola, con lo stesso ordine delle etichette.</summary>
    public string SubText => Running ? Strings.SimplePlaying
        : !Ready ? Strings.SimpleNeedsSetupOnce
        : CloudText ?? LastPlayText;
}

/// <summary>L'ultima piastrella della griglia: "Aggiungi un gioco".</summary>
public sealed class SimpleAddGameTile
{
    public static SimpleAddGameTile Instance { get; } = new();

    private SimpleAddGameTile()
    {
    }
}

/// <summary>
/// Modalità semplice: "Chi gioca?" (le persone), poi i giochi del giocatore. Un
/// gioco pronto si gioca con la sua partita preferita (P7). Un gioco non pronto,
/// o aggiungere un gioco o una persona, chiede un amministratore.
/// </summary>
public sealed partial class SimpleViewModel : ObservableObject
{
    private readonly People people;
    private readonly IDialogService dialogs;
    private readonly IReadOnlyList<GameItemViewModel> games;
    private readonly Func<GameItemViewModel, SaveManager?> managerOf;
    private readonly Func<GameItemViewModel, Task> setUp;
    private readonly Func<GameItemViewModel, Task> play;
    private readonly Func<Task> addGame;

    /// <param name="setUp">Procedura di ricerca dei salvataggi per un gioco non pronto.</param>
    /// <param name="play">Gioca un gioco pronto (P7).</param>
    /// <param name="addGame">Aggiunge un gioco a mano.</param>
    internal SimpleViewModel(People people, IDialogService dialogs, IReadOnlyList<GameItemViewModel> games,
        Func<GameItemViewModel, SaveManager?> managerOf, Func<GameItemViewModel, Task> setUp,
        Func<GameItemViewModel, Task> play, Func<Task> addGame)
    {
        this.people = people;
        this.dialogs = dialogs;
        this.games = games;
        this.managerOf = managerOf;
        this.setUp = setUp;
        this.play = play;
        this.addGame = addGame;

        Refresh();
    }

    /// <summary>Le persone, nell'ordine di People: le piastrelle della prima pagina.</summary>
    public ObservableCollection<PersonTile> Players { get; } = [];

    /// <summary>I giochi del giocatore attuale, dal più recente.</summary>
    public ObservableCollection<SimpleGameTile> Games { get; } = [];

    /// <summary>La griglia della pagina dei giochi: i giochi e, in fondo, "Aggiungi un gioco".</summary>
    public ObservableCollection<object> Tiles { get; } = [];

    /// <summary>Il giocatore attuale, o null sulla prima pagina.</summary>
    public Person? Current { get; private set; }

    /// <summary>
    /// true se il giocatore attuale ha messo il suo PIN scegliendosi: le azioni che chiedono il suo
    /// PIN (di admin) non lo richiedono più, finché resta scelto. Solo in memoria.
    /// </summary>
    public bool Unlocked { get; private set; }

    /// <summary>La piastrella del giocatore attuale (il suo pallino nella pagina dei giochi), o null.</summary>
    public PersonTile? CurrentTile { get; private set; }

    /// <summary>true sulla pagina dei giochi.</summary>
    public bool OnGamesPage => Current != null;

    public string CurrentName => Current?.Name ?? "";

    /// <summary>L'icona delle impostazioni compare solo dentro il profilo di un amministratore.</summary>
    public bool ShowSettings => Current is { IsAdmin: true };

    /// <summary>Ricostruisce le persone e, se c'è, i giochi del giocatore attuale.</summary>
    public void Refresh()
    {
        Current = Current == null ? null : people.Find(Current.Id);

        if (Current == null)
            Unlocked = false;

        Players.Clear();

        foreach (Person person in people.All)
        {
            Players.Add(new PersonTile(person, Initial(person.Name),
                new SolidColorBrush(Color.Parse(person.Color))));
        }

        Games.Clear();

        if (Current != null)
        {
            foreach (SimpleGameTile tile in BuildGames(Current))
                Games.Add(tile);
        }

        Tiles.Clear();

        foreach (SimpleGameTile tile in Games)
            Tiles.Add(tile);

        Tiles.Add(SimpleAddGameTile.Instance);

        CurrentTile = Current == null ? null : Players.FirstOrDefault(tile => tile.Person.Id == Current.Id);

        OnPropertyChanged(nameof(OnGamesPage));
        OnPropertyChanged(nameof(CurrentName));
        OnPropertyChanged(nameof(ShowSettings));
        OnPropertyChanged(nameof(CurrentTile));
    }

    /// <summary>L'interruttore Semplice | Avanzata dell'intestazione (lo possiede il MainWindowViewModel).</summary>
    public ModeSwitchViewModel? Mode { get; set; }

    /// <summary>Il pulsante dello schermo intero dell'intestazione (lo possiede il MainWindowViewModel).</summary>
    public FullScreenViewModel? FullScreen { get; set; }

    /// <summary>"Impostazioni" dell'intestazione: apre le impostazioni di LSS (dopo un admin).</summary>
    public Func<Task>? LssSettings { get; set; }

    /// <summary>"Impostazioni del gioco…" dal menu di una copertina (dopo un admin).</summary>
    public Func<GameItemViewModel, Task>? GameSettings { get; set; }

    [RelayCommand]
    private Task SelectPlayerTile(PersonTile tile) => SelectPlayerAsync(tile.Person);

    [RelayCommand]
    private Task ActivateGameTile(SimpleGameTile tile) => ActivateAsync(tile);

    /// <summary>
    /// Chi gioca, come lo sceglie l'utente: se ha un PIN lo si chiede prima. PIN sbagliato o
    /// annullato: si resta su "Chi gioca?".
    /// </summary>
    public async Task SelectPlayerAsync(Person person)
    {
        Person? found = people.Find(person.Id);

        if (found == null || !await dialogs.CheckPinAsync(people, found))
            return;

        SelectPlayer(found);
        Unlocked = true;
    }

    /// <summary>Chi gioca: si va alla pagina dei giochi di quella persona (il PIN è già stato controllato).</summary>
    public void SelectPlayer(Person person)
    {
        if (Current?.Id != person.Id)
            Unlocked = false;

        Current = people.Find(person.Id);
        Refresh();
    }

    /// <summary>"Cambia giocatore": si torna alla prima pagina.</summary>
    public void ChangePlayer()
    {
        Current = null;
        Unlocked = false;
        Refresh();
    }

    /// <summary>La freccia "←" della pagina dei giochi.</summary>
    [RelayCommand]
    private void GoBack() => ChangePlayer();

    /// <summary>La partita in corso dalla modalità semplice (il pannello "Buon divertimento"), o null.</summary>
    [ObservableProperty]
    private PlaySession? playing;

    /// <summary>Messaggio dell'ultima partita salvata (il posto del toast).</summary>
    [ObservableProperty]
    private string message = "";

    public bool IsPlaying => Playing != null;

    public string PlayingText => Playing == null ? "" : Loc.F(Strings.SimpleHaveFun, Playing.Player.Name, Playing.Item.Name);

    public string StartYourselfText => Playing is { Launched: false } session
        ? Loc.F(Strings.SimpleStartYourself, session.Item.Name)
        : "";

    /// <summary>Il pulsante "Ho finito di giocare" serve solo se il launcher non sa dire quando il gioco si chiude.</summary>
    public bool ShowFinishButton => Playing is { CanDetect: false };

    public string FinishText => Strings.SimpleFinished;

    /// <summary>Il pannello "In gioco" come nel design: titolo, testo e la copertina del gioco.</summary>
    public string PlayingTitle => Playing == null ? "" : Loc.F(Strings.SimpleHaveFunTitle, Playing.Player.Name);

    public string PlayingBody => Playing == null ? "" : Loc.F(Strings.SimpleHaveFunBody, Playing.Item.Name);

    public string PlayingOpenText => Playing == null ? "" : Loc.F(Strings.SimpleGameOpen, Playing.Item.Name);

    public bool ShowStartYourself => Playing is { Launched: false };

    public SimpleGameTile? PlayingTile => Playing == null ? null : Games.FirstOrDefault(tile => tile.Item == Playing.Item);

    private TaskCompletionSource? finishing;

    partial void OnPlayingChanged(PlaySession? value)
    {
        OnPropertyChanged(nameof(IsPlaying));
        OnPropertyChanged(nameof(PlayingText));
        OnPropertyChanged(nameof(StartYourselfText));
        OnPropertyChanged(nameof(ShowFinishButton));
        OnPropertyChanged(nameof(PlayingTitle));
        OnPropertyChanged(nameof(PlayingBody));
        OnPropertyChanged(nameof(PlayingOpenText));
        OnPropertyChanged(nameof(ShowStartYourself));
        OnPropertyChanged(nameof(PlayingTile));
    }

    /// <summary>Mostra il pannello della partita. Il Task finisce quando l'utente preme "Ho finito".</summary>
    public Task BeginPlaying(PlaySession session)
    {
        finishing = new TaskCompletionSource();
        PlayCancelled = false;
        Playing = session;
        return finishing.Task;
    }

    /// <summary>Chiude il pannello e torna alle copertine.</summary>
    public void EndPlaying()
    {
        Playing = null;
        finishing = null;
        Refresh();
    }

    [RelayCommand]
    private void FinishPlaying() => finishing?.TrySetResult();

    /// <summary>true se l'utente ha detto che il gioco non è partito: niente da salvare.</summary>
    public bool PlayCancelled { get; private set; }

    public string NotStartedText => Strings.SimpleNotStarted;

    /// <summary>"Il gioco non è partito": LSS smette di aspettare, senza salvare né contare il tempo.</summary>
    [RelayCommand]
    private void GameNotStarted()
    {
        PlayCancelled = true;
        finishing?.TrySetResult();
    }

    /// <summary>"Le mie partite" su una copertina: si apre il menu (con il MainWindowViewModel).</summary>
    public Func<GameItemViewModel, Task>? MyGames { get; set; }

    public Task OpenMyGamesAsync(SimpleGameTile tile) => MyGames?.Invoke(tile.Item) ?? Task.CompletedTask;

    /// <summary>Un gioco: si gioca se è pronto, altrimenti si sistema (con un admin).</summary>
    public async Task ActivateAsync(SimpleGameTile tile)
    {
        if (tile.Running)
            return;

        if (!tile.Ready)
        {
            if (!await EnsureAdminAsync())
                return;

            await setUp(tile.Item);
        }
        else
        {
            await play(tile.Item);
        }

        Refresh();
    }

    /// <summary>"Aggiungi una persona": un admin, poi il nome. La nuova persona è un utente.</summary>
    [RelayCommand]
    private async Task AddPlayerAsync()
    {
        Person? admin = await dialogs.ChooseAdminAsync(people, Current, Unlocked);

        if (admin == null)
            return;

        string? name = await dialogs.PromptAsync(Strings.AddPlayer, Strings.PlayerNameCaption, "",
            Strings.AddPlayer);

        if (name != null)
            people.Add(name);

        Refresh();
    }

    /// <summary>"Impostazioni": un admin, poi le impostazioni di LSS.</summary>
    [RelayCommand]
    private async Task OpenSettingsAsync()
    {
        if (LssSettings == null || !await EnsureAdminAsync())
            return;

        await LssSettings();
        Refresh();
    }

    /// <summary>"Impostazioni del gioco…": un admin, poi le impostazioni di quel gioco.</summary>
    public async Task OpenGameSettingsAsync(SimpleGameTile tile)
    {
        if (GameSettings == null || !await EnsureAdminAsync())
            return;

        await GameSettings(tile.Item);
        Refresh();
    }

    /// <summary>"Aggiungi un gioco": un admin, poi la procedura di aggiunta.</summary>
    [RelayCommand]
    private async Task AddGameAsync()
    {
        if (!await EnsureAdminAsync())
            return;

        await addGame();
        Refresh();
    }

    /// <summary>
    /// Chi autorizza: il giocatore attuale se è admin (solo il suo PIN, se ce l'ha),
    /// altrimenti un admin. True se c'è un'autorizzazione.
    /// </summary>
    public async Task<bool> EnsureAdminAsync() =>
        await dialogs.ChooseAdminAsync(people, Current, Unlocked) != null;

    private List<SimpleGameTile> BuildGames(Person player)
    {
        var built = new List<(SimpleGameTile Tile, DateTime? Last)>();

        foreach (GameItemViewModel item in games)
        {
            // I giochi con i salvataggi non riconosciuti (Unsafe) non si mostrano.
            if (item.Info.Support == SupportState.Unsafe)
                continue;

            SaveManager? manager = managerOf(item);
            DateTime? last = manager?.LastPlayedOf(player.Id);
            bool running = manager?.IsGameRunning() ?? false;

            string? cloud = manager != null && manager.GetCloudState() == CloudState.Enabled &&
                            !manager.State.CloudAllowedInSimple
                ? Strings.SimpleCloudLabel
                : null;

            var tile = new SimpleGameTile(
                item,
                item.Name,
                item.Provider.DisplayName,
                item.Info.CoverPath == "" ? null : item.Info.CoverPath,
                Initials(item.Name),
                new SolidColorBrush(Color.Parse(People.Palette[Stable(item.Name) % People.Palette.Count])),
                item.Info.Support == SupportState.Supported,
                running,
                last is DateTime when ? Loc.F(Strings.SimpleLastPlay, when.ToString("d")) : "",
                cloud,
                LoadCover(item.Info.CoverPath));

            built.Add((tile, last));
        }

        return built
            .OrderByDescending(entry => entry.Last ?? DateTime.MinValue)
            .ThenBy(entry => entry.Tile.Name, StringComparer.OrdinalIgnoreCase)
            .Select(entry => entry.Tile)
            .ToList();
    }

    // La copertina del gioco, se il file c'è; altrimenti null e si mostra la piastrella generata.
    private static IImage? LoadCover(string path)
    {
        if (path == "" || !File.Exists(path))
            return null;

        try
        {
            return new Bitmap(path);
        }
        catch (Exception ex)
        {
            Log.Write("Copertina non letta: " + path, ex);
            return null;
        }
    }

    private static string Initial(string name) =>
        name.Length > 0 ? char.ToUpperInvariant(name[0]).ToString() : "?";

    // Due lettere per la piastrella generata: le iniziali delle prime due parole.
    private static string Initials(string name)
    {
        string[] words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Concat(words.Take(2).Select(word => char.ToUpperInvariant(word[0])));
    }

    // Lo stesso nome dà sempre lo stesso colore, anche tra un avvio e l'altro.
    private static int Stable(string text) => text.Sum(character => (int)character);
}
