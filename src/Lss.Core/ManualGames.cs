namespace Lss;

/// <summary>
/// Un gioco aggiunto a mano: nome, eseguibile (opzionale), argomenti e prefisso
/// Wine o Proton (solo Linux, opzionale).
/// </summary>
public sealed record ManualGame(string Name, string Exe, string Arguments, string Prefix);

/// <summary>
/// I giochi aggiunti a mano, in &lt;dati LSS&gt;\manual\local\&lt;id&gt;\gioco.ini
/// (una cartella per gioco, come quelle dei giochi dei launcher).
/// </summary>
internal static class ManualGames
{
    // Cartelle dei giochi eliminati, così si possono ripristinare a mano.
    private const string RemovedFolder = "eliminati";

    /// <summary>Cartella dei giochi aggiunti a mano (account "local").</summary>
    public static string AccountFolder(string dataRoot) => Path.Combine(dataRoot, "manual", "local");

    /// <summary>
    /// Aggiunge un gioco con un id nuovo (8 cifre esadecimali minuscole) e ne restituisce l'id.
    /// </summary>
    public static string Add(string dataRoot, ManualGame game)
    {
        string id = Guid.NewGuid().ToString("N")[..8];
        Write(dataRoot, id, game);

        return id;
    }

    /// <summary>
    /// Scrive i dati di un gioco (gioco.ini), creando la cartella se manca.
    /// </summary>
    public static void Write(string dataRoot, string id, ManualGame game)
    {
        IniFile.Write(Path.Combine(AccountFolder(dataRoot), id, "gioco.ini"),
        [
            new KeyValuePair<string, string>("Nome", game.Name),
            new KeyValuePair<string, string>("Eseguibile", game.Exe),
            new KeyValuePair<string, string>("Argomenti", game.Arguments),
            new KeyValuePair<string, string>("Prefisso", game.Prefix),
        ]);
    }

    /// <summary>
    /// I giochi aggiunti a mano, con il loro id. Salta le cartelle senza un gioco leggibile.
    /// </summary>
    public static IReadOnlyList<(string Id, ManualGame Game)> ReadAll(string dataRoot)
    {
        var games = new List<(string, ManualGame)>();
        string folder = AccountFolder(dataRoot);

        if (!Directory.Exists(folder))
            return games;

        foreach (string dir in Directory.GetDirectories(folder).OrderBy(dir => dir, StringComparer.Ordinal))
        {
            Dictionary<string, string> values = IniFile.Read(Path.Combine(dir, "gioco.ini"));

            if (!values.TryGetValue("Nome", out string? name) || name.Trim() == "")
                continue;

            games.Add((Path.GetFileName(dir), new ManualGame(
                name.Trim(),
                values.GetValueOrDefault("Eseguibile", "").Trim(),
                values.GetValueOrDefault("Argomenti", "").Trim(),
                values.GetValueOrDefault("Prefisso", "").Trim())));
        }

        return games;
    }

    /// <summary>
    /// Toglie un gioco da LSS: la sua cartella (con le partite) va in manual\eliminati,
    /// la cartella di salvataggio del gioco non si tocca.
    /// </summary>
    public static void Remove(string dataRoot, string id)
    {
        string source = Path.Combine(AccountFolder(dataRoot), id);
        string target = Path.Combine(dataRoot, "manual", RemovedFolder, id + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));

        if (!Directory.Exists(source))
            return;

        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        Directory.Move(source, target);
        Log.Write("Gioco aggiunto a mano tolto da LSS: " + id + " → " + target);
    }
}
