using System.Text.Json.Serialization;

namespace Lss;

/// <summary>Un posto come è scritto in posti.json.</summary>
internal sealed class SpotEntry
{
    [JsonPropertyName("Id")]
    public int Id { get; set; }

    [JsonPropertyName("Cartella")]
    public string Folder { get; set; } = "";

    // Percorso portabile; vuoto = lo stesso della cartella.
    [JsonPropertyName("Portabile")]
    public string Portable { get; set; } = "";

    [JsonPropertyName("Maschera")]
    public string Pattern { get; set; } = "";

    [JsonPropertyName("Ricorsiva")]
    public bool Recursive { get; set; } = true;

    // "scelta", "imparata" o "importata".
    [JsonPropertyName("Origine")]
    public string Origin { get; set; } = "scelta";
}

/// <summary>L'id del posto di un launcher, con la sua chiave, come è scritto in posti.json.</summary>
internal sealed class LauncherSpotEntry
{
    [JsonPropertyName("Chiave")]
    public string Key { get; set; } = "";

    [JsonPropertyName("Id")]
    public int Id { get; set; }
}

/// <summary>
/// Come posti.json è scritto. Un solo file con due parti: i posti (GameSpots) e le cartelle
/// ignorate del gioco (IgnoredFolders). Ogni classe legge tutto il file, cambia solo la sua parte
/// e riscrive tutto: una che scrivesse solo la sua cancellerebbe l'altra.
/// </summary>
internal sealed class SpotsFile
{
    // 0 = non scritto.
    [JsonPropertyName("Prossimo")]
    public int Next { get; set; }

    [JsonPropertyName("Posti")]
    public List<SpotEntry> Spots { get; set; } = [];

    [JsonPropertyName("Launcher")]
    public List<LauncherSpotEntry> Launchers { get; set; } = [];

    [JsonPropertyName("CartelleExtra")]
    public List<string> ExtraRoots { get; set; } = [];

    [JsonPropertyName("Ignorate")]
    public List<IgnoredEntry> Ignored { get; set; } = [];

    /// <summary>true se non c'è niente da ricordare: il file si elimina.</summary>
    [JsonIgnore]
    public bool IsEmpty =>
        Next == 0 && Spots.Count == 0 && Launchers.Count == 0 && ExtraRoots.Count == 0 && Ignored.Count == 0;

    /// <summary>Il file (vuoto se manca o è rotto).</summary>
    public static SpotsFile Read(string path) => JsonFile.Read<SpotsFile>(path) ?? new SpotsFile();

    /// <summary>Scrive il file; senza niente da ricordare lo elimina. Lancia un'eccezione in caso di errore.</summary>
    public static void Write(string path, SpotsFile file)
    {
        if (file.IsEmpty)
        {
            if (File.Exists(path))
                File.Delete(path);

            return;
        }

        JsonFile.Write(path, file);
    }
}

/// <summary>
/// I posti di un gioco salvati in posti.json nella cartella dati del gioco: le
/// cartelle scelte o imparate (il posto 1 e gli altri, con il loro id), le
/// cartelle extra da cercare e gli id dei posti dei launcher. I posti dei launcher
/// (userdata, regole) non si salvano: si ritrovano a ogni avvio, ma la voce
/// Launcher fa tenere loro lo stesso id. Le cartelle ignorate per questo gioco
/// (M3b) stanno nello stesso file ma sono di IgnoredFolders: qui restano com'erano.
/// Non lancia eccezioni in lettura: un file illeggibile vale come vuoto.
/// </summary>
internal sealed class GameSpots
{
    private const string FileName = "posti.json";
    private const string ScelteText = "scelta";
    private const string ImparateText = "imparata";
    private const string ImportateText = "importata";

    private const int FirstExtraId = 2;

    private readonly string file;
    private readonly List<SaveSpot> spots = [];
    private readonly Dictionary<string, int> launcherIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> extraRoots = [];
    // Il posto 1 è quello principale (in cima allo snapshot): gli altri id partono da 2.
    private int next = FirstExtraId;

    public GameSpots(string gameDataFolder)
    {
        file = Path.Combine(gameDataFolder, FileName);
    }

    /// <summary>Percorso di posti.json.</summary>
    internal string FilePath => file;

    /// <summary>Cartella dati del gioco, dove stanno posti.json e proposte.json.</summary>
    public string GameDataFolder => Path.GetDirectoryName(file) ?? "";

    /// <summary>Posti scelti o imparati, in ordine di id (dopo Load).</summary>
    public IReadOnlyList<SaveSpot> Spots => spots.OrderBy(spot => spot.Id).ToList();

    /// <summary>Cartelle extra da cercare per questo gioco (dopo Load).</summary>
    public IReadOnlyList<string> ExtraRoots => extraRoots;

