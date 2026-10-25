namespace Lss.App.Services;

/// <summary>
/// Cartella dei salvataggi scelta a mano: si chiede la cartella, si controlla
/// che sia sicura, si chiede conferma e si salva in cartella.ini. Lo usano le
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
        GameAccount account, IGameProvider provider)
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

        bool confirmed = await dialogs.ConfirmAsync(
            Loc.F(Strings.ChooseSaveFolderTitle, game.Name),
            Loc.F(Strings.UseThisFolderText, folder, game.Name),
            Strings.UseThisFolder,
            kind: InfoKind.Warning);

        if (!confirmed)
            return false;

        new GameSaveSettings(DataFolder(game, account, provider)) { Folder = folder, Origin = SaveSource.Chosen }.Save();
        return true;
    }

    /// <summary>
    /// Cartella dei dati LSS del gioco (dove sta cartella.ini), come in SaveLookup.
    /// </summary>
    public static string DataFolder(GameInfo game, GameAccount account, IGameProvider provider) =>
        Path.Combine(AppData.Root, provider.Id, account.Id, game.Id);

    /// <summary>
    /// Da dove parte il selettore: la cartella utente del prefisso se c'è, altrimenti Documents.
    /// </summary>
    private static string StartFolder(GameInfo game, GameAccount account, IGameProvider provider)
    {
        SaveContext context = SaveLookup.ContextFor(provider, account, game, Platform.Current.GetKnownFolders());
        string user = SavePathResolver.PrefixUser(context);

        if (user != "")
            return user;

        return context.Folders.Documents != "" ? context.Folders.Documents : context.Folders.Home;
    }
}
