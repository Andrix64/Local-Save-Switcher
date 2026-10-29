using System.Globalization;
using System.Text.Json.Serialization;

namespace Lss;

/// <summary>Cartella segnata come "non di salvataggi", con l'ambito.</summary>
/// <param name="Folder">Cartella: assoluta per un gioco solo, percorso portabile per tutti i giochi.</param>
/// <param name="PortablePath">Come è salvata nel file (per tutti i giochi, la radice tra graffe).</param>
/// <param name="GameKey">Cartella dati del gioco per un gioco solo; "" per tutti i giochi.</param>
/// <param name="Added">Quando è stata segnata.</param>
public sealed record IgnoredFolder(string Folder, string PortablePath, string GameKey, DateTime Added);

/// <summary>Una cartella ignorata come è scritta in cartelle-ignorate.json.</summary>
internal sealed class IgnoredEntry
{
    [JsonPropertyName("Cartella")]
    public string Folder { get; set; } = "";

    /// <summary>Quando è stata segnata, in formato "O"; vuoto se non si sa.</summary>
    [JsonPropertyName("Aggiunta")]
    public string Added { get; set; } = "";
}

/// <summary>Come cartelle-ignorate.json è scritto.</summary>
internal sealed class IgnoredFile
{
    [JsonPropertyName("Cartelle")]
    public List<IgnoredEntry> Folders { get; set; } = [];
}

/// <summary>
/// Cartelle che la ricerca non propone mai più: per tutti i giochi
/// (cartelle-ignorate.json nella cartella dei dati)
/// o per un gioco solo (posti.json del gioco, parte "Ignorate").
/// Non lancia eccezioni: un file illeggibile vale come nessuna cartella.
/// </summary>
internal sealed class IgnoredFolders
{
    private const string FileName = "cartelle-ignorate.json";
    private const string GameFileName = "posti.json";

    private readonly string file;
    private readonly List<IgnoredFolder> global = [];

    public IgnoredFolders(string dataRoot)
    {
        file = Path.Combine(dataRoot, FileName);
    }

    /// <summary>Cartelle ignorate per tutti i giochi, come sono nel file (dopo Load).</summary>
    public IReadOnlyList<IgnoredFolder> Global => global;

    /// <summary>Legge cartelle-ignorate.json.</summary>
    public void Load()
    {
        global.Clear();
        global.AddRange(ReadGlobal());
    }

    /// <summary>Cartelle ignorate per un gioco solo, dalla sua cartella dei dati.</summary>
    public IReadOnlyList<IgnoredFolder> ForGame(string gameDataFolder)
    {
        return ReadGame(Path.Combine(gameDataFolder, GameFileName), gameDataFolder);
    }

    /// <summary>Segna una cartella per tutti i giochi, nel percorso portabile dato. Non ripete una voce già presente.</summary>
    public void AddGlobal(string portablePath)
    {
        Load();

        if (global.Any(entry => string.Equals(entry.PortablePath, portablePath, StringComparison.OrdinalIgnoreCase)))
            return;

        global.Add(new IgnoredFolder(portablePath, portablePath, "", DateTime.Now));
        WriteGlobal();
    }

    /// <summary>Segna una cartella per un gioco solo, nella sua cartella dei dati.</summary>
    public void AddForGame(string gameDataFolder, string folder)
    {
        string path = Path.Combine(gameDataFolder, GameFileName);
        List<IgnoredFolder> entries = ReadGame(path, gameDataFolder).ToList();

        if (entries.Any(entry => string.Equals(entry.Folder, folder, StringComparison.OrdinalIgnoreCase)))
            return;

        entries.Add(new IgnoredFolder(folder, folder, gameDataFolder, DateTime.Now));
        WriteGame(path, entries);
    }

    /// <summary>Toglie una cartella da quelle per tutti i giochi, con il suo percorso portabile.</summary>
    public void RemoveGlobal(string portablePath)
    {
        Load();
        global.RemoveAll(entry => string.Equals(entry.PortablePath, portablePath, StringComparison.OrdinalIgnoreCase));
        WriteGlobal();
    }

    /// <summary>Toglie una cartella da quelle di un gioco solo.</summary>
    public void RemoveForGame(string gameDataFolder, string folder)
    {
        string path = Path.Combine(gameDataFolder, GameFileName);
        List<IgnoredFolder> entries = ReadGame(path, gameDataFolder).ToList();
        entries.RemoveAll(entry => string.Equals(entry.Folder, folder, StringComparison.OrdinalIgnoreCase));
        WriteGame(path, entries);
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

    /// <summary>Le voci di cartelle-ignorate.json con le loro date.</summary>
    private List<IgnoredFolder> ReadGlobal()
    {
        var entries = new List<IgnoredFolder>();

        foreach (IgnoredEntry entry in JsonFile.Read<IgnoredFile>(file)?.Folders ?? [])
        {
            if ((entry.Folder ?? "") == "")
                continue;

            DateTime added = DateTime.MinValue;

            if (entry.Added != null)
                DateTime.TryParse(entry.Added, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out added);

            entries.Add(new IgnoredFolder(entry.Folder!, entry.Folder!, "", added));
        }

        return entries;
    }

    /// <summary>
    /// Scrive cartelle-ignorate.json con le voci di tutti i giochi. Senza voci elimina il file.
    /// Un errore si registra.
    /// </summary>
    private void WriteGlobal()
    {
        try
        {
            if (global.Count == 0)
            {
                if (File.Exists(file))
                    File.Delete(file);

                return;
            }

            JsonFile.Write(file, new IgnoredFile
            {
                Folders = global.Select(entry => new IgnoredEntry
                {
                    Folder = entry.PortablePath,
                    Added = entry.Added.ToString("O", CultureInfo.InvariantCulture),
                }).ToList(),
            });
        }
        catch (Exception ex)
        {
            Log.Write("Cartelle ignorate non salvate: " + file, ex);
        }
    }

    /// <summary>
    /// Le cartelle ignorate di un gioco, dalla parte "Ignorate" di posti.json, con le loro date.
    /// </summary>
    private static List<IgnoredFolder> ReadGame(string path, string gameKey)
    {
        var entries = new List<IgnoredFolder>();

        foreach (IgnoredEntry entry in SpotsFile.Read(path).Ignored)
        {
            if (entry.Folder == "")
                continue;

            DateTime added = DateTime.MinValue;
            DateTime.TryParse(entry.Added, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out added);

            entries.Add(new IgnoredFolder(entry.Folder, entry.Folder, gameKey, added));
        }

        return entries;
    }

    /// <summary>
    /// Scrive la parte "Ignorate" di posti.json; i posti restano. Senza cartelle e senza posti
    /// elimina il file. Un errore si registra.
    /// </summary>
    private static void WriteGame(string path, IReadOnlyList<IgnoredFolder> entries)
    {
        try
        {
            SpotsFile data = SpotsFile.Read(path);

            data.Ignored = entries.Select(entry => new IgnoredEntry
            {
                Folder = entry.PortablePath,
                Added = entry.Added.ToString("O", CultureInfo.InvariantCulture),
            }).ToList();

            SpotsFile.Write(path, data);
        }
        catch (Exception ex)
        {
            Log.Write("Cartelle ignorate non salvate: " + path, ex);
        }
    }
}
