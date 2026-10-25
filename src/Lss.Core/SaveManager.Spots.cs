namespace Lss;

/// <summary>
/// Che cosa hanno le altre partite in un posto nuovo.
/// </summary>
public enum NewSpotFill
{
    /// <summary>Una copia dei file che ci sono ora.</summary>
    CopyCurrent,

    /// <summary>Un posto vuoto.</summary>
    Empty,
}

/// <summary>
/// Aggiungere e togliere i posti di un gioco che ha partite (N7).
/// </summary>
public sealed partial class SaveManager
{
    // Nome della cartella di backup prima di aggiungere un posto (vedi BackupReasonText).
    private const string NewSpotBackupName = "Prima_nuovo_posto";

    /// <summary>
    /// Aggiunge un posto ai posti del gioco. I file che ci sono ora appartengono alla
    /// partita attiva; per le altre partite: una copia dei file attuali o un posto
    /// vuoto. L'id lo dà LSS. Con il gioco aperto non si fa.
    /// </summary>
    public Result AddSpot(SaveSpot spot, NewSpotFill others)
    {
        return Finish("Aggiunge posto " + spot.Folder, AddSpotCore(spot, others));
    }

    private Result AddSpotCore(SaveSpot spot, NewSpotFill others)
    {
        if (IsGameRunning())
            return Result.Fail(Loc.F(Strings.CloseGameFirst, Game.Name));

        SaveContext context = SaveLookup.ContextFor(provider, Account, Game, Platform.Current.GetKnownFolders());
        SafetyProblem problem = SaveLocator.CheckSpot(spot, context, provider.LauncherFolders(Account), AppData.Root);

        if (problem != SafetyProblem.None)
            return Result.Fail(Loc.F(Strings.SupportUnsafe, SaveFolderSafety.Describe(problem)));

        string backup = NewBackupPath(NewSpotBackupName);

        try
        {
            new SaveSet([spot with { Id = 1 }]).CopyGameTo(backup);
            WriteBackupInfo(backup, HasActiveProfile ? State.ActiveProfile : "", BackupKind.NewSpot, "");
        }
        catch (Exception ex)
        {
            return Result.Fail(Loc.F(Strings.BackupFailedCancelled, ex.Message));
        }

        var stored = new GameSpots(dataFolder);
        stored.Load();
        stored.ReserveIdsAbove(Game.Spots.Select(existing => existing.Id).DefaultIfEmpty(0).Max());

        SaveSpot added;

        try
        {
            added = stored.Add(spot.Folder, spot.Pattern, spot.PortablePath, spot.Source, spot.Recursive);
        }
        catch (ArgumentException ex)
        {
            return Result.Fail(Loc.F(Strings.SpotBadName, ex.Message));
        }

        var made = new List<string>();

        try
        {
            CopyNewSpotToPlaythroughs(added, others, made);

            if (!stored.Save())
                throw new IOException("posti.ini non scritto");
        }
        catch (Exception ex)
        {
            RemoveNewSpotFolders(made);
            return Result.Fail(Loc.F(Strings.ErrorWithMessage, ex.Message));
        }

        Game = Game with { Spots = Game.Spots.Append(added).OrderBy(existing => existing.Id).ToList() };
        saves = new SaveSet(Game.Spots);

        return Result.Ok(Strings.SpotAdded);
    }

    /// <summary>
    /// Copia il posto nuovo nella partita attiva e, per le altre, come scelto. Le cartelle
    /// fatte finiscono in made, così si possono togliere se qualcosa va storto.
    /// </summary>
    private void CopyNewSpotToPlaythroughs(SaveSpot added, NewSpotFill others, List<string> made)
    {
        var placed = new SaveSet([added]);

        foreach (string name in GetProfiles())
        {
            string profile = Path.Combine(ProfilesPath, name);
            bool active = HasActiveProfile && name == State.ActiveProfile;
            string folder = Path.Combine(profile, FileOps.ExtraSpotsFolder, added.Id.ToString(System.Globalization.CultureInfo.InvariantCulture));

            made.Add(folder);

            if (active || others == NewSpotFill.CopyCurrent)
            {
                placed.CopyGameTo(profile);
            }
            else
            {
                Directory.CreateDirectory(folder);
            }

        }
    }

    /// <summary>
    /// Toglie le cartelle di un posto nuovo che non si è riusciti a completare.
    /// </summary>
    private static void RemoveNewSpotFolders(IEnumerable<string> folders)
    {
        foreach (string folder in folders)
        {
            TryDeleteFolder(folder);

            string parent = Path.GetDirectoryName(folder) ?? "";

            if (Directory.Exists(parent) && !Directory.EnumerateFileSystemEntries(parent).Any())
                TryDeleteFolder(parent);
        }
    }

    /// <summary>
    /// Toglie un posto aggiunto a mano. Le copie nelle partite restano: se la cartella
    /// si aggiunge di nuovo, è un posto nuovo.
    /// </summary>
    public Result RemoveSpot(int id)
    {
        return Finish("Toglie posto " + id, RemoveSpotCore(id));
    }

    private Result RemoveSpotCore(int id)
    {
        var stored = new GameSpots(dataFolder);
        stored.Load();

        if (stored.Spots.All(spot => spot.Id != id))
            return Result.Fail(Strings.SpotNotFound);

        stored.Remove(id);

        if (!stored.Save())
            return Result.Fail(Loc.F(Strings.ErrorWithMessage, "posti.ini non scritto"));

        Game = Game with { Spots = Game.Spots.Where(spot => spot.Id != id).ToList() };
        saves = new SaveSet(Game.Spots);

        Log.Write("Posto " + id + " tolto da " + Game.Name + ": le copie nelle partite restano.");

        return Result.Ok(Strings.SpotRemoved);
    }

    /// <summary>
    /// Percorso portabile di una cartella di questo gioco (per un posto nuovo: vedi GameSpots).
    /// </summary>
    public string PortableFolder(string folder) =>
        SavePathResolver.ToPortable(folder, SaveLookup.ContextFor(provider, Account, Game, Platform.Current.GetKnownFolders()));

    /// <summary>
    /// File JSON della configurazione dei salvataggi di questo gioco, da condividere.
    /// skipped: i posti che non stanno sotto una radice standard (non esportati).
    /// </summary>
    public string ExportConfigJson(string notes, out IReadOnlyList<string> skipped)
    {
        SaveContext context = SaveLookup.ContextFor(provider, Account, Game, Platform.Current.GetKnownFolders());
        (SaveConfigGame config, IReadOnlyList<string> notExported) = SaveConfig.FromGame(Game, context, notes);

        skipped = notExported;

        return SaveConfig.Write(SaveConfig.CurrentFile([config]));
    }
}
