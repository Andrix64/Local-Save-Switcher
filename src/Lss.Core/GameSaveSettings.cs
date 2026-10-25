namespace Lss;

/// <summary>
/// Cartella dei salvataggi scelta a mano o trovata da LSS, e le cartelle extra
/// da cercare: sono il posto 1 e le cartelle extra di posti.ini (GameSpots),
/// nella cartella dati del gioco. Non lancia eccezioni: un file illeggibile
/// vale come nessuna scelta, e gli errori di scrittura si registrano.
/// </summary>
internal sealed class GameSaveSettings
{
    private readonly string gameDataFolder;

    public GameSaveSettings(string gameDataFolder)
    {
        this.gameDataFolder = gameDataFolder;
    }

    /// <summary>Cartella dei salvataggi scelta o trovata, o "" se non c'è.</summary>
    public string Folder { get; set; } = "";

    /// <summary>Chosen (scelta da te) o Learned (trovata da LSS).</summary>
    public SaveSource Origin { get; set; } = SaveSource.Chosen;

    /// <summary>
    /// Altre cartelle in cui cercare il gioco (CartelleExtra in posti.ini),
    /// come sono adesso nel file.
    /// </summary>
    public IReadOnlyList<string> ExtraFolders => ReadSpots(new GameSpots(gameDataFolder)).ExtraRoots;

    /// <summary>
    /// Aggiunge una cartella da cercare, senza ripetere una già presente, e salva il file.
    /// </summary>
    public void AddExtraFolder(string folder)
    {
        GameSpots spots = ReadSpots(new GameSpots(gameDataFolder));

        if (spots.ExtraRoots.Contains(folder, StringComparer.OrdinalIgnoreCase))
            return;

        spots.AddExtraRoot(folder);
        spots.Save();
    }

    /// <summary>
    /// Legge il posto 1 (cartella e origine). Senza file o con valori mancanti: nessuna scelta.
    /// </summary>
    public void Load()
    {
        GameSpots spots = ReadSpots(new GameSpots(gameDataFolder));
        SaveSpot? primary = spots.Spots.FirstOrDefault(spot => spot.Id == 1);

        Folder = primary?.Folder ?? "";
        Origin = primary?.Source ?? SaveSource.Chosen;
    }

    /// <summary>
    /// Scrive il posto 1 in posti.ini. Con Folder "" lo toglie, ed elimina il file
    /// se non ha altro. Le altre chiavi (cartelle ignorate, altri posti) restano com'erano.
    /// </summary>
    public void Save()
    {
        try
        {
            GameSpots spots = ReadSpots(new GameSpots(gameDataFolder));
            spots.SetPrimary(Folder, Origin);
            spots.Save();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            Log.Write("posti.ini non salvato: " + gameDataFolder, ex);
        }
    }

    /// <summary>
    /// Legge posti.ini (e converte cartella.ini se serve), restituendo lo stesso oggetto.
    /// </summary>
    private static GameSpots ReadSpots(GameSpots spots)
    {
        spots.Load();
        return spots;
    }
}
