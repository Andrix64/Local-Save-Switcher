using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Lss.App.ViewModels;

/// <summary>Da dove parte una partita nuova.</summary>
public enum NewContent
{
    /// <summary>Vuota: un gioco nuovo dall'inizio.</summary>
    Empty,

    /// <summary>Copia di una partita che c'è già.</summary>
    CopyOf,

    /// <summary>Importata da un file zip.</summary>
    Import,
}

/// <summary>Una partita esistente da copiare: la cartella e il nome da mostrare.</summary>
public sealed record CopySource(string ProfileName, string DisplayName);

/// <summary>Esito della finestra: nome e scelte. Null se annullata.</summary>
/// <param name="PlayerId">Persona a cui va la partita nuova ("" se non c'è nessuna).</param>
/// <param name="SourceProfile">La partita da copiare (con Content CopyOf).</param>
/// <param name="ImportPath">Il file zip da importare (con Content Import).</param>
public sealed record NewPlaythroughResult(string Name, NewContent Content, bool ActivateNow, string PlayerId = "",
    string SourceProfile = "", string ImportPath = "");

/// <summary>
/// Nuova partita o rinomina (NewProfileForm in WinForms): nome con controllo
/// in tempo reale; la partita nuova parte vuota, è la copia di una esistente o si importa da un file;
/// attiva subito.
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

    /// <summary>Le partite che si possono copiare (vuoto: l'opzione non si vede).</summary>
    public IReadOnlyList<CopySource> Sources { get; init; } = [];

    /// <summary>Sceglie il file zip da importare (null se si annulla); si collega al selettore di file.</summary>
    public Func<Task<string?>>? PickImportFile { get; init; }

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

    // Di partenza la partita è vuota.
    [NotifyPropertyChangedFor(nameof(EmptyProfile), nameof(CopyOfOption), nameof(ImportOption), nameof(ShowName))]
    [ObservableProperty]
    private NewContent content = NewContent.Empty;

    [ObservableProperty]
    private CopySource? selectedSource;

    [NotifyPropertyChangedFor(nameof(ImportFileText))]
    [ObservableProperty]
    private string importPath = "";

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

    // I tre pulsanti di scelta: ognuno vale se Content è il suo.
    public bool EmptyProfile
    {
        get => Content == NewContent.Empty;
        set
        {
            if (value)
                Content = NewContent.Empty;
        }
    }

    public bool CopyOfOption
    {
        get => Content == NewContent.CopyOf;
        set
        {
            if (value)
                Content = NewContent.CopyOf;
        }
    }

    public bool ImportOption
    {
        get => Content == NewContent.Import;
        set
        {
            if (value)
                Content = NewContent.Import;
        }
    }

    /// <summary>La copia si offre solo se c'è una partita da copiare.</summary>
    public bool ShowCopyOption => Sources.Count > 0;

    /// <summary>Importando il nome viene dal file: il campo non serve.</summary>
    public bool ShowName => Content != NewContent.Import;

    public string ImportFileText => ImportPath == "" ? Strings.NewNoFile : Path.GetFileName(ImportPath);

    public NewPlaythroughResult CreateResult() =>
        new(Name.Trim(), Content, ActivateNow, SelectedPlayer?.Id ?? "",
            Content == NewContent.CopyOf ? SelectedSource?.ProfileName ?? "" : "",
            Content == NewContent.Import ? ImportPath : "");

    /// <summary>"Scegli il file…": il file zip di una partita esportata.</summary>
    [RelayCommand]
    private async Task ChooseImportFileAsync()
    {
        string? picked = PickImportFile == null ? null : await PickImportFile();

        if (picked != null)
            ImportPath = picked;
    }

    partial void OnNameChanged(string value) => Validate();

    partial void OnContentChanged(NewContent value)
    {
        // Passando alla copia si parte dalla prima partita, così il pulsante Crea non resta spento per niente.
        if (value == NewContent.CopyOf)
            SelectedSource ??= Sources.FirstOrDefault();

        Validate();
    }

    partial void OnSelectedSourceChanged(CopySource? value) => Validate();

    partial void OnImportPathChanged(string value) => Validate();

    private void Validate()
    {
        string trimmed = Name.Trim();
        string error = "";

        if (Content != NewContent.Import)
        {
            if (trimmed != "" && !SaveManager.IsValidName(trimmed))
                error = Loc.F(Strings.InvalidNameHint, Hint);
            else if (trimmed != "" && profileExists(trimmed) && !IsCurrentName(trimmed))
                error = Strings.ProfileExists;
        }

        HintText = error == "" ? Hint : error;
        HintIsError = error != "";

        CanCreate = Content switch
        {
            // Importando basta il file: il nome lo dà lo zip.
            NewContent.Import => ImportPath != "",
            NewContent.CopyOf => trimmed != "" && error == "" && SelectedSource != null,
            // Con la rinomina, il nome invariato non serve.
            _ => trimmed != "" && error == "" && trimmed != currentName,
        };
    }

    // Nel rinomina il nome attuale è valido anche con maiuscole diverse.
    private bool IsCurrentName(string name) =>
        currentName != null && string.Equals(name, currentName, StringComparison.OrdinalIgnoreCase);
}
