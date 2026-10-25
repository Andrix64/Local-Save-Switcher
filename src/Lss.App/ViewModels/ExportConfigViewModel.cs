using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lss.App.Services;

namespace Lss.App.ViewModels;

/// <summary>
/// Anteprima della configurazione dei salvataggi di un gioco (N9c): i posti come
/// saranno scritti, quelli che non si esportano, e le note facoltative. Il file si
/// scrive solo con Salva; Annulla non scrive nulla.
/// </summary>
public partial class ExportConfigViewModel : ObservableObject
{
    private readonly SaveManager manager;
    private readonly IDialogService dialogs;

    public ExportConfigViewModel(SaveManager manager, IDialogService dialogs)
    {
        this.manager = manager;
        this.dialogs = dialogs;

        Title = Loc.F(Strings.ConfigPreviewTitle, manager.Game.Name);
        IntroText = Strings.ConfigPreviewText;
        LauncherText = manager.Game.Source == SaveSource.Launcher ? Loc.F(Strings.ConfigFromLauncher, manager.LauncherName) : "";

        manager.ExportConfigJson("", out IReadOnlyList<string> skipped);

        var lines = new List<string>();

        foreach (SaveSpot spot in manager.Game.Spots.Where(spot => !skipped.Contains(spot.Folder)))
            lines.Add(spot.IsWholeFolder ? spot.PortablePath : spot.PortablePath + " (" + spot.Pattern + ")");

        SpotsText = string.Join("\n", lines);
        SkippedText = skipped.Count > 0 ? Loc.F(Strings.ConfigSpotSkipped, string.Join(", ", skipped)) : "";
    }

    /// <summary>Esito della finestra: true se il file è stato scritto.</summary>
    public event Action<bool>? Finished;

    public string Title { get; }

    public string IntroText { get; }

    /// <summary>Solo se il gioco ha le cartelle dal launcher: a cosa serve il file.</summary>
    public string LauncherText { get; }

    public bool HasLauncherText => LauncherText != "";

    /// <summary>I posti da scrivere, uno per riga.</summary>
    public string SpotsText { get; }

    /// <summary>I posti che non si esportano (fuori dalle cartelle standard), o vuoto.</summary>
    public string SkippedText { get; }

    public bool HasSkipped => SkippedText != "";

    public string NotesCaption => Strings.ConfigNotes;

    /// <summary>Note facoltative per chi riceve il file: finiscono nel campo notes.</summary>
    [ObservableProperty]
    private string notes = "";

    public string SaveText => Strings.SaveButton;

    public string CancelText => Strings.Cancel;

    /// <summary>
    /// Chiede dove salvare il file e lo scrive con le note. Se la scrittura fallisce
    /// si avvisa e la finestra resta aperta.
    /// </summary>
    [RelayCommand]
    private async Task SaveAsync()
    {
        string platform = OperatingSystem.IsLinux() ? "Linux" : "Windows";
        string suggested = string.Concat($"{manager.Game.Name} - {platform}.lss-config.json"
            .Where(character => !Path.GetInvalidFileNameChars().Contains(character)));
        string? path = await dialogs.PickConfigToSaveAsync(Title, suggested);

        if (path == null)
            return;

        try
        {
            File.WriteAllText(path, manager.ExportConfigJson(Notes.Trim(), out _));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            await dialogs.InformAsync(Strings.TitleOperationFailed, ex.Message, InfoKind.Warning);
            return;
        }

        Finished?.Invoke(true);
    }

    [RelayCommand]
    private void Cancel() => Finished?.Invoke(false);
}
