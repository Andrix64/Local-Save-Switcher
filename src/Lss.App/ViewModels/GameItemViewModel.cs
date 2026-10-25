using Avalonia.Media.Imaging;

namespace Lss.App.ViewModels;

/// <summary>
/// Un gioco nella colonna di sinistra: icona (o lettera iniziale), nome e,
/// con più launcher, l'intestazione del gruppo.
/// </summary>
public sealed class GameItemViewModel
{
    public GameItemViewModel(GameInfo info, GameAccount account, IGameProvider provider)
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
            _ => "",
        };
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
