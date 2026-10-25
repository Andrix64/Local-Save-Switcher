using System.Globalization;

namespace Lss;

/// <summary>
/// Regole Auto-Cloud dei giochi dalla cache di Steam (appcache/appinfo.vdf).
/// Letto una volta per avvio; nessuna eccezione verso l'esterno: un file
/// rotto vale per quello che si è letto prima dell'errore.
/// </summary>
internal sealed class SteamAppInfo
{
    private const uint MagicV28 = 0x07564428;
    private const uint MagicV29 = 0x07564429;

    // Dopo la dimensione di ogni gioco: infoState, lastUpdated, picsToken,
    // sha1, changeNumber e sha1 dei dati (4+4+8+20+4+20).
    private const int EntryHeaderSize = 60;

    private readonly Dictionary<string, IReadOnlyList<SaveRule>> rules = new();
    private readonly Dictionary<string, IReadOnlyList<RootOverride>> overrides = new();

    private SteamAppInfo()
    {
    }

    /// <summary>
    /// Legge da steamRoot\appcache\appinfo.vdf le regole dei giochi in
    /// appIds. File assente o non riconosciuto: nessuna regola.
    /// </summary>
    public static SteamAppInfo Load(string steamRoot, IReadOnlySet<string> appIds)
    {
        var info = new SteamAppInfo();
        string file = Path.Combine(steamRoot, "appcache", "appinfo.vdf");

        if (appIds.Count == 0 || !File.Exists(file))
            return info;

        try
        {
            // FileShare.ReadWrite: Steam può stare scrivendo il file.
            using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            info.ReadApps(stream, appIds);
        }
        catch (Exception ex)
        {
            Log.Write("appinfo.vdf non letto: " + file, ex);
        }

        return info;
    }

    /// <summary>
    /// Regole Auto-Cloud del gioco, in ordine. Vuoto se Steam non ne ha.
    /// </summary>
    public IReadOnlyList<SaveRule> RulesOf(string appId) =>
        rules.TryGetValue(appId, out IReadOnlyList<SaveRule>? found) ? found : [];

    /// <summary>
    /// Sostituzioni di radici del gioco (rootoverrides). Vuoto se non ce ne sono.
    /// </summary>
    public IReadOnlyList<RootOverride> OverridesOf(string appId) =>
        overrides.TryGetValue(appId, out IReadOnlyList<RootOverride>? found) ? found : [];

    private void ReadApps(FileStream stream, IReadOnlySet<string> appIds)
    {
        using var reader = new BinaryReader(stream);

        uint magic = reader.ReadUInt32();
        reader.ReadUInt32(); // universo: non serve

        IReadOnlyList<string>? strings = null;

        if (magic == MagicV29)
        {
            long tableOffset = reader.ReadInt64();
            strings = ReadStringTable(reader, tableOffset);
        }
        else if (magic != MagicV28)
        {
            Log.Write("appinfo.vdf: formato " + magic.ToString("X8", CultureInfo.InvariantCulture) + " sconosciuto");
            return;
        }

        int found = 0;

        while (found < appIds.Count)
        {
            uint id = reader.ReadUInt32();

            if (id == 0)
                break;

            uint size = reader.ReadUInt32();
            string appId = id.ToString(CultureInfo.InvariantCulture);

            if (size < EntryHeaderSize)
                throw new InvalidDataException("appinfo.vdf: voce troppo corta.");

            if (!appIds.Contains(appId))
            {
                // Gioco che non serve: si salta senza leggerlo.
                stream.Seek(size, SeekOrigin.Current);
                continue;
            }

            found++;
            byte[] entry = ReadExact(reader, (int)size);

            try
            {
                using var vdf = new BinaryReader(new MemoryStream(entry, EntryHeaderSize, (int)size - EntryHeaderSize));
                AddApp(appId, BinaryVdf.Read(vdf, strings));
            }
            catch (InvalidDataException ex)
            {
                Log.Write("appinfo.vdf: gioco " + appId + " non letto", ex);
            }
        }
    }

