using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lss.App.Services;
using Lss.App.Views;

namespace Lss.App.ViewModels;

/// <summary>
/// Impostazioni di LSS (R8): cercare altre cartelle dopo aver giocato, i giocatori, le
/// cartelle ignorate per tutti i giochi, la cartella dei dati e le
/// informazioni. Le impostazioni di un gioco stanno in GameSettingsViewModel.
/// </summary>
public partial class LssSettingsViewModel : ObservableObject
{
    private readonly IDialogService dialogs;
    private readonly AppSettings settings;
    private readonly Func<Task>? openPlayers;
    private readonly string accountText;

    /// <param name="openPlayers">Apre la finestra Giocatori.</param>
    /// <param name="accountText">Gli account dei launcher, per le informazioni.</param>
    /// <param name="mode">L'interruttore Semplice | Avanzata della finestra principale.</param>
    internal LssSettingsViewModel(AppSettings settings, IDialogService dialogs,
        Func<Task>? openPlayers = null, string accountText = "", ModeSwitchViewModel? mode = null)
    {
        Mode = mode;
        this.settings = settings;
        this.dialogs = dialogs;
        this.openPlayers = openPlayers;
        this.accountText = accountText;

        lookAfterPlaying = settings.LookAfterPlaying;
    }

    public string Title => Strings.SettingsTitle;

    /// <summary>L'interruttore Semplice | Avanzata; null fuori dall'app (test, schermate).</summary>
    public ModeSwitchViewModel? Mode { get; }

    public bool HasMode => Mode != null;

    public string ModeText => Strings.SettingMode;

    public string ModeHint => Strings.SettingModeHint;

    public string DataFolderText => Loc.F(Strings.SettingDataFolder, AppData.Root);

    public string OpenFolderText => Strings.OpenFolder;

    public string AboutText => Loc.F(Strings.SettingAbout, accountText == "" ? "—" : accountText, Program.Version);

    public string CaptionData => Strings.CaptionData;

    public string CaptionPlayers => Strings.CaptionPlayers;

    public string CaptionFolders => Strings.CaptionFolders;

    [ObservableProperty]
    private bool lookAfterPlaying;

    public string LookAfterPlayingText => Strings.LookAfterPlaying;

    public string LookAfterPlayingHint => Strings.LookAfterPlayingHint;

    /// <summary>Le impostazioni dell'app si salvano subito: valgono subito per tutti i giochi.</summary>
    partial void OnLookAfterPlayingChanged(bool value)
    {
        settings.LookAfterPlaying = value;
        settings.Save();
    }

    public string PlayersText => Strings.PlayersButton;

    /// <summary>"Giocatori…": la finestra delle persone di LSS.</summary>
    [RelayCommand]
    private async Task OpenPlayersAsync()
    {
        if (openPlayers != null)
            await openPlayers();
    }

    public string IgnoredFoldersText => Strings.IgnoredFoldersAllLink;

    /// <summary>Le cartelle che non sono salvataggi per tutti i giochi: si tolgono da lì.</summary>
    [RelayCommand]
    private async Task OpenIgnoredFoldersAsync()
    {
        await dialogs.ShowAsync<bool>(new IgnoredFoldersWindow(new IgnoredFoldersViewModel("", "", IgnoredScope.All)));
    }

    [RelayCommand]
    private async Task OpenFolderAsync()
    {
        if (!await dialogs.OpenFolderAsync(AppData.Root))
            await dialogs.InformAsync(Strings.TitleOperationFailed, Loc.F(Strings.CannotOpenFolder, AppData.Root), InfoKind.Warning);
    }
}
