using System.Globalization;

namespace Lss;

/// <summary>Cartella segnata come "non di salvataggi", con l'ambito.</summary>
/// <param name="Folder">Cartella: assoluta per un gioco solo, percorso portabile per tutti i giochi.</param>
/// <param name="PortablePath">Come è salvata nel file (per tutti i giochi, la radice tra graffe).</param>
/// <param name="GameKey">Cartella dati del gioco per un gioco solo; "" per tutti i giochi.</param>
/// <param name="Added">Quando è stata segnata.</param>
public sealed record IgnoredFolder(string Folder, string PortablePath, string GameKey, DateTime Added);

/// <summary>
/// Cartelle che la ricerca non propone mai più: per tutti i giochi
/// (cartelle-ignorate.ini nella cartella dei dati, chiavi Cartella.n e Aggiunta.n)
/// o per un gioco solo (posti.ini del gioco, chiavi Ignorate.n e Aggiunta.n).
/// Non lancia eccezioni: un file illeggibile vale come nessuna cartella.
/// </summary>
internal sealed class IgnoredFolders
{
    private const string FileName = "cartelle-ignorate.ini";
    private const string GameFileName = "posti.ini";
    private const string GlobalKey = "Cartella";
    private const string GameKey = "Ignorate";
    private const string AddedKey = "Aggiunta";

    private readonly string file;
    private readonly List<IgnoredFolder> global = [];

    public IgnoredFolders(string dataRoot)
    {
        file = Path.Combine(dataRoot, FileName);
    }

    /// <summary>Cartelle ignorate per tutti i giochi, come sono nel file (dopo Load).</summary>
    public IReadOnlyList<IgnoredFolder> Global => global;

    /// <summary>Legge cartelle-ignorate.ini.</summary>
    public void Load()
    {
        global.Clear();
        global.AddRange(Read(file, "", GlobalKey));
    }

    /// <summary>Cartelle ignorate per un gioco solo, dalla sua cartella dei dati.</summary>
    public IReadOnlyList<IgnoredFolder> ForGame(string gameDataFolder)
    {
        ConvertOldFile(gameDataFolder);
        return Read(Path.Combine(gameDataFolder, GameFileName), gameDataFolder, GameKey);
    }

    /// <summary>Segna una cartella per tutti i giochi, nel percorso portabile dato. Non ripete una voce già presente.</summary>
    public void AddGlobal(string portablePath)
    {
        Load();

        if (global.Any(entry => string.Equals(entry.PortablePath, portablePath, StringComparison.OrdinalIgnoreCase)))
            return;

        global.Add(new IgnoredFolder(portablePath, portablePath, "", DateTime.Now));
        Write(file, GlobalKey, global);
    }

    /// <summary>Segna una cartella per un gioco solo, nella sua cartella dei dati.</summary>
    public void AddForGame(string gameDataFolder, string folder)
    {
        ConvertOldFile(gameDataFolder);
        string path = Path.Combine(gameDataFolder, GameFileName);
        List<IgnoredFolder> entries = Read(path, gameDataFolder, GameKey).ToList();

        if (entries.Any(entry => string.Equals(entry.Folder, folder, StringComparison.OrdinalIgnoreCase)))
            return;

        entries.Add(new IgnoredFolder(folder, folder, gameDataFolder, DateTime.Now));
        Write(path, GameKey, entries);
    }

    /// <summary>Toglie una cartella da quelle per tutti i giochi, con il suo percorso portabile.</summary>
    public void RemoveGlobal(string portablePath)
    {
        Load();
        global.RemoveAll(entry => string.Equals(entry.PortablePath, portablePath, StringComparison.OrdinalIgnoreCase));
        Write(file, GlobalKey, global);
    }

    /// <summary>Toglie una cartella da quelle di un gioco solo.</summary>
    public void RemoveForGame(string gameDataFolder, string folder)
    {
        ConvertOldFile(gameDataFolder);
        string path = Path.Combine(gameDataFolder, GameFileName);
        List<IgnoredFolder> entries = Read(path, gameDataFolder, GameKey).ToList();
        entries.RemoveAll(entry => string.Equals(entry.Folder, folder, StringComparison.OrdinalIgnoreCase));
        Write(path, GameKey, entries);
    }

    /// <summary>
    /// Tutte le cartelle ignorate per questo gioco, già riportate al percorso del
    /// sistema e del gioco (le voci che non esistono qui non ci sono).
    /// </summary>
    public IReadOnlyList<string> Folders(string gameDataFolder, SaveContext context)
    {
        var folders = new List<string>();

        foreach (IgnoredFolder entry in global)
        {
            string? resolved = SavePathResolver.FromPortable(entry.PortablePath, context);

            if (resolved != null)
                folders.Add(resolved);
        }

        folders.AddRange(ForGame(gameDataFolder).Select(entry => entry.Folder));

        return folders;
    }

    /// <summary>
    /// true se la cartella, o una che la contiene, è ignorata per questo gioco o per tutti.
    /// </summary>
    public bool IsIgnored(string folder, string gameDataFolder, SaveContext context)
    {
        StringComparison comparison = context.IgnoreCase || OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        return Folders(gameDataFolder, context).Any(ignored =>
            string.Equals(folder, ignored, comparison)
            || folder.StartsWith(ignored.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, comparison));
    }

    /// <summary>
    /// Converte cartella.ini in posti.ini (GameSpots) prima di leggere o scrivere
    /// le cartelle ignorate del gioco, così non si perdono le voci vecchie.
    /// </summary>
    private static void ConvertOldFile(string gameDataFolder) => new GameSpots(gameDataFolder).Load();

    /// <summary>
    /// Legge le voci di una famiglia (Cartella.n o Ignorate.n) con le loro date.
    /// </summary>
    private static List<IgnoredFolder> Read(string path, string gameKey, string key)
    {
        Dictionary<string, string> values = IniFile.Read(path);
        var entries = new List<IgnoredFolder>();

        for (int n = 0; values.TryGetValue(key + "." + n, out string? value); n++)
        {
            DateTime added = DateTime.MinValue;

            if (values.TryGetValue(AddedKey + "." + n, out string? date))
                DateTime.TryParse(date, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out added);

            entries.Add(new IgnoredFolder(value, value, gameKey, added));
        }

        return entries;
    }

    /// <summary>
    /// Scrive le voci di una famiglia; le altre chiavi del file restano. Senza voci
    /// e senza altre chiavi elimina il file. Un errore si registra.
    /// </summary>
    private static void Write(string path, string key, IReadOnlyList<IgnoredFolder> entries)
    {
        try
        {
            List<KeyValuePair<string, string>> values = IniFile.Read(path)
                .Where(pair => !IsOfFamily(pair.Key, key))
                .ToList();

            for (int n = 0; n < entries.Count; n++)
            {
                values.Add(new(key + "." + n, entries[n].PortablePath));
                values.Add(new(AddedKey + "." + n, entries[n].Added.ToString("O", CultureInfo.InvariantCulture)));
            }

            if (values.Count == 0)
            {
                if (File.Exists(path))
                    File.Delete(path);

                return;
            }

            IniFile.Write(path, values);
        }
        catch (Exception ex)
        {
            Log.Write("Cartelle ignorate non salvate: " + path, ex);
        }
    }

    private static bool IsOfFamily(string name, string key) =>
        name.StartsWith(key + ".", StringComparison.OrdinalIgnoreCase)
        || name.StartsWith(AddedKey + ".", StringComparison.OrdinalIgnoreCase);
}
