using System.Globalization;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Lss.App.ViewModels;

/// <summary>
/// Una cartella proposta nella procedura "Trova i salvataggi": il motivo di ogni
/// punto, i file cambiati, la scelta con il pulsante radio, e i comandi per dire
/// che non è una cartella di salvataggi.
/// </summary>
public sealed partial class LearnCandidate : ObservableObject
{
    // Sotto questo punteggio la cartella è marcata "Poco probabile".
    private const int UnlikelyBelow = 6;

    // Quanti nomi di file mostrare per cartella.
    private const int ShownFiles = 5;

    public LearnCandidate(SaveCandidate candidate, string gameName, bool recommended,
        Action<LearnCandidate, bool> ignore)
    {
        Candidate = candidate;
        Folder = candidate.Folder;
        Recommended = recommended;
        Unlikely = candidate.Score < UnlikelyBelow;
        Reasons = candidate.Reasons.Select(ReasonLine).ToList();
        ReasonsText = string.Join("\n", Reasons);
        FilesText = Loc.F(Strings.LearnFilesChanged, candidate.Files.Count.ToString(CultureInfo.CurrentCulture));

        FileNames = candidate.Files
            .Take(ShownFiles)
            .Select(file => Path.GetRelativePath(candidate.Folder, file.Path))
            .ToList();
        FileNamesText = string.Join("\n", FileNames);

        IgnoreForGameText = Loc.F(Strings.IgnoreForGame, gameName);

        ToggleIgnoreCommand = new RelayCommand(() => ChoosingIgnore = !ChoosingIgnore);
        IgnoreForGameCommand = new RelayCommand(() => ignore(this, false));
        IgnoreForAllCommand = new RelayCommand(() => ignore(this, true));
    }

    /// <summary>La cartella proposta, con il punteggio e i file.</summary>
    public SaveCandidate Candidate { get; }

    public string Folder { get; }

    /// <summary>true per la prima cartella della lista.</summary>
    public bool Recommended { get; }

    /// <summary>true se il punteggio è sotto la soglia: la lista la marca.</summary>
    public bool Unlikely { get; }

    /// <summary>Etichetta della cartella: consigliata, poco probabile, o niente.</summary>
    public string Badge => Recommended ? Strings.LearnRecommended : Unlikely ? Strings.LearnUnlikely : "";

    /// <summary>true se la cartella ha un'etichetta da mostrare.</summary>
    public bool HasBadge => Badge != "";

    /// <summary>Un motivo per riga: ✓ per i punti presi, ✗ per quelli tolti.</summary>
    public IReadOnlyList<string> Reasons { get; }

    /// <summary>I motivi, uno per riga, da mostrare in un solo blocco.</summary>
    public string ReasonsText { get; }

    /// <summary>Quanti file sono cambiati nella cartella.</summary>
    public string FilesText { get; }

    /// <summary>I primi nomi dei file, relativi alla cartella.</summary>
    public IReadOnlyList<string> FileNames { get; }

    /// <summary>I nomi dei file, uno per riga.</summary>
    public string FileNamesText { get; }

    /// <summary>Testo del pulsante "Solo per {0}".</summary>
    public string IgnoreForGameText { get; }

    /// <summary>Se questa cartella è quella scelta con il pulsante radio.</summary>
    [ObservableProperty]
    private bool isSelected;

    /// <summary>Se la riga "Non è una cartella di salvataggi…" è aperta con i due pulsanti.</summary>
    [ObservableProperty]
    private bool choosingIgnore;

    public ICommand ToggleIgnoreCommand { get; }

    public ICommand IgnoreForGameCommand { get; }

    public ICommand IgnoreForAllCommand { get; }

    /// <summary>Il testo di un motivo, con il punto preso o tolto davanti.</summary>
    internal static string ReasonLine(ScoreReason reason)
    {
        string text = reason.Kind switch
        {
            ScoreReasonKind.GameName => Loc.F(Strings.ReasonGameName, reason.Detail),
            ScoreReasonKind.GameOwned => Strings.ReasonGameOwned,
            ScoreReasonKind.SaveLikeFile => Loc.F(Strings.ReasonSaveLikeFile, reason.Detail),
            ScoreReasonKind.ChangedFiles => Loc.F(Strings.ReasonChangedFiles, reason.Detail),
            ScoreReasonKind.SaveLikePath => Loc.F(Strings.ReasonSaveLikePath, reason.Detail),
            ScoreReasonKind.OnlySettings => Strings.ReasonOnlySettings,
            _ => Strings.ReasonOtherProgram,
        };

        return (reason.Points >= 0 ? "✓ " : "✗ ") + text;
    }
}
