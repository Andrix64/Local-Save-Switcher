using System.Windows.Input;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.Input;

namespace Lss.App.ViewModels;

/// <summary>
/// Un gioco nella colonna di sinistra: icona (o lettera iniziale), nome e,
/// con più launcher, l'intestazione del gruppo. Per i giochi in Altri giochi
/// il comando sceglie la cartella dei salvataggi a mano.
/// </summary>
public sealed class GameItemViewModel
{
    /// <param name="chooseFolder">Sceglie la cartella dei salvataggi di questo gioco; null = nessuna scelta.</param>
    public GameItemViewModel(GameInfo info, GameAccount account, IGameProvider provider,
        Func<GameItemViewModel, Task>? chooseFolder = null)
    {
        Info = info;
        Account = account;
        Provider = provider;
        Name = info.Name;
        Initial = info.Name.Length > 0 ? char.ToUpperInvariant(info.Name[0]).ToString() : "?";
        SupportText = info.Support switch
        {
            SupportState.SavesElsewhere => Strings.SupportSavesElsewhere,
            SupportState.NoSaves => Strings.SupportNoSaves,
            SupportState.NotFound => Strings.SupportNotFound,
            SupportState.Unsure => Strings.SupportUnsure,
            SupportState.Unsafe => Loc.F(Strings.SupportUnsafe, UnsafeReason(info.Reason)),
            _ => "",
        };

        // "Trova la cartella" aprirà la procedura di fase M; fino ad allora è la stessa scelta a mano.
        ChooseFolderCommand = new AsyncRelayCommand(() => chooseFolder?.Invoke(this) ?? Task.CompletedTask);
        FindFolderCommand = ChooseFolderCommand;
        Icon = LoadIcon(info.IconPath);
    }

    public GameInfo Info { get; }

    /// <summary>Account di cui è il gioco, e il launcher che lo gestisce.</summary>
    public GameAccount Account { get; }

    public IGameProvider Provider { get; }

    /// <summary>Nome del launcher sopra il primo gioco di ciascun gruppo, o "" (un solo launcher).</summary>
    public string LauncherHeader { get; set; } = "";

    public bool HasLauncherHeader => LauncherHeader != "";

    public string Name { get; }

    /// <summary>Icona 32×32, o null: al suo posto la lettera iniziale.</summary>
    public Bitmap? Icon { get; }

    public bool HasIcon => Icon != null;

    public string Initial { get; }

    /// <summary>Perché il gioco non è gestito: vuoto se lo è.</summary>
    public string SupportText { get; }

    /// <summary>Scelta della cartella dei salvataggi a mano.</summary>
    public ICommand ChooseFolderCommand { get; }

    /// <summary>Trova la cartella (fase M); per ora come ChooseFolderCommand.</summary>
    public ICommand FindFolderCommand { get; }

    /// <summary>
    /// Il motivo di Unsafe è il nome del problema (SafetyProblem): si traduce qui.
    /// </summary>
    private static string UnsafeReason(string reason) =>
        Enum.TryParse(reason, out SafetyProblem problem) ? SaveFolderSafety.Describe(problem) : reason;

    private static Bitmap? LoadIcon(string path)
    {
        if (path == "")
            return null;

        try
        {
            return new Bitmap(path);
        }
        catch (Exception ex)
        {
            Log.Write("Icona non caricata: " + path, ex);
            return null;
        }
    }
}
