namespace Lss;

/// <summary>Stato di un posto di una configurazione importata, su questo computer.</summary>
public enum ConfigSpotState
{
    /// <summary>La cartella c'è e passa il controllo di sicurezza.</summary>
    Found,

    /// <summary>La cartella non c'è ancora: sarà usata quando il gioco la crea.</summary>
    NotYet,

    /// <summary>La cartella c'è ma non si può usare (il controllo di sicurezza).</summary>
    Refused,

    /// <summary>La radice non esiste su questo sistema (per esempio Windows su Linux senza prefisso).</summary>
    NotForThisSystem,
}

/// <summary>Un posto della configurazione, con la cartella trovata qui e il suo stato.</summary>
public sealed record ConfigSpotPlan(SaveConfigSpot Spot, string Folder, ConfigSpotState State, string Reason);

/// <summary>
/// Un gioco della configurazione abbinato a un gioco di questo computer (Game è null se non c'è).
/// WindowsInPrefix: il file è di Windows e il gioco sta in un prefisso Proton/Wine.
/// </summary>
public sealed record ConfigGamePlan(SaveConfigGame Config, GameInfo? Game, IReadOnlyList<ConfigSpotPlan> Spots,
    bool WindowsInPrefix);

/// <summary>
/// Importazione di una configurazione dei salvataggi (N9b): abbina i giochi,
/// risolve i posti in cartelle di questo computer e li controlla come ogni altra
/// cartella. Plan non scrive nulla; Apply scrive solo quello che l'utente conferma.
/// </summary>
public static class SaveConfigImport
{
    /// <summary>
    /// Abbina ogni gioco del file a uno di questo computer e ne decide i posti.
    /// contextOf e launchersOf danno il contesto e le cartelle dei launcher del gioco.
    /// </summary>
    public static IReadOnlyList<ConfigGamePlan> Plan(SaveConfigFile file, IReadOnlyList<GameInfo> games,
        Func<GameInfo, SaveContext> contextOf, Func<GameInfo, IReadOnlyList<string>> launchersOf, string lssDataRoot)
    {
        var plans = new List<ConfigGamePlan>();

        foreach (SaveConfigGame config in file.Games)
        {
            GameInfo? game = Match(config, games);

            if (game == null)
            {
                plans.Add(new ConfigGamePlan(config, null, [], false));
                continue;
            }

            SaveContext context = contextOf(game);
            IReadOnlyList<string> launchers = launchersOf(game);

            var spots = config.Spots
                .Select(spot => PlanSpot(spot, context, launchers, lssDataRoot))
                .ToList();

            bool inPrefix = file.Os == "windows" && context.PrefixPath != "";

            plans.Add(new ConfigGamePlan(config, game, spots, inPrefix));
        }

        return plans;
    }

    /// <summary>
    /// Scrive i posti da importare (trovati o non ancora qui) nei posti del gioco, con
    /// origine importata. Con partite fatte ogni posto passa da addWithPlaythroughs
    /// (AddSpot: copia o vuoto). Ritorna quanti posti sono stati aggiunti.
    /// </summary>
    public static int Apply(ConfigGamePlan plan, string dataFolder, bool hasPlaythroughs,
        Func<SaveSpot, Result>? addWithPlaythroughs)
    {
        if (plan.Game == null)
            return 0;

        var stored = new GameSpots(dataFolder);
        stored.Load();

        int added = 0;

        foreach (ConfigSpotPlan spot in plan.Spots.Where(spot => spot.State is ConfigSpotState.Found or ConfigSpotState.NotYet))
        {
            string portable = Portable(spot.Spot);

            if (plan.Game.Spots.Any(existing => SameFolder(existing.Folder, spot.Folder)
                                                && existing.Pattern == spot.Spot.Pattern))
                continue;

            if (hasPlaythroughs)
            {
                if (addWithPlaythroughs == null)
                    continue;

                var candidate = new SaveSpot(0, spot.Folder, spot.Spot.Pattern, spot.Spot.Recursive, portable,
                    SaveSource.Imported);

                if (addWithPlaythroughs(candidate).Success)
                    added++;

                continue;
            }

            try
            {
                stored.Add(spot.Folder, spot.Spot.Pattern, portable, SaveSource.Imported, spot.Spot.Recursive);
                added++;
            }
            catch (ArgumentException ex)
            {
                Log.Write("Posto importato non aggiunto: " + spot.Folder, ex);
            }
        }

        if (!hasPlaythroughs && added > 0)
            stored.Save();

        return added;
    }

    private static GameInfo? Match(SaveConfigGame config, IReadOnlyList<GameInfo> games)
    {
        if (config.Id != "")
        {
            GameInfo? byId = games.FirstOrDefault(game => game.Provider == config.Provider && game.Id == config.Id);

            if (byId != null)
                return byId;
        }

        string name = SaveFinder.Normalize(config.Name);

        return games.FirstOrDefault(game =>
            (name != "" && SaveFinder.Normalize(game.Name) == name)
            || (config.InstallDir != "" && game.InstallDir == config.InstallDir));
    }

    private static ConfigSpotPlan PlanSpot(SaveConfigSpot spot, SaveContext context,
        IReadOnlyList<string> launchers, string lssDataRoot)
    {
        string? folder = SavePathResolver.FromPortable(Portable(spot), context);

        if (string.IsNullOrEmpty(folder))
            return new ConfigSpotPlan(spot, "", ConfigSpotState.NotForThisSystem, "");

        if (spot.Pattern == "" && !Directory.Exists(folder))
            return new ConfigSpotPlan(spot, folder, ConfigSpotState.NotYet, "");

        var candidate = new SaveSpot(0, folder, spot.Pattern, spot.Recursive, Portable(spot), SaveSource.Imported);
        SafetyProblem problem = SaveLocator.CheckSpot(candidate, context, launchers, lssDataRoot);

        return problem switch
        {
            SafetyProblem.None => new ConfigSpotPlan(spot, folder, ConfigSpotState.Found, ""),
            SafetyProblem.Missing or SafetyProblem.Empty => new ConfigSpotPlan(spot, folder, ConfigSpotState.NotYet, ""),
            _ => new ConfigSpotPlan(spot, folder, ConfigSpotState.Refused, SaveFolderSafety.Describe(problem)),
        };
    }

    /// <summary>Il percorso portabile del posto: {radice}/percorso.</summary>
    private static string Portable(SaveConfigSpot spot) =>
        "{" + spot.Root + "}" + (spot.Path == "" ? "" : "/" + spot.Path);

    private static bool SameFolder(string first, string second) =>
        string.Equals(first, second, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
}
