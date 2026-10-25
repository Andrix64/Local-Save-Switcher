namespace Lss;

/// <summary>
/// I posti dei salvataggi di un gioco trovati, con la fonte e il motivo se non si usano.
/// Reason: per il log in italiano; per Unsafe è il nome del problema (SafetyProblem).
/// </summary>
public sealed record SaveLocation(IReadOnlyList<SaveSpot> Spots, SaveSource Source, SupportState Support, string Reason)
{
    /// <summary>Cartella del primo posto, o "" se non ce n'è.</summary>
    public string Folder => Spots.Count > 0 ? Spots[0].Folder : "";
}

/// <summary>
/// Trova i posti dei salvataggi di un gioco. Il posto 1 è la cartella scelta o
/// imparata (se c'è: vince, come prima), altrimenti quella di Steam in userdata,
/// altrimenti la prima trovata dalle regole del launcher. Gli altri posti trovati
/// dalle regole e quelli aggiunti a mano (posti.ini) vanno dopo.
/// </summary>
internal static class SaveLocator
{
    /// <summary>Più posti di così LSS non li cambia insieme in modo sicuro.</summary>
    public const int MaxSpots = 8;

    /// <summary>
    /// Posti del gioco. stored sono i posti salvati in posti.ini (scelti o imparati);
    /// i posti trovati di un launcher ricevono un id da stored (LauncherId).
    /// </summary>
    public static SaveLocation Locate(GameInfo game, IReadOnlyList<SaveRule> rules,
        IReadOnlyList<RootOverride> overrides, SaveContext context, IReadOnlyList<string> launcherFolders,
        string lssDataRoot, GameSpots stored)
    {
        SaveSpot? chosen = stored.Spots.FirstOrDefault(spot => spot.Id == 1);
        var chosenExtras = stored.Spots.Where(spot => spot.Id != 1).ToList();

        // Cartella scelta a mano o imparata: vince sulle regole. Se non passa il controllo, Unsafe senza provare le altre.
        if (chosen is not null)
        {
            SafetyProblem problem = CheckSpot(chosen, context, launcherFolders, lssDataRoot);

            if (problem != SafetyProblem.None)
                return new SaveLocation([chosen], chosen.Source, SupportState.Unsafe, problem.ToString());
        }

        (List<SaveSpot> found, bool tooWide) = FoundByRules(rules, overrides, context);
        bool userdata = game.Source == SaveSource.Userdata && game.Support == SupportState.Supported
                        && game.SaveFolder != "";

        if (tooWide)
        {
            // Una maschera "*" senza ricorsione non è un posto: se la cartella di Steam o quella scelta bastano, si ignora.
            if (chosen is null && !userdata)
                return new SaveLocation([], SaveSource.None, SupportState.Unsure, "più cartelle o file sparsi");

            Log.Write("Maschera troppo larga ignorata per " + game.Name);
        }

        SaveSpot? primary = chosen;

        if (primary is null && userdata)
        {
            primary = new SaveSpot(1, game.SaveFolder, "", true,
                SavePathResolver.ToPortable(game.SaveFolder, context), SaveSource.Userdata);
        }
        else if (primary is null && found.Count > 0)
        {
            primary = found[0] with { Id = 1 };
            found = found.Skip(1).ToList();
        }
        else if (primary is not null && found.Count > 0)
        {
            Log.Write($"Cartelle trovate di {game.Name} sostituite dalla cartella scelta: {primary.Folder}");
            found = [];
        }

        if (primary is null)
        {
            if (rules.Count == 0)
                return new SaveLocation(game.Spots, game.Source, game.Support, game.Reason);

            return new SaveLocation([], SaveSource.None, SupportState.NotFound,
                "nessuna cartella trovata con le regole del launcher");
        }

        // Posti aggiunti a mano prima di quelli trovati dalle regole. Ogni posto si controlla, anche quelli che poi si sovrappongono.
        List<SaveSpot> candidates = chosenExtras.Concat(found).ToList();
        var dropped = new List<SaveSpot>();

        foreach (SaveSpot spot in candidates.Prepend(primary).Where(spot => spot.Source != SaveSource.Userdata))
        {
            SafetyProblem problem = CheckSpot(spot, context, launcherFolders, lssDataRoot);

            if (problem == SafetyProblem.None)
                continue;

            // Un gioco gestito con la userdata (come nella 1.0) resta gestito: una cartella
            // in più delle regole che non passa il controllo si lascia fuori, non blocca il gioco.
            if (primary.Source == SaveSource.Userdata && spot.Source == SaveSource.Launcher)
            {
                Log.Write($"Cartella delle regole di {game.Name} lasciata fuori ({problem}): {spot.Folder}");
                dropped.Add(spot);
                continue;
            }

            return new SaveLocation([spot], spot.Source, SupportState.Unsafe, problem.ToString());
        }

        candidates.RemoveAll(dropped.Contains);

        List<SaveSpot> kept = WithoutOverlaps(primary, candidates);

        if (1 + kept.Count > MaxSpots)
            return new SaveLocation([], SaveSource.None, SupportState.Unsure,
                "più di " + MaxSpots + " posti: troppi per LSS");

        var spots = new List<SaveSpot> { primary };

        foreach (SaveSpot spot in kept)
        {
            spots.Add(spot.Id == 0 ? spot with { Id = stored.LauncherId(KeyOf(spot)) } : spot);
        }

        string reason = primary.Source == SaveSource.Launcher ? "regola del launcher" : "";

        return new SaveLocation(spots.OrderBy(spot => spot.Id).ToList(), primary.Source, SupportState.Supported, reason);
    }

