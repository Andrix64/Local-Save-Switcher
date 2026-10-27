namespace Lss;

/// <summary>
/// I posti di un gioco salvati in posti.ini nella cartella dati del gioco: le
/// cartelle scelte o imparate (il posto 1 e gli altri, con il loro id), le
/// cartelle extra da cercare, le cartelle ignorate per questo gioco (M3b) e
/// gli id dei posti dei launcher. I posti dei launcher (userdata, regole) non
/// si salvano: si ritrovano a ogni avvio, ma la riga Launcher.&lt;percorso&gt;
/// fa tenere loro lo stesso id.
/// Un cartella.ini della 1.0 si converte alla prima lettura (V1Compat).
/// Le chiavi che non sono di questa classe (Ignorate.n, Aggiunta.n) restano com'erano.
/// Non lancia eccezioni in lettura: un file illeggibile vale come vuoto.
/// </summary>
internal sealed class GameSpots
{
    private const string FileName = "posti.ini";
    private const string NextKey = "Prossimo";
    private const string SpotPrefix = "Posto.";
    private const string LauncherPrefix = "Launcher.";
    private const string ExtraKey = "CartelleExtra";
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

    /// <summary>Percorso di posti.ini.</summary>
    internal string FilePath => file;

    /// <summary>Cartella dati del gioco, dove stanno posti.ini e proposte.ini.</summary>
    public string GameDataFolder => Path.GetDirectoryName(file) ?? "";

    /// <summary>Posti scelti o imparati, in ordine di id (dopo Load).</summary>
    public IReadOnlyList<SaveSpot> Spots => spots.OrderBy(spot => spot.Id).ToList();

    /// <summary>Cartelle extra da cercare per questo gioco (dopo Load).</summary>
    public IReadOnlyList<string> ExtraRoots => extraRoots;

    /// <summary>
    /// Legge posti.ini. Se non c'è ma c'è un cartella.ini della 1.0, lo converte.
    /// </summary>
    public void Load()
    {
        spots.Clear();
        launcherIds.Clear();
        extraRoots.Clear();
        next = FirstExtraId;

        // V1Compat: cartella.ini della 1.0 diventa posti.ini
        if (V1Compat.ConvertOldSpots(this))
            return;

        Dictionary<string, string> values = IniFile.Read(file);

        foreach ((string key, string value) in values)
            ReadValue(key, value);

        if (spots.Count > 0)
            next = Math.Max(next, spots.Max(spot => spot.Id) + 1);

        if (launcherIds.Count > 0)
            next = Math.Max(next, launcherIds.Values.Max() + 1);
    }

    /// <summary>
    /// Aggiunge un posto con il prossimo id (mai riusato). Il percorso non
    /// può contenere "|": il formato lo usa come separatore.
    /// </summary>
    public SaveSpot Add(string folder, string pattern, string portable, SaveSource source, bool recursive = true)
    {
        CheckText(folder, portable, pattern);

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

        CheckText(folder, folder, "");
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
    /// riavvio: un id nuovo si scrive subito in posti.ini.
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
    /// Scrive posti.ini: le chiavi di questa classe e quelle di altre classi.
    /// Senza nulla da ricordare elimina il file.
    /// </summary>
    public bool Save()
    {
        IEnumerable<KeyValuePair<string, string>> others = IniFile.Read(file)
            .Where(pair => !IsOwned(pair.Key));
        return Write(others.ToList());
    }

    /// <summary>Come Save, ma con le chiavi di altre classi date (per V1Compat).</summary>
    internal bool Save(List<KeyValuePair<string, string>> others) => Write(others);

    private void ReadValue(string key, string value)
    {
        if (key == NextKey && int.TryParse(value, out int nextId))
        {
            next = nextId;
        }
        else if (key.StartsWith(SpotPrefix, StringComparison.OrdinalIgnoreCase)
                 && int.TryParse(key[SpotPrefix.Length..], out int id))
        {
            if (ParseSpot(id, value) is { } spot)
                spots.Add(spot);
        }
        else if (key.StartsWith(LauncherPrefix, StringComparison.OrdinalIgnoreCase)
                 && int.TryParse(value, out int launcherId))
        {
            launcherIds[key[LauncherPrefix.Length..]] = launcherId;
        }
        else if (key == ExtraKey)
        {
            extraRoots.AddRange(value.Split(';', StringSplitOptions.RemoveEmptyEntries));
        }
    }

    /// <summary>
    /// Un posto da "cartella|percorso portabile|maschera|ricorsiva|origine".
    /// null se la riga non è valida.
    /// </summary>
    private static SaveSpot? ParseSpot(int id, string value)
    {
        string[] parts = value.Split('|');

        if (parts.Length != 5 || parts[0] == "")
            return null;

        string portable = parts[1] == "" ? parts[0] : parts[1];

        return new SaveSpot(id, parts[0], parts[2], parts[3] == "1", portable, OriginOf(parts[4]));
    }

    internal static SaveSource OriginOf(string text) =>
        text == ImparateText ? SaveSource.Learned : text == ImportateText ? SaveSource.Imported : SaveSource.Chosen;

    private static string TextOf(SaveSource source) =>
        source == SaveSource.Learned ? ImparateText : source == SaveSource.Imported ? ImportateText : ScelteText;

    private static void CheckText(string folder, string portable, string pattern)
    {
        if (folder.Contains('|') || portable.Contains('|') || pattern.Contains('|'))
            throw new ArgumentException("Il percorso contiene '|': non si può salvare in posti.ini.");
    }

    private static bool IsOwned(string key) =>
        key == NextKey
        || key.StartsWith(SpotPrefix, StringComparison.OrdinalIgnoreCase)
        || key.StartsWith(LauncherPrefix, StringComparison.OrdinalIgnoreCase)
        || key == ExtraKey;

    /// <summary>
    /// Scrive posti.ini con le chiavi di questa classe e le altre date. Senza
    /// niente da ricordare elimina il file. Ritorna false se la scrittura fallisce.
    /// </summary>
    private bool Write(List<KeyValuePair<string, string>> others)
    {
        try
        {
            var values = new List<KeyValuePair<string, string>>(others);

            // Il posto 1 può tornare senza danni; gli id dopo il 1 non si riusano mai.
            if (spots.Count > 0 || launcherIds.Count > 0 || extraRoots.Count > 0 || next > 2)
                values.Add(new(NextKey, next.ToString(System.Globalization.CultureInfo.InvariantCulture)));

            foreach (SaveSpot spot in spots.OrderBy(spot => spot.Id))
            {
                string value = string.Join("|", spot.Folder, spot.PortablePath, spot.Pattern,
                    spot.Recursive ? "1" : "0", TextOf(spot.Source));

                values.Add(new(SpotPrefix + spot.Id, value));
            }

            foreach ((string portable, int id) in launcherIds.OrderBy(pair => pair.Value))
                values.Add(new(LauncherPrefix + portable, id.ToString(System.Globalization.CultureInfo.InvariantCulture)));

            if (extraRoots.Count > 0)
                values.Add(new(ExtraKey, string.Join(";", extraRoots)));

            if (values.Count == 0)
            {
                if (File.Exists(file))
                    File.Delete(file);

                return true;
            }

            IniFile.Write(file, values);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Write("posti.ini non salvato: " + file, ex);
            return false;
        }
    }
}
