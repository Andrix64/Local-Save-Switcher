using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lss.App.Services;

namespace Lss.App.ViewModels;

/// <summary>
/// Le cartelle proposte dopo una partita, tutte in una finestra (N11). Ogni proposta si
/// aggiunge ai posti, si segna come "non sono salvataggi" (per questo gioco o per tutti),
/// o si lascia per dopo. Chiudere la finestra non cambia nulla.
/// </summary>
public partial class SpotSuggestionsViewModel : ObservableObject
{
    private readonly GameViewModel game;

    public SpotSuggestionsViewModel(GameViewModel game, IReadOnlyList<SpotSuggestion> suggestions)
    {
        this.game = game;
        Title = Loc.F(Strings.SuggestionsTitle, game.Manager.Game.Name);

        Items = new ObservableCollection<SpotSuggestionItem>(suggestions.Select(suggestion => new SpotSuggestionItem(
            suggestion, game.Manager.Game.Name, Add, Ignore, Later)));
    }

    /// <summary>Esito della finestra: true se una proposta è stata gestita, false se si è chiusa senza fare nulla.</summary>
    public event Action<bool>? Finished;

    public string Title { get; }

    /// <summary>Le proposte ancora da decidere: quelle gestite (aggiunte, ignorate, lasciate) si tolgono.</summary>
    public ObservableCollection<SpotSuggestionItem> Items { get; }

    public string CloseText => Strings.Close;

    /// <summary>Chiude senza toccare le proposte rimaste.</summary>
    [RelayCommand]
    private void Close() => Finished?.Invoke(false);

    private async Task Add(SpotSuggestionItem item)
    {
        if (await game.AddSuggestionAsync(item.Suggestion))
            Remove(item);
    }

    private void Ignore(SpotSuggestionItem item, bool forAll)
    {
        game.IgnoreSuggestion(item.Suggestion, forAll);
        Remove(item);
    }

    private void Later(SpotSuggestionItem item) => Remove(item);

    private void Remove(SpotSuggestionItem item)
    {
        Items.Remove(item);

        if (Items.Count == 0)
            Finished?.Invoke(true);
    }
}

/// <summary>
/// Una proposta nella finestra: la cartella, i motivi, e i comandi per decidere. "Non sono
/// salvataggi" apre le due scelte come nella procedura "Trova i salvataggi".
/// </summary>
public sealed partial class SpotSuggestionItem : ObservableObject
{
    private readonly Func<SpotSuggestionItem, Task> add;
    private readonly Action<SpotSuggestionItem, bool> ignore;
    private readonly Action<SpotSuggestionItem> later;

    public SpotSuggestionItem(SpotSuggestion suggestion, string gameName, Func<SpotSuggestionItem, Task> add,
        Action<SpotSuggestionItem, bool> ignore, Action<SpotSuggestionItem> later)
    {
        Suggestion = suggestion;
        this.add = add;
        this.ignore = ignore;
        this.later = later;

        FolderText = suggestion.Pattern == ""
            ? suggestion.Folder
            : Loc.F(Strings.SpotFiles, suggestion.Folder, suggestion.Pattern);
        ReasonsText = string.Join("\n", suggestion.Reasons.Select(LearnCandidate.ReasonLine));
        IgnoreForGameText = Loc.F(Strings.IgnoreForGame, gameName);

        AddCommand = new AsyncRelayCommand(() => this.add(this));
        ToggleIgnoreCommand = new RelayCommand(() => ChoosingIgnore = !ChoosingIgnore);
        IgnoreForGameCommand = new RelayCommand(() => this.ignore(this, false));
        IgnoreForAllCommand = new RelayCommand(() => this.ignore(this, true));
        LaterCommand = new RelayCommand(() => this.later(this));
    }

    /// <summary>La proposta così come è salvata in proposte.ini.</summary>
    public SpotSuggestion Suggestion { get; }

    /// <summary>La cartella, o i file (maschera) dentro la cartella.</summary>
    public string FolderText { get; }

    /// <summary>I motivi, uno per riga, con ✓ e ✗ (come nella procedura "Trova i salvataggi").</summary>
    public string ReasonsText { get; }

    public bool HasReasons => ReasonsText != "";

    public string AddText => Strings.SuggestionAdd;

    public string NotSaveText => Strings.SuggestionNotSaves;

    public string IgnoreForGameText { get; }

    public string IgnoreForAllText => Strings.IgnoreForAll;

    /// <summary>
    /// "Per tutti i giochi" solo per una cartella intera: per i file sparsi la cartella
    /// è quella del gioco, e ignorarla per tutti toglierebbe le installazioni di ogni gioco.
    /// </summary>
    public bool CanIgnoreForAll => Suggestion.Pattern == "";

    public string LaterText => Strings.SuggestionLater;

    /// <summary>Se la scelta "Solo per questo gioco / Per tutti i giochi" è aperta.</summary>
    [ObservableProperty]
    private bool choosingIgnore;

    public IAsyncRelayCommand AddCommand { get; }

    public IRelayCommand ToggleIgnoreCommand { get; }

    public IRelayCommand IgnoreForGameCommand { get; }

    public IRelayCommand IgnoreForAllCommand { get; }

    public IRelayCommand LaterCommand { get; }
}
