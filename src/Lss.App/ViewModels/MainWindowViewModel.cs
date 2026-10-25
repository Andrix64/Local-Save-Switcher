using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Lss.App.Services;

namespace Lss.App.ViewModels;

/// <summary>
/// Finestra principale: la colonna dei giochi e il gioco selezionato, con la
/// sua GameViewModel (creata alla prima selezione).
/// </summary>
public partial class MainWindowViewModel : ObservableObject
{
    private readonly GameAccount account;
    private readonly AppSettings settings;
    private readonly IDialogService dialogs;

    // Una GameViewModel per gioco, per AppID.
    private readonly Dictionary<string, GameViewModel> gameModels = [];

    // Giochi già aperti in lista: al primo ingresso si fa il primo avvio e il controllo delle cartelle.
    private readonly HashSet<string> checkedGames = [];

    // Dopo l'apertura della finestra le selezioni fanno subito il primo avvio (vedi OnOpenedAsync).
    private bool opened;

    internal MainWindowViewModel(GameAccount account, IEnumerable<GameInfo> games, AppSettings settings, IDialogService dialogs)
    {
        this.account = account;
        this.settings = settings;
        this.dialogs = dialogs;

        foreach (GameInfo game in games)
        {
            var item = new GameItemViewModel(game);

            if (game.Support == SupportState.Supported)
                Games.Add(item);
            else
                OtherGames.Add(item);
        }

        ShowSidebar = Games.Count > 1 || OtherGames.Count > 0;
        AccountText = account.DisplayName;

        // I giochi già usati restano controllati anche quando non sono selezionati.
        foreach (GameItemViewModel item in Games)
        {
            if (File.Exists(Path.Combine(AppData.Root, account.Id, item.Info.AppId, "stato.ini")))
                GameFor(item.Info);
        }

        Monitor = new GameMonitor(() => gameModels.Values);
        SelectedGame = Games.FirstOrDefault(g => g.Info.AppId == settings.LastGame) ?? Games.FirstOrDefault();
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

    public string AccountText { get; }

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

        GameViewModel game = GameFor(value.Info);
        CurrentGame = game;
        settings.LastGame = value.Info.AppId;
        settings.Save();

        // Alla prima selezione di un gioco (quelle prima dell'apertura le fa OnOpenedAsync).
        if (checkedGames.Add(value.Info.AppId) && opened)
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

    private GameViewModel GameFor(GameInfo game)
    {
        if (gameModels.TryGetValue(game.AppId, out GameViewModel? existing))
            return existing;

        Result<SaveManager> open = SaveManager.Open(AppData.Root, account, game);

        if (!open.Success)
            throw new InvalidOperationException(open.Message);

        var created = new GameViewModel(open.Value!, dialogs);
        created.Refresh();

        gameModels[game.AppId] = created;
        return created;
    }
}