    /// <summary>Legge posti.json.</summary>
    public void Load()
    {
        spots.Clear();
        launcherIds.Clear();
        extraRoots.Clear();
        next = FirstExtraId;

        SpotsFile data = SpotsFile.Read(file);

        if (data.Next > 0)
            next = data.Next;

        foreach (SpotEntry entry in data.Spots)
        {
            if (entry.Folder == "")
                continue;

            string portable = entry.Portable == "" ? entry.Folder : entry.Portable;
            spots.Add(new SaveSpot(entry.Id, entry.Folder, entry.Pattern, entry.Recursive, portable, OriginOf(entry.Origin)));
        }

        foreach (LauncherSpotEntry entry in data.Launchers)
            launcherIds[entry.Key] = entry.Id;

        foreach (string root in data.ExtraRoots.Where(root => root != ""))
            AddExtraRoot(root);

        if (spots.Count > 0)
            next = Math.Max(next, spots.Max(spot => spot.Id) + 1);

        if (launcherIds.Count > 0)
            next = Math.Max(next, launcherIds.Values.Max() + 1);
    }

    /// <summary>
    /// Aggiunge un posto con il prossimo id (mai riusato).
    /// </summary>
    public SaveSpot Add(string folder, string pattern, string portable, SaveSource source, bool recursive = true)
    {
        var spot = new SaveSpot(next++, folder, pattern, recursive, portable, source);
        spots.Add(spot);

        return spot;
    }

    /// <summary>
    /// Mette la cartella del posto 1 (scelta o imparata), o la toglie se folder è "".
    /// </summary>
    public void SetPrimary(string folder, SaveSource source)
    {
        spots.RemoveAll(spot => spot.Id == 1);

        if (folder == "")
            return;

        spots.Add(new SaveSpot(1, folder, "", true, folder, source));
        next = Math.Max(next, 2);
    }

    /// <summary>
    /// Fa in modo che i prossimi id siano dopo id (gli id dei posti del gioco già noti).
    /// </summary>
    public void ReserveIdsAbove(int id)
    {
        next = Math.Max(next, id + 1);
    }

    /// <summary>Toglie un posto. Il suo id non torna più in uso.</summary>
    public void Remove(int id)
    {
        spots.RemoveAll(spot => spot.Id == id);
    }

    /// <summary>
    /// Aggiunge una cartella extra da cercare per questo gioco, se non c'è già.
    /// </summary>
    public void AddExtraRoot(string folder)
    {
        if (!extraRoots.Contains(folder, StringComparer.OrdinalIgnoreCase))
            extraRoots.Add(folder);
    }

    /// <summary>
    /// Id del posto di un launcher, identificato dalla sua chiave (percorso portabile,
    /// maschera, ricorsione). La stessa chiave ha sempre lo stesso id, anche dopo un
    /// riavvio: un id nuovo si scrive subito in posti.json.
    /// </summary>
    public int LauncherId(string key)
    {
        if (launcherIds.TryGetValue(key, out int id))
            return id;

        id = next++;
        launcherIds[key] = id;
        Save();

        return id;
    }

    /// <summary>
    /// Scrive posti.json: i posti di questa classe, lasciando le cartelle ignorate come sono.
    /// Senza nulla da ricordare elimina il file. Ritorna false se la scrittura fallisce.
    /// </summary>
    public bool Save()
    {
        try
        {
            SpotsFile data = SpotsFile.Read(file);

            // Il posto 1 può tornare senza danni; gli id dopo il 1 non si riusano mai.
            data.Next = spots.Count > 0 || launcherIds.Count > 0 || extraRoots.Count > 0 || next > 2 ? next : 0;
            data.Spots = spots.OrderBy(spot => spot.Id).Select(spot => new SpotEntry
            {
                Id = spot.Id,
                Folder = spot.Folder,
                Portable = spot.PortablePath,
                Pattern = spot.Pattern,
                Recursive = spot.Recursive,
                Origin = TextOf(spot.Source),
            }).ToList();
            data.Launchers = launcherIds.OrderBy(pair => pair.Value)
                .Select(pair => new LauncherSpotEntry { Key = pair.Key, Id = pair.Value })
                .ToList();
            data.ExtraRoots = extraRoots.ToList();

            SpotsFile.Write(file, data);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Write("posti.json non salvato: " + file, ex);
            return false;
        }
    }

    internal static SaveSource OriginOf(string text) =>
        text == ImparateText ? SaveSource.Learned : text == ImportateText ? SaveSource.Imported : SaveSource.Chosen;

    internal static string TextOf(SaveSource source) =>
        source == SaveSource.Learned ? ImparateText : source == SaveSource.Imported ? ImportateText : ScelteText;
}
