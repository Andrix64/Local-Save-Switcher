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
    /// prefixOf dà il prefisso Proton (o Wine) del gioco, o "" se non ce n'è.
    /// Dopo il controllo, l'ordine dei giochi è quello di prima.
    /// </summary>
    public static IReadOnlyList<GameInfo> Apply(IReadOnlyList<GameInfo> games, IGameProvider provider,
        GameAccount account, KnownFolders folders, IReadOnlyList<string> launcherFolders, string lssDataRoot,
        Func<GameInfo, string> prefixOf)
    {
        var result = new List<GameInfo>(games.Count);

        foreach (GameInfo game in games)
        {
            string prefix = prefixOf(game);

            var context = new SaveContext(
                folders,
                prefix,
                PrefixIsPerGame: prefix != "",
                game.InstallPath,
                account.Id,
                IgnoreCase: OperatingSystem.IsLinux());

            // Cartella della scelta: AppData\LSS\<launcher>\<account>\<gioco> (vedi SaveManager.Open).
            var settings = new GameSaveSettings(Path.Combine(lssDataRoot, provider.Id, account.Id, game.Id));
            settings.Load();

            SaveLocation found = SaveLocator.Locate(
                game,
                provider.SaveRules(account, game),
                provider.RootOverrides(account, game),
                context,
                launcherFolders,
                lssDataRoot,
                settings.Folder,
                settings.Origin);

            if (found.Source != SaveSource.Userdata && found.Source != SaveSource.None)
                Log.Write($"Salvataggi di {game.Name} fuori da userdata: {found.Folder} ({found.Source})");

            result.Add(game with
            {
                SaveFolder = found.Folder,
                Source = found.Source,
                Support = found.Support,
                Reason = found.Reason,
                HasOtherSaves = found.HasOtherSaves,
            });
        }

        return result;
    }
}
