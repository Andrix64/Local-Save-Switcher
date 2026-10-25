using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lss.App.Services;
using Lss.App.Views;

namespace Lss.App.ViewModels;

/// <summary>Un file condiviso nell'elenco: il percorso relativo alla cartella del gioco.</summary>
public sealed class SharedFileItemViewModel
{
    public SharedFileItemViewModel(string path, Action<SharedFileItemViewModel> remove)
    {
        Path = path;
        RemoveCommand = new RelayCommand(() => remove(this));
    }

    public string Path { get; }

    public IRelayCommand RemoveCommand { get; }
}

/// <summary>
/// Impostazioni della partita e dell'app (SettingsForm in WinForms), con i
/// file condivisi tra le partite.
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly SaveManager manager;
    private readonly IDialogService dialogs;
    private readonly Func<Task>? exportAll;

    /// <summary>exportAll esporta la configurazione di tutti i giochi (vedi ExportAllCommand).</summary>
    public SettingsViewModel(SaveManager manager, IDialogService dialogs, Func<Task>? exportAll = null)
    {
        this.manager = manager;
        this.dialogs = dialogs;
        this.exportAll = exportAll;

        AppState state = manager.State;
        launchGame = state.LaunchGame;
        saveOnExit = state.SaveOnGameExit;
        shareOptions = state.SharePreferences;
        keepCopies = Math.Min(999, state.BackupsToKeep);

        foreach (string file in manager.SharedFiles)
            SharedFiles.Add(new SharedFileItemViewModel(file, Remove));
    }

    public string Title => Strings.SettingsTitle;

    public string LaunchText => Loc.F(Strings.SettingLaunch, manager.Game.Name);

    public string LaunchHint => Strings.SettingLaunchHint;

    public string SaveOnExitText => Loc.F(Strings.SettingSaveOnExit, manager.Game.Name);

    public string SaveOnExitHint => Strings.SettingSaveOnExitHint;

    public string ShareText => Strings.SettingSharePreferences;

    public string ShareHint => Strings.SettingSharePreferencesHint;

    public string SharedFilesCaption => Strings.SharedFilesCaption;

    public string AddSharedFileText => Strings.AddSharedFile;

    public string RemoveText => Strings.RemoveSharedFile;

    public string KeepText => Strings.SettingKeepCopies;

    public string KeepHint => Strings.SettingKeepCopiesHint;

    public string DataFolderText => Loc.F(Strings.SettingDataFolder, AppData.Root);

    public string OpenFolderText => Strings.OpenFolder;

    public string AboutText => Loc.F(Strings.SettingAbout,
        manager.AccountName == "" ? "—" : manager.AccountName, Program.Version);

    public string CaptionGame => Strings.CaptionGame;

    public string CaptionCopies => Strings.CaptionCopies;

    public string CaptionData => Strings.CaptionData;

    public string SaveText => Strings.SaveButton;

    public string CancelText => Strings.Cancel;

    [ObservableProperty]
    private bool launchGame;

    [ObservableProperty]
    private bool saveOnExit;

    [ObservableProperty]
    private bool shareOptions;

    [ObservableProperty]
    private decimal keepCopies;

    public ObservableCollection<SharedFileItemViewModel> SharedFiles { get; } = [];

    /// <summary>
    /// Salva le impostazioni. Prima chiede conferma se si eliminano copie.
    /// true se sono state salvate.
    /// </summary>
    public async Task<bool> TrySaveAsync()
    {
        int keep = (int)KeepCopies;
        int excess = keep == 0 ? 0 : Math.Max(0, manager.GetBackups().Count - keep);

        // Eliminare copie è definitivo: si chiede prima.
        if (excess > 0 && !await dialogs.ConfirmAsync(Strings.ConfirmPruneHeading,
                Loc.F(Strings.ConfirmPrune, excess), Strings.DeleteCopies,
                destructive: true, kind: InfoKind.Warning))
            return false;

        Result result = manager.SaveSettings(LaunchGame, SaveOnExit, ShareOptions, keep,
            SharedFiles.Select(f => f.Path).ToList());

        if (!result.Success)
        {
            await dialogs.InformAsync(Strings.TitleOperationFailed, result.Message, InfoKind.Warning);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Aggiunge un file dentro la cartella dei salvataggi, come percorso relativo con "/".
    /// </summary>
    [RelayCommand]
    private async Task AddSharedFileAsync()
    {
        string? picked = await dialogs.PickFileAsync(Strings.AddSharedFile, manager.SavePath);

        if (picked == null)
            return;

        string root = Path.GetFullPath(manager.SavePath) + Path.DirectorySeparatorChar;
        string full = Path.GetFullPath(picked);

        if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            await dialogs.InformAsync("", Strings.SharedFileOutside, InfoKind.Warning);
            return;
        }

        string relative = Path.GetRelativePath(manager.SavePath, full).Replace(Path.DirectorySeparatorChar, '/');

        if (SharedFiles.Any(f => f.Path == relative))
            return;

        SharedFiles.Add(new SharedFileItemViewModel(relative, Remove));
    }

    public string ExportAllText => Strings.ExportAllConfigs;

    /// <summary>Esporta in un file le configurazioni di tutti i giochi con cartelle scelte, imparate o importate.</summary>
    [RelayCommand]
    private async Task ExportAllAsync()
    {
        if (exportAll != null)
            await exportAll();
    }

    public string IgnoredFoldersText => Strings.IgnoredFoldersLink;

    /// <summary>
    /// Le cartelle che non sono salvataggi (per tutti i giochi e per questo): si tolgono da lì.
    /// </summary>
    [RelayCommand]
    private async Task OpenIgnoredFoldersAsync()
    {
        string dataFolder = Path.GetDirectoryName(manager.ProfilesPath)!;

        await dialogs.ShowAsync<bool>(new IgnoredFoldersWindow(new IgnoredFoldersViewModel(dataFolder, manager.Game.Name)));
    }

    [RelayCommand]
    private async Task OpenFolderAsync()
    {
        string folder = Path.GetDirectoryName(manager.ProfilesPath)!;

        if (!await dialogs.OpenFolderAsync(folder))
            await dialogs.InformAsync(Strings.TitleOperationFailed, Loc.F(Strings.CannotOpenFolder, folder), InfoKind.Warning);
    }

    private void Remove(SharedFileItemViewModel item) => SharedFiles.Remove(item);
}
