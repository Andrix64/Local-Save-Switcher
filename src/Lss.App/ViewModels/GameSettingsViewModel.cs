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
/// Impostazioni di un gioco (R8): avviare il gioco, salvare alla chiusura, opzioni condivise tra le
/// partite, quante copie tenere e le cartelle ignorate per questo gioco. Quelle di LSS stanno in
/// LssSettingsViewModel.
/// </summary>
public partial class GameSettingsViewModel : ObservableObject
{
    private readonly SaveManager manager;
    private readonly IDialogService dialogs;
    private bool ready;
    private bool reverting;

    internal GameSettingsViewModel(SaveManager manager, IDialogService dialogs)
    {
        this.manager = manager;
        this.dialogs = dialogs;

        AppState state = manager.State;
        launchGame = state.LaunchGame;
        saveOnExit = state.SaveOnGameExit;
        shareOptions = state.SharePreferences;
        keepCopies = Math.Min(999, state.BackupsToKeep);

        foreach (string file in manager.SharedFiles)
            SharedFiles.Add(new SharedFileItemViewModel(file, Remove));

        ready = true;
    }

    /// <summary>"Impostazioni di {gioco}".</summary>
    public string Title => Loc.F(Strings.GameSettingsTitle, manager.Game.Name);

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

    public string CaptionGame => Strings.CaptionGame;

    public string CaptionCopies => Strings.CaptionCopies;

    public string CaptionFolders => Strings.CaptionFolders;

    [ObservableProperty]
    private bool launchGame;

    [ObservableProperty]
    private bool saveOnExit;

    [ObservableProperty]
    private bool shareOptions;

    [ObservableProperty]
    private decimal keepCopies;

    public ObservableCollection<SharedFileItemViewModel> SharedFiles { get; } = [];

    /// <summary>true se almeno una modifica è stata salvata: chi apre la finestra aggiorna le schede.</summary>
    public bool Saved { get; private set; }

    // Ogni modifica vale subito (nessun Salva): si scrive lo stato del gioco a ogni cambiamento.
    partial void OnLaunchGameChanged(bool value) => ApplyNow();

    partial void OnSaveOnExitChanged(bool value) => ApplyNow();

    partial void OnShareOptionsChanged(bool value) => ApplyNow();

    // Meno copie da tenere elimina quelle in più: se non si conferma, il numero torna com'era.
    partial void OnKeepCopiesChanged(decimal oldValue, decimal newValue)
    {
        if (!ready || reverting)
            return;

        _ = ApplyKeepAsync(oldValue);
    }

    private async Task ApplyKeepAsync(decimal previous)
    {
        if (await TrySaveAsync())
            return;

        reverting = true;
        KeepCopies = previous;
        reverting = false;
    }

    private void ApplyNow()
    {
        if (ready && !reverting)
            _ = TrySaveAsync();
    }

    /// <summary>
    /// Salva le impostazioni (a ogni modifica). Prima chiede conferma se si eliminano copie.
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

        Saved = true;
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
        ApplyNow();
    }

    public string IgnoredFoldersText => Strings.IgnoredFoldersLink;

    /// <summary>
    /// Le cartelle che non sono salvataggi di questo gioco: si tolgono da lì (quelle di tutti i giochi
    /// stanno nelle impostazioni di LSS).
    /// </summary>
    [RelayCommand]
    private async Task OpenIgnoredFoldersAsync()
    {
        string dataFolder = Path.GetDirectoryName(manager.ProfilesPath)!;

        await dialogs.ShowAsync<bool>(new IgnoredFoldersWindow(
            new IgnoredFoldersViewModel(dataFolder, manager.Game.Name, IgnoredScope.Game)));
    }

    private void Remove(SharedFileItemViewModel item)
    {
        SharedFiles.Remove(item);
        ApplyNow();
    }
}
