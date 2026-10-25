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
    private readonly AppSettings settings;
    private readonly IDialogService dialogs;

    // Una GameViewModel per gioco, per provider/id.
    private readonly Dictionary<string, GameViewModel> gameModels = [];

    // Giochi già aperti in lista: al primo ingresso si fa il primo avvio e il controllo delle cartelle.
    private readonly HashSet<string> checkedGames = [];

    // Dopo l'apertura della finestra le selezioni fanno subito il primo avvio (vedi OnOpenedAsync).
    private bool opened;

    internal MainWindowViewModel(IReadOnlyList<LauncherGames> launchers, AppSettings settings, IDialogService dialogs)
    {
        this.settings = settings;
        this.dialogs = dialogs;

        // Con più launcher ogni gruppo di giochi ha la sua intestazione.
        bool headers = launchers.Count > 1;

        foreach (LauncherGames launcher in launchers)
        {
            bool firstSupported = true;
            bool firstOther = true;

            foreach (GameInfo game in launcher.Games)
            {
                var item = new GameItemViewModel(game, launcher.Account, launcher.Provider);

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

    private GameViewModel GameFor(GameItemViewModel item)
    {
        if (gameModels.TryGetValue(item.Info.Key, out GameViewModel? existing))
            return existing;

        Result<SaveManager> open = SaveManager.Open(AppData.Root, item.Account, item.Info, item.Provider);

        if (!open.Success)
            throw new InvalidOperationException(open.Message);

        var created = new GameViewModel(open.Value!, dialogs);
        created.Refresh();

        gameModels[item.Info.Key] = created;
        return created;
    }
}
