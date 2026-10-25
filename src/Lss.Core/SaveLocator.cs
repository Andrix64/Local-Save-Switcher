namespace Lss;

/// <summary>
/// Cartella dei salvataggi trovata, o il motivo per cui manca.
/// Reason: per il log in italiano; per Unsafe è il nome del problema (SafetyProblem).
/// </summary>
public sealed record SaveLocation(string Folder, SaveSource Source, SupportState Support, string Reason,
    bool HasOtherSaves);

/// <summary>
/// Trova la cartella dei salvataggi di un gioco: prima quella scelta o
/// imparata, poi la cartella di Steam in userdata, poi le regole del launcher.
/// </summary>
internal static class SaveLocator
{
    /// <summary>
    /// Ordine: cartella scelta o imparata (vince sulle regole; se non passa il
    /// controllo, Unsafe senza provare le regole); userdata di Steam; regole
    /// del launcher (una cartella sola → supportata, altrimenti Unsure).
    /// chosenFolder "" = nessuna scelta; chosenSource Chosen o Learned.
    /// </summary>
    public static SaveLocation Locate(GameInfo game, IReadOnlyList<SaveRule> rules,
        IReadOnlyList<RootOverride> overrides, SaveContext context, IReadOnlyList<string> launcherFolders,
        string lssDataRoot, string chosenFolder, SaveSource chosenSource)
    {
        if (chosenFolder != "")
        {
            SafetyProblem problem = SaveFolderSafety.Check(chosenFolder, context, launcherFolders, lssDataRoot);

            return problem == SafetyProblem.None
                ? new SaveLocation(chosenFolder, chosenSource, SupportState.Supported, "", false)
                : new SaveLocation(chosenFolder, chosenSource, SupportState.Unsafe, problem.ToString(), false);
        }

        if (game.Source == SaveSource.Userdata && game.Support == SupportState.Supported)
        {
            bool others = rules.Any(rule => SavePathResolver.Resolve(rule, overrides, context).Count > 0);

            return new SaveLocation(game.SaveFolder, SaveSource.Userdata, SupportState.Supported, "", others);
        }

        if (rules.Count == 0)
            return new SaveLocation(game.SaveFolder, game.Source, game.Support, game.Reason, game.HasOtherSaves);

        List<string> found = OutermostOnly(rules.SelectMany(rule => SavePathResolver.Resolve(rule, overrides, context)));

        if (found.Count == 0)
        {
            return new SaveLocation(game.SaveFolder, SaveSource.None, SupportState.NotFound,
                "nessuna cartella trovata con le regole del launcher", false);
        }

        if (found.Count == 1 && Directory.Exists(found[0]))
        {
            SafetyProblem problem = SaveFolderSafety.Check(found[0], context, launcherFolders, lssDataRoot);

            return problem == SafetyProblem.None
                ? new SaveLocation(found[0], SaveSource.Launcher, SupportState.Supported, "regola del launcher", false)
                : new SaveLocation(found[0], SaveSource.Launcher, SupportState.Unsafe, problem.ToString(), false);
        }

        return new SaveLocation("", SaveSource.None, SupportState.Unsure, "più cartelle o file sparsi", false);
    }

    /// <summary>
    /// Percorsi senza doppioni, senza quelli che stanno dentro un altro
    /// (un file dentro una cartella trovata è già coperto dalla cartella).
    /// </summary>
    private static List<string> OutermostOnly(IEnumerable<string> paths)
    {
        List<string> all = paths.Distinct().OrderBy(path => path, StringComparer.Ordinal).ToList();

        return all.Where(path => !all.Any(other => other != path && IsInside(path, other))).ToList();
    }

    private static bool IsInside(string path, string folder)
    {
        string separator = Path.DirectorySeparatorChar.ToString();
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        return path.StartsWith(folder.TrimEnd(Path.DirectorySeparatorChar) + separator, comparison);
    }
}
