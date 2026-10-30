namespace Lss.App.Services;

/// <summary>
/// Risultato di una ricerca: le cartelle proposte, e se la lettura si è fermata
/// prima della fine (tempo finito o annullata).
/// </summary>
/// <param name="Candidates">Cartelle proposte, la migliore per prima.</param>
/// <param name="Partial">true se la lettura non è completa.</param>
public sealed record LearnResult(IReadOnlyList<SaveCandidate> Candidates, bool Partial);

/// <summary>
/// Ricerca dei salvataggi di un gioco sul disco (fase M): i file cambiati dopo
/// l'avvio del gioco nelle cartelle di ricerca, poi la classifica. Legge soltanto.
/// </summary>
internal static class LearnScanner
{
    // Oltre questo tempo la lettura si ferma e si usa quello che è stato trovato.
    private static readonly TimeSpan TimeLimit = TimeSpan.FromSeconds(90);

    /// <summary>
    /// Cerca i file cambiati dopo since. Si può annullare con cancel.
    /// </summary>
    public static Task<LearnResult> ScanAsync(GameInfo game, GameAccount account, IGameProvider provider,
        DateTime since, CancellationToken cancel) =>
        Task.Run(() => Scan(game, account, provider, since, cancel));

    private static LearnResult Scan(GameInfo game, GameAccount account, IGameProvider provider, DateTime since,
        CancellationToken cancel)
    {
        SaveContext context = SaveLookup.ContextFor(provider, account, game, Platform.Current.GetKnownFolders());
        string dataFolder = SaveFolderChoice.DataFolder(game, account, provider);
        var settings = new GameSaveSettings(dataFolder);
        var ignored = new IgnoredFolders(AppData.Root);
        IReadOnlyList<string> launchers = provider.LauncherFolders(account);
        string userdata = game.Source == SaveSource.Userdata ? game.SaveFolder : "";
        IReadOnlyList<string> ignoredFolders = ignored.Folders(dataFolder, context);

        // LSS e le cartelle dei launcher non si leggono: la userdata del gioco e la sua installazione sì.
        var skip = new List<string>(ignoredFolders) { AppData.Root };
        skip.AddRange(launchers.Where(folder => !SameFolder(folder, userdata) && !SameFolder(folder, game.InstallPath)));

        IReadOnlyList<SearchRoot> roots = SaveFinder.Roots(context, settings.ExtraFolders, userdata);
        IReadOnlyList<ChangedFile> files = SaveFinder.FindChanged(roots, since, skip, TimeLimit, cancel, out bool partial);
        IReadOnlyList<string> known = game.SaveFolder != "" ? [game.SaveFolder] : [];
        IReadOnlyList<SaveCandidate> candidates = SaveFinder.Rank(files, context, game.Name, launchers, AppData.Root,
            known, ignoredFolders);

        return new LearnResult(candidates, partial);
    }

    private static bool SameFolder(string folder, string other)
    {
        if (folder == "" || other == "")
            return false;

        StringComparison comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        return string.Equals(folder.TrimEnd(Path.DirectorySeparatorChar), other.TrimEnd(Path.DirectorySeparatorChar), comparison);
    }
}
