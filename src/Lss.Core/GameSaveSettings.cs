namespace Lss;

/// <summary>
/// Cartella dei salvataggi scelta a mano o trovata da LSS, salvata in
/// cartella.ini nella cartella dati del gioco. Non lancia eccezioni: un file
/// illeggibile vale come nessuna scelta, e gli errori di scrittura si registrano.
/// </summary>
internal sealed class GameSaveSettings
{
    private const string FileName = "cartella.ini";
    private const string FolderKey = "CartellaSalvataggi";
    private const string OriginKey = "Origine";

    private readonly string file;

    public GameSaveSettings(string gameDataFolder)
    {
        file = Path.Combine(gameDataFolder, FileName);
    }

    /// <summary>Cartella dei salvataggi scelta o trovata, o "" se non c'è.</summary>
    public string Folder { get; set; } = "";

    /// <summary>Chosen (scelta da te) o Learned (trovata da LSS).</summary>
    public SaveSource Origin { get; set; } = SaveSource.Chosen;

    /// <summary>
    /// Legge cartella.ini. Senza file o con valori mancanti: nessuna scelta.
    /// </summary>
    public void Load()
    {
        Dictionary<string, string> values = IniFile.Read(file);

        Folder = values.GetValueOrDefault(FolderKey) ?? "";
        Origin = values.GetValueOrDefault(OriginKey) == "imparata" ? SaveSource.Learned : SaveSource.Chosen;
    }

    /// <summary>
    /// Scrive cartella.ini. Con Folder "" elimina il file, se non ha altre chiavi.
    /// Le altre chiavi (cartelle ignorate, cartelle extra) restano com'erano.
    /// </summary>
    public void Save()
    {
        try
        {
            List<KeyValuePair<string, string>> others = OtherValues();

            if (Folder == "" && others.Count == 0)
            {
                if (File.Exists(file))
                    File.Delete(file);

                return;
            }

            var values = new List<KeyValuePair<string, string>>();

            if (Folder != "")
            {
                string origin = Origin == SaveSource.Learned ? "imparata" : "scelta";

                values.Add(new(FolderKey, Folder));
                values.Add(new(OriginKey, origin));
            }

            values.AddRange(others);
            IniFile.Write(file, values);
        }
        catch (Exception ex)
        {
            Log.Write("cartella.ini non salvato: " + file, ex);
        }
    }

    /// <summary>
    /// Le chiavi del file diverse da cartella e origine, come sono adesso nel file.
    /// </summary>
    private List<KeyValuePair<string, string>> OtherValues() =>
        IniFile.Read(file)
            .Where(pair => pair.Key != FolderKey && pair.Key != OriginKey)
            .ToList();
}
