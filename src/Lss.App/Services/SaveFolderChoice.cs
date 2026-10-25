namespace Lss.App.Services;

/// <summary>
/// Cartella dei salvataggi scelta a mano: si chiede la cartella, si controlla
/// che sia sicura, si chiede conferma e si salva in posti.ini. Lo usano le
/// schede di Altri giochi e, nelle fasi M, N e P, gli altri percorsi.
/// </summary>
internal static class SaveFolderChoice
{
    /// <summary>
    /// Chiede la cartella e, se passa il controllo e l'utente conferma, la
    /// salva. true se è stata salvata.
    /// </summary>
    public static async Task<bool> ChooseAsync(IDialogService dialogs, GameInfo game, GameAccount account,
        IGameProvider provider)
    {
        string? folder = await dialogs.PickFolderAsync(
            Loc.F(Strings.ChooseSaveFolderTitle, game.Name),
            StartFolder(game, account, provider));

        return folder != null && await ConfirmAndSaveAsync(dialogs, folder, game, account, provider);
    }

    /// <summary>
    /// Controllo di sicurezza, conferma e salvataggio di una cartella già
    /// scelta. true se è stata salvata.
    /// </summary>
    public static async Task<bool> ConfirmAndSaveAsync(IDialogService dialogs, string folder, GameInfo game,
        GameAccount account, IGameProvider provider, SaveSource origin = SaveSource.Chosen)
    {
        SafetyProblem problem = SaveFolderSafety.Check(
            folder,
            SaveLookup.ContextFor(provider, account, game, Platform.Current.GetKnownFolders()),
            provider.LauncherFolders(account),
            AppData.Root);

        if (problem != SafetyProblem.None)
        {
            await dialogs.InformAsync(
                Loc.F(Strings.ChooseSaveFolderTitle, game.Name),
                Loc.F(Strings.SupportUnsafe, SaveFolderSafety.Describe(problem)),
                InfoKind.Warning);

            return false;
        }

        string dataFolder = DataFolder(game, account, provider);

        // Con partite fatte nella cartella di prima, la cambiano: si avvisa, invece della conferma normale.
        bool changed = FolderChanged(dataFolder, game, folder);

        bool confirmed = await dialogs.ConfirmAsync(
            Loc.F(Strings.ChooseSaveFolderTitle, game.Name),
            changed ? Loc.F(Strings.LearnFolderChanged, game.Name) : Loc.F(Strings.UseThisFolderText, folder, game.Name),
            Strings.UseThisFolder,
            kind: InfoKind.Warning);

        if (!confirmed)
            return false;

        new GameSaveSettings(dataFolder) { Folder = folder, Origin = origin }.Save();
        return true;
    }

    /// <summary>
    /// true se il gioco ha partite e la cartella nuova è diversa da quella attuale.
    /// </summary>
    private static bool FolderChanged(string dataFolder, GameInfo game, string folder)
    {
        var settings = new GameSaveSettings(dataFolder);
        settings.Load();

        string current = settings.Folder != "" ? settings.Folder : game.SaveFolder;

        return current != ""
            && !string.Equals(Path.GetFullPath(current), Path.GetFullPath(folder), StringComparison.OrdinalIgnoreCase)
            && HasPlaythroughs(dataFolder);
    }

    /// <summary>
    /// true se nella cartella dei dati c'è almeno una partita (Profili\Profilo_...).
    /// </summary>
    internal static bool HasPlaythroughs(string dataFolder)
    {
        string profiles = Path.Combine(dataFolder, "Profili");

        try
        {
            return Directory.Exists(profiles) && Directory.EnumerateDirectories(profiles, "Profilo_*").Any();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Write("Partite non lette: " + profiles, ex);
            return false;
        }
    }

    /// <summary>
    /// Cartella dei dati LSS del gioco (dove sta posti.ini), come in SaveLookup.
    /// </summary>
    public static string DataFolder(GameInfo game, GameAccount account, IGameProvider provider) =>
        Path.Combine(AppData.Root, provider.Id, account.Id, game.Id);

    /// <summary>
    /// Da dove parte il selettore: la cartella utente del prefisso se c'è, altrimenti Documents.
    /// </summary>
    internal static string StartFolder(GameInfo game, GameAccount account, IGameProvider provider)
    {
        SaveContext context = SaveLookup.ContextFor(provider, account, game, Platform.Current.GetKnownFolders());
        string user = SavePathResolver.PrefixUser(context);

        if (user != "")
            return user;

        return context.Folders.Documents != "" ? context.Folders.Documents : context.Folders.Home;
    }
}
