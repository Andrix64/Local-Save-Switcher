namespace Lss;

/// <summary>
/// Impostazioni generali dell'app in lss.ini nella cartella dei dati:
/// ultimo account e ultimo gioco usati. Non lancia eccezioni: sono solo
/// comodità.
/// </summary>
internal sealed class AppSettings
{
    private const string AccountKey = "UltimoAccount";
    private const string GameKey = "UltimoGioco";

    private readonly string path;

    public string LastAccount { get; set; } = "";
    public string LastGame { get; set; } = "";

    public AppSettings(string path)
    {
        this.path = path;
    }

    /// <summary>
    /// true se il valore salvato (provider/id) è quell'account o quel gioco. I
    /// valori di prima della K5 non hanno il launcher: sono di Steam.
    /// </summary>
    public static bool Refers(string stored, string provider, string id) =>
        stored.Contains('/') ? stored == provider + "/" + id : provider == "steam" && stored == id;

    /// <summary>
    /// Legge lss.ini. Se manca o non si legge, i valori restano vuoti.
    /// </summary>
    public void Load()
    {
        try
        {
            Dictionary<string, string> values = IniFile.Read(path);

            LastAccount = values.GetValueOrDefault(AccountKey, "");
            LastGame = values.GetValueOrDefault(GameKey, "");
        }
        catch (Exception ex)
        {
            Log.Write("Lettura di " + path, ex);
        }
    }

    /// <summary>
    /// Scrive lss.ini. Un errore viene solo registrato.
    /// </summary>
    public void Save()
    {
        try
        {
            IniFile.Write(path,
            [
                new(AccountKey, LastAccount),
                new(GameKey, LastGame)
            ]);
        }
        catch (Exception ex)
        {
            Log.Write("Scrittura di " + path, ex);
        }
    }
}