    /// <summary>
    /// Tabella delle chiavi del formato v29 (uint32 numero, poi testi terminati
    /// da 0). Il flusso torna alla posizione di prima.
    /// </summary>
    private static IReadOnlyList<string> ReadStringTable(BinaryReader reader, long offset)
    {
        long back = reader.BaseStream.Position;
        reader.BaseStream.Seek(offset, SeekOrigin.Begin);

        uint count = reader.ReadUInt32();
        var strings = new List<string>();

        for (uint i = 0; i < count; i++)
            strings.Add(BinaryVdf.ReadCString(reader));

        reader.BaseStream.Seek(back, SeekOrigin.Begin);
        return strings;
    }

    private static byte[] ReadExact(BinaryReader reader, int count)
    {
        byte[] data = reader.ReadBytes(count);

        return data.Length == count ? data : throw new EndOfStreamException("appinfo.vdf troncato.");
    }

    private void AddApp(string appId, Vdf.Node root)
    {
        if (root.GetValueOrDefault("appinfo") is not Vdf.Node app || app.GetValueOrDefault("ufs") is not Vdf.Node ufs)
            return;

        rules[appId] = SaveRulesOf(ufs);
        overrides[appId] = RootOverridesOf(ufs);
    }

    private static List<SaveRule> SaveRulesOf(Vdf.Node ufs)
    {
        var result = new List<SaveRule>();

        foreach (Vdf.Node entry in Entries(ufs, "savefiles"))
        {
            string root = Text(entry, "root");

            if (root == "")
            {
                Log.Write("appinfo.vdf: regola senza radice ignorata");
                continue;
            }

            string pattern = Text(entry, "pattern");

            result.Add(new SaveRule(
                root,
                Text(entry, "path").Replace('\\', '/'),
                pattern == "" ? "*" : pattern,
                Text(entry, "recursive") == "1",
                Platforms(entry)));
        }

        return result;
    }

    private static List<RootOverride> RootOverridesOf(Vdf.Node ufs)
    {
        var result = new List<RootOverride>();

        foreach (Vdf.Node entry in Entries(ufs, "rootoverrides"))
        {
            var transforms = Entries(entry, "pathtransforms")
                .Select(transform => (Find: Text(transform, "find"), Replace: Text(transform, "replace")))
                .ToList();

            result.Add(new RootOverride(
                Text(entry, "root"),
                Text(entry, "os"),
                Text(entry, "oscompare"),
                Text(entry, "useinstead"),
                Text(entry, "addpath").Replace('\\', '/'),
                transforms));
        }

        return result;
    }

    /// <summary>
    /// Sistemi di una regola (platforms: valori "1", "2"... in ordine). Vuoto = tutti.
    /// </summary>
    private static List<string> Platforms(Vdf.Node entry)
    {
        if (entry.GetValueOrDefault("platforms") is not Vdf.Node block)
            return [];

        return Ordered(block).Select(pair => pair.Value).OfType<string>().ToList();
    }

    /// <summary>
    /// Blocchi figli di un nodo con il nome dato, in ordine numerico ("0", "1"...).
    /// </summary>
    private static List<Vdf.Node> Entries(Vdf.Node node, string name)
    {
        if (node.GetValueOrDefault(name) is not Vdf.Node block)
            return [];

        return Ordered(block).Select(pair => pair.Value).OfType<Vdf.Node>().ToList();
    }

    private static IEnumerable<KeyValuePair<string, object>> Ordered(Vdf.Node block) =>
        block.OrderBy(pair => int.TryParse(pair.Key, NumberStyles.None, CultureInfo.InvariantCulture, out int n) ? n : int.MaxValue);

    /// <summary>
    /// Valore di testo di una chiave, o "" se manca o non è testo.
    /// </summary>
    private static string Text(Vdf.Node node, string key) =>
        node.GetValueOrDefault(key) as string ?? "";
}