    /// <summary>
    /// Se un posto si può usare: il controllo di SaveFolderSafety, per cartella o per maschera.
    /// </summary>
    internal static SafetyProblem CheckSpot(SaveSpot spot, SaveContext context,
        IReadOnlyList<string> launcherFolders, string lssDataRoot) =>
        spot.IsWholeFolder
            ? SaveFolderSafety.Check(spot.Folder, context, launcherFolders, lssDataRoot)
            : SaveFolderSafety.CheckPattern(spot.Folder, spot.Pattern, spot.Recursive, context,
                launcherFolders, lssDataRoot);

    /// <summary>
    /// I posti che le regole trovano, senza doppioni. tooWide: una regola con "*" senza ricorsione.
    /// </summary>
    private static (List<SaveSpot> Spots, bool TooWide) FoundByRules(IReadOnlyList<SaveRule> rules,
        IReadOnlyList<RootOverride> overrides, SaveContext context)
    {
        var spots = new List<SaveSpot>();
        bool tooWide = false;

        foreach (SaveRule rule in rules)
        {
            (IReadOnlyList<SaveSpot> found, bool wide) = SavePathResolver.ResolveSpots(rule, overrides, context);
            tooWide |= wide;

            foreach (SaveSpot spot in found)
            {
                bool duplicate = spots.Any(other => SameFolder(other.Folder, spot.Folder)
                                                    && other.Pattern == spot.Pattern && other.Recursive == spot.Recursive);

                if (!duplicate)
                    spots.Add(spot);
            }
        }

        return (spots, tooWide);
    }

    /// <summary>
    /// Toglie i posti che si sovrappongono: uno dentro il posto principale, o che lo
    /// contiene, o che sta dentro una cartella intera già tenuta. Una cartella intera
    /// toglie quelli che stanno dentro. Il posto principale non si tocca mai.
    /// </summary>
    private static List<SaveSpot> WithoutOverlaps(SaveSpot primary, List<SaveSpot> candidates)
    {
        var kept = new List<SaveSpot>();

        foreach (SaveSpot candidate in candidates)
        {
            if (Overlaps(candidate, primary))
            {
                Log.Write("Posto scartato, si sovrappone al posto principale: " + candidate.Folder);
                continue;
            }

            if (kept.Any(other => other.IsWholeFolder && SameOrInside(candidate.Folder, other.Folder)))
            {
                Log.Write("Posto scartato, sta dentro una cartella già usata: " + candidate.Folder);
                continue;
            }

            if (candidate.IsWholeFolder)
                kept.RemoveAll(other => SameOrInside(other.Folder, candidate.Folder));

            kept.Add(candidate);
        }

        return kept;
    }

    private static bool Overlaps(SaveSpot candidate, SaveSpot primary) =>
        (primary.IsWholeFolder && SameOrInside(candidate.Folder, primary.Folder))
        || (candidate.IsWholeFolder && SameOrInside(primary.Folder, candidate.Folder));

    /// <summary>
    /// Chiave con cui LauncherId ricorda l'id di un posto trovato dalle regole.
    /// </summary>
    private static string KeyOf(SaveSpot spot) =>
        spot.PortablePath + "|" + spot.Pattern + "|" + (spot.Recursive ? "1" : "0");

    private static bool SameFolder(string first, string second) =>
        string.Equals(first, second, FolderComparison());

    /// <summary>
    /// true se folder è uguale a root o sta dentro root.
    /// </summary>
    private static bool SameOrInside(string folder, string root)
    {
        string trimmed = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        return string.Equals(folder, trimmed, FolderComparison())
               || folder.StartsWith(trimmed + Path.DirectorySeparatorChar, FolderComparison());
    }

    private static StringComparison FolderComparison() =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
}
