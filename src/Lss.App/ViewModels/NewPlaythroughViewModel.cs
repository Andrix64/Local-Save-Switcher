using CommunityToolkit.Mvvm.ComponentModel;

namespace Lss.App.ViewModels;

/// <summary>Esito della finestra: nome e scelte. Null se annullata.</summary>
/// <param name="PlayerId">Persona a cui va la partita nuova ("" se non c'è nessuna).</param>
public sealed record NewPlaythroughResult(string Name, bool CopyCurrentSaves, bool ActivateNow, string PlayerId = "");

/// <summary>
/// Nuova partita o rinomina (NewProfileForm in WinForms): nome con controllo
/// in tempo reale, copia o partita vuota, attiva subito.
/// </summary>
public partial class NewPlaythroughViewModel : ObservableObject
{
    private static readonly string Hint = Strings.ProfileNameHint;

    private readonly Func<string, bool> profileExists;

    // Nome attuale del profilo da rinominare, null per una partita nuova.
    private readonly string? currentName;

    /// <param name="profileExists">Dice se esiste già una partita con quel nome.</param>
    /// <param name="currentName">Nome da mostrare per la rinomina; null per una partita nuova.</param>
    /// <param name="players">Persone tra cui scegliere il giocatore della partita nuova.</param>
    /// <param name="defaultPlayerId">Giocatore preselezionato (il proprietario della partita in gioco).</param>
    public NewPlaythroughViewModel(Func<string, bool> profileExists, string? currentName = null,
        IReadOnlyList<Person>? players = null, string? defaultPlayerId = null)
    {
        this.profileExists = profileExists;
        this.currentName = currentName;

        name = currentName ?? "";
        Players = players ?? [];
        selectedPlayer = Players.FirstOrDefault(person => person.Id == defaultPlayerId) ?? Players.FirstOrDefault();
        Validate();
    }

    /// <summary>Le persone tra cui scegliere (nella partita nuova con più di una persona).</summary>
    public IReadOnlyList<Person> Players { get; }

    /// <summary>La persona a cui va la partita nuova.</summary>
    [ObservableProperty]
    private Person? selectedPlayer;

    /// <summary>Il campo Giocatore si vede solo per una partita nuova con più persone.</summary>
    public bool ShowPlayer => !IsRename && Players.Count > 1;

    public string PlayerCaption => Strings.PlaythroughPlayer;

    [ObservableProperty]
    private string name = "";

    [NotifyPropertyChangedFor(nameof(EmptyProfile))]
    [ObservableProperty]
    private bool copyCurrentSaves = true;

    [ObservableProperty]
    private bool activateNow = true;

    [ObservableProperty]
    private string hintText = Hint;

    [ObservableProperty]
    private bool hintIsError;

    [ObservableProperty]
    private bool canCreate;

    public bool IsRename => currentName != null;

    public string Title => IsRename ? Strings.RenameProfileTitle : Strings.NewProfileTitle;

    /// <summary>Il nome del gioco, per il titolo dentro la finestra ("" se non si sa).</summary>
    public string GameName { get; init; } = "";

    /// <summary>"Nuova partita di Control": il titolo in cima alla finestra.</summary>
    public string Heading => IsRename || GameName == "" ? Title : Loc.F(Strings.NewProfileTitleFor, GameName);

    public string CreateText => IsRename ? Strings.Rename : Strings.Create;

    // Il pulsante "partita vuota" della coppia di scelte: l'opposto di CopyCurrentSaves.
    public bool EmptyProfile
    {
        get => !CopyCurrentSaves;
        set => CopyCurrentSaves = !value;
    }

    public NewPlaythroughResult CreateResult() =>
        new(Name.Trim(), CopyCurrentSaves, ActivateNow, SelectedPlayer?.Id ?? "");

    partial void OnNameChanged(string value) => Validate();

    private void Validate()
    {
        string trimmed = Name.Trim();
        string error = "";

        if (trimmed != "" && !SaveManager.IsValidName(trimmed))
            error = Loc.F(Strings.InvalidNameHint, Hint);
        else if (trimmed != "" && profileExists(trimmed) && !IsCurrentName(trimmed))
            error = Strings.ProfileExists;

        HintText = error == "" ? Hint : error;
        HintIsError = error != "";

        // Con la rinomina, il nome invariato non serve.
        CanCreate = trimmed != "" && error == "" && trimmed != currentName;
    }

    // Nel rinomina il nome attuale è valido anche con maiuscole diverse.
    private bool IsCurrentName(string name) =>
        currentName != null && string.Equals(name, currentName, StringComparison.OrdinalIgnoreCase);
}
