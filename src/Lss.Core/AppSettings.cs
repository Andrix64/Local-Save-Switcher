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
    private const string LookKey = "CercaDopoPartita";
    private const string ModeKey = "Modalita";
    private const string FullScreenKey = "SchermoIntero";

    /// <summary>Modalità semplice (chi gioca sceglie il gioco).</summary>
    public const string SimpleMode = "semplice";

    /// <summary>Modalità avanzata (tutto).</summary>
    public const string AdvancedMode = "avanzata";

    private readonly string path;

    public string LastAccount { get; set; } = "";
    public string LastGame { get; set; } = "";

    /// <summary>Dopo una partita cerca altre cartelle dei salvataggi (CercaDopoPartita).</summary>
    public bool LookAfterPlaying { get; set; } = true;

    /// <summary>Schermo intero nella modalità semplice (SchermoIntero): LSS riparte così. Mai nell'avanzata.</summary>
    public bool FullScreen { get; set; }

    /// <summary>Modalita: <see cref="SimpleMode"/> o <see cref="AdvancedMode"/>. Senza chiave è avanzata.</summary>
    public string Mode { get; set; } = AdvancedMode;

    /// <summary>
    /// true se lss.ini non c'era: alla prima partenza si chiede la modalità.
    /// </summary>
    public bool FirstStart { get; private set; }

    public AppSettings(string path)
    {
        this.path = path;
    }

    /// <summary>
    /// true se il valore salvato (provider/id) è quell'account o quel gioco.
    /// </summary>
    public static bool Refers(string stored, string provider, string id) =>
        stored == provider + "/" + id
        || V1Compat.IsSteamIdWithoutProvider(stored, provider, id); // V1Compat: valori di prima della K5, senza launcher

    /// <summary>
    /// Legge lss.ini. Se manca o non si legge, i valori restano vuoti.
    /// </summary>
    public void Load()
    {
        FirstStart = !File.Exists(path);

        try
        {
            Dictionary<string, string> values = IniFile.Read(path);

            LastAccount = values.GetValueOrDefault(AccountKey, "");
            LastGame = values.GetValueOrDefault(GameKey, "");
            LookAfterPlaying = values.GetValueOrDefault(LookKey, "1") != "0";
            Mode = values.GetValueOrDefault(ModeKey, AdvancedMode) == SimpleMode ? SimpleMode : AdvancedMode;
            FullScreen = values.GetValueOrDefault(FullScreenKey, "0") == "1";
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
                new(GameKey, LastGame),
                new(LookKey, LookAfterPlaying ? "1" : "0"),
                new(ModeKey, Mode),
                new(FullScreenKey, FullScreen ? "1" : "0")
            ]);
        }
        catch (Exception ex)
        {
            Log.Write("Scrittura di " + path, ex);
        }
    }
}
