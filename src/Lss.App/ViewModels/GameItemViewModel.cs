using System.Windows.Input;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lss.App.Services;

namespace Lss.App.ViewModels;

/// <summary>
/// Un gioco nella colonna di sinistra: icona (o lettera iniziale), nome e,
/// con più launcher, l'intestazione del gruppo. Per i giochi in Altri giochi
/// il comando sceglie la cartella dei salvataggi a mano.
/// </summary>
public sealed partial class GameItemViewModel : ObservableObject
{
    /// <param name="chooseFolder">Sceglie la cartella dei salvataggi di questo gioco; null = nessuna scelta.</param>
    /// <param name="findFolder">Procedura "Trova i salvataggi" di questo gioco; null = nessuna.</param>
    public GameItemViewModel(GameInfo info, GameAccount account, IGameProvider provider,
        Func<GameItemViewModel, Task>? chooseFolder = null, Func<GameItemViewModel, Task>? findFolder = null)
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

        ChooseFolderCommand = new AsyncRelayCommand(() => chooseFolder?.Invoke(this) ?? Task.CompletedTask);
        FindFolderCommand = new AsyncRelayCommand(() => findFolder?.Invoke(this) ?? Task.CompletedTask);
    }

    public GameInfo Info { get; }

    /// <summary>Account di cui è il gioco, e il launcher che lo gestisce.</summary>
    public GameAccount Account { get; }

    public IGameProvider Provider { get; }

    /// <summary>Nome del launcher sopra il primo gioco di ciascun gruppo, o "" (un solo launcher).</summary>
    public string LauncherHeader { get; set; } = "";

    public bool HasLauncherHeader => LauncherHeader != "";

    public string Name { get; }

    /// <summary>Icona 64×64, o null: al suo posto la lettera iniziale. Arriva in background (LoadIconAsync).</summary>
    [ObservableProperty]
    private Bitmap? icon;

    public bool HasIcon => Icon != null;

    public string Initial { get; }

    /// <summary>Perché il gioco non è gestito: vuoto se lo è.</summary>
    public string SupportText { get; }

    /// <summary>Scelta della cartella dei salvataggi a mano.</summary>
    public ICommand ChooseFolderCommand { get; }

    /// <summary>Trova la cartella dei salvataggi: la procedura "Trova i salvataggi" (fase M).</summary>
    public ICommand FindFolderCommand { get; }

    /// <summary>
    /// Il motivo di Unsafe è il nome del problema (SafetyProblem): si traduce qui.
    /// </summary>
    private static string UnsafeReason(string reason) =>
        Enum.TryParse(reason, out SafetyProblem problem) ? SaveFolderSafety.Describe(problem) : reason;

    partial void OnIconChanged(Bitmap? value) => OnPropertyChanged(nameof(HasIcon));

    /// <summary>
    /// Cerca l'icona del gioco (GameIcons) fuori dalla UI: la lista si mostra subito con
    /// la lettera, e l'icona arriva quando è pronta. dataRoot è la cartella dei dati.
    /// </summary>
    public async Task LoadIconAsync(string dataRoot)
    {
        // L'eseguibile si chiede al launcher qui, nel thread della UI.
        string exe = Provider.ExecutableOf(Info);
        // Dopo il disco non serve tornare nel thread della UI: il passaggio alla UI è Post.
        string? path = await Task.Run(() => GameIcons.Ensure(Info, exe, dataRoot)).ConfigureAwait(false);

        if (path is not null)
            Dispatcher.UIThread.Post(() => ShowIcon(path));
    }

    /// <summary>Mostra il PNG dell'icona (nel thread della UI).</summary>
    internal void ShowIcon(string path) => Icon = LoadBitmap(path);

    private static Bitmap? LoadBitmap(string path)
    {
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
