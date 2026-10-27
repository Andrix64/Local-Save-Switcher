using System.Text.Json.Serialization;

namespace Lss;

/// <summary>Come lss.json è scritto: nomi corti in italiano.</summary>
internal sealed class AppSettingsFile
{
    [JsonPropertyName("UltimoAccount")]
    public string LastAccount { get; set; } = "";

    [JsonPropertyName("UltimoGioco")]
    public string LastGame { get; set; } = "";

    [JsonPropertyName("CercaDopoPartita")]
    public bool LookAfterPlaying { get; set; } = true;

    [JsonPropertyName("Modalita")]
    public string Mode { get; set; } = AppSettings.AdvancedMode;

    [JsonPropertyName("SchermoIntero")]
    public bool FullScreen { get; set; }
}

/// <summary>
/// Impostazioni generali dell'app in lss.json nella cartella dei dati:
/// ultimo account e ultimo gioco usati, modalità, schermo intero. Non lancia eccezioni: sono solo
/// comodità.
/// </summary>
internal sealed class AppSettings
{
    /// <summary>Modalità semplice (chi gioca sceglie il gioco).</summary>
    public const string SimpleMode = "semplice";

    /// <summary>Modalità avanzata (tutto).</summary>
    public const string AdvancedMode = "avanzata";

    private const string FileName = "lss.json";

    private readonly string path;

    public string LastAccount { get; set; } = "";
    public string LastGame { get; set; } = "";

    /// <summary>Dopo una partita cerca altre cartelle dei salvataggi (CercaDopoPartita).</summary>
    public bool LookAfterPlaying { get; set; } = true;

    /// <summary>Schermo intero nella modalità semplice (SchermoIntero): LSS riparte così. Mai nell'avanzata.</summary>
    public bool FullScreen { get; set; }

    /// <summary>Modalita: <see cref="SimpleMode"/> o <see cref="AdvancedMode"/>. Senza il valore è avanzata.</summary>
    public string Mode { get; set; } = AdvancedMode;

    /// <summary>
    /// true se lss.json non c'era (dopo la conversione dei file vecchi): alla prima partenza si chiede la modalità.
    /// </summary>
    public bool FirstStart { get; private set; }

    public AppSettings(string path)
    {
        this.path = path;
    }

    /// <summary>Il file delle impostazioni nella cartella dei dati.</summary>
    public static string FileIn(string dataRoot) => Path.Combine(dataRoot, FileName);

    /// <summary>
    /// true se il valore salvato (provider/id) è quell'account o quel gioco.
    /// </summary>
    public static bool Refers(string stored, string provider, string id) =>
        stored == provider + "/" + id
        || V1Compat.IsSteamIdWithoutProvider(stored, provider, id); // V1Compat: valori di prima della K5, senza launcher

    /// <summary>
    /// Legge lss.json. Se manca o non si legge, i valori restano quelli di partenza.
    /// </summary>
    public void Load()
    {
        FirstStart = !File.Exists(path);

        try
        {
            if (JsonFile.Read<AppSettingsFile>(path) is not { } file)
                return;

            LastAccount = file.LastAccount ?? "";
            LastGame = file.LastGame ?? "";
            LookAfterPlaying = file.LookAfterPlaying;
            Mode = file.Mode == SimpleMode ? SimpleMode : AdvancedMode;
            FullScreen = file.FullScreen;
        }
        catch (Exception ex)
        {
            Log.Write("Lettura di " + path, ex);
        }
    }

    /// <summary>
    /// Scrive lss.json. Un errore viene solo registrato.
    /// </summary>
    public void Save()
    {
        try
        {
            JsonFile.Write(path, new AppSettingsFile
            {
                LastAccount = LastAccount,
                LastGame = LastGame,
                LookAfterPlaying = LookAfterPlaying,
                Mode = Mode,
                FullScreen = FullScreen,
            });
        }
        catch (Exception ex)
        {
            Log.Write("Scrittura di " + path, ex);
        }
    }
}
