namespace Lss;

/// <summary>
/// Lo stesso gioco in due launcher (per esempio Epic e Heroic su Windows, o un gioco
/// GOG aggiunto a Steam): si tiene il primo, nell'ordine di Providers.All, e il
/// secondo si lascia fuori con una riga di log. Il confronto è sulla cartella
/// d'installazione; un gioco senza cartella non si confronta.
/// </summary>
internal static class DuplicateGames
{
    /// <summary>
    /// Per ogni gioco, nell'ordine dato (launcher per launcher): true se si mostra.
    /// </summary>
    public static IReadOnlyList<bool> Keep(IReadOnlyList<GameInfo> games)
    {
        // Cartella d'installazione → launcher che l'ha per prima.
        var owners = new Dictionary<string, string>(StringComparer.Ordinal);
        var keep = new List<bool>(games.Count);

        foreach (GameInfo game in games)
        {
            if (game.InstallPath == "")
            {
                keep.Add(true);
                continue;
            }

            string key = Path.TrimEndingDirectorySeparator(game.InstallPath);

            if (OperatingSystem.IsWindows())
                key = key.ToLowerInvariant();

            if (!owners.TryGetValue(key, out string? owner))
            {
                owners[key] = game.Provider;
                keep.Add(true);
                continue;
            }

            // Lo stesso launcher può avere due voci con la stessa cartella: si tengono entrambe.
            if (owner == game.Provider)
            {
                keep.Add(true);
                continue;
            }

            Log.Write($"Stesso gioco in {owner} e in {game.Provider}: {game.Name} mostrato solo con {owner}");
            keep.Add(false);
        }

        return keep;
    }
}
