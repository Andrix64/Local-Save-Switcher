namespace Lss;

/// <summary>
/// Dove sono i salvataggi di ogni gioco di un launcher: per ogni gioco si
/// applica SaveLocator con le regole del launcher, la cartella scelta o
/// imparata e il prefisso. Usato da ogni provider alla fine di FindGames.
/// </summary>
internal static class SaveLookup
{
    /// <summary>
    /// Applica SaveLocator a ogni gioco e registra quelli trovati fuori da userdata.
    /// Dopo il controllo, l'ordine dei giochi è quello di prima.
    /// </summary>
    public static IReadOnlyList<GameInfo> Apply(IReadOnlyList<GameInfo> games, IGameProvider provider,
        GameAccount account, KnownFolders folders, string lssDataRoot)
    {
        IReadOnlyList<string> launchers = provider.LauncherFolders(account);
        var result = new List<GameInfo>(games.Count);

        foreach (GameInfo game in games)
        {
            // Posti scelti o imparati: <dati LSS>\<launcher>\<account>\<gioco> (vedi SaveManager.Open).
            var stored = new GameSpots(Path.Combine(lssDataRoot, provider.Id, account.Id, game.Id));
            stored.Load();

            SaveLocation found = SaveLocator.Locate(
                game,
                provider.SaveRules(account, game),
                provider.RootOverrides(account, game),
                ContextFor(provider, account, game, folders),
                launchers,
                lssDataRoot,
                stored);

            if (found.Source != SaveSource.Userdata && found.Source != SaveSource.None)
                Log.Write($"Salvataggi di {game.Name} fuori da userdata: {found.Folder} ({found.Source})");

            result.Add(game with
            {
                Spots = found.Spots,
                Source = found.Source,
                Support = found.Support,
                Reason = found.Reason,
            });
        }

        return result;
    }

    /// <summary>
    /// Contesto per trasformare le regole del gioco in cartelle: cartelle
    /// dell'utente, prefisso, cartella di installazione, account.
    /// </summary>
    /// <summary>
    /// Percorso portabile di una cartella per questo gioco, come nei posti (vedi SavePathResolver.ToPortable).
    /// </summary>
    public static string PortableFolder(IGameProvider provider, GameAccount account, GameInfo game, string folder) =>
        SavePathResolver.ToPortable(folder, ContextFor(provider, account, game, Platform.Current.GetKnownFolders()));

    public static SaveContext ContextFor(IGameProvider provider, GameAccount account, GameInfo game,
        KnownFolders folders)
    {
        string prefix = provider.PrefixOf(game);

        return new SaveContext(
            folders,
            prefix,
            PrefixIsPerGame: prefix != "",
            game.InstallPath,
            account.Id,
            IgnoreCase: OperatingSystem.IsLinux());
    }
}
