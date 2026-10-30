namespace Lss;

/// <summary>Domanda che la modalità semplice può fare prima di giocare.</summary>
public interface ISimpleQuestions
{
    /// <summary>
    /// Di chi sono i salvataggi già presenti nel gioco? null = non si sa:
    /// vanno da parte (al giocatore attuale, non preferiti).
    /// </summary>
    Task<Person?> WhoseSavesAsync(string gameName, IReadOnlyList<Person> people);
}

/// <summary>
/// Prepara un gioco per un giocatore nella modalità semplice: mette al loro
/// posto i salvataggi della sua partita preferita. Non avvia il gioco.
/// </summary>
internal static class SimplePlay
{
    // Le operazioni che copiano file girano su un thread a parte (Task.Run): sulla
    // finestra non devono bloccare. Le domande restano sul thread di chi chiama.

    /// <summary>Mette al loro posto i salvataggi preferiti del giocatore. Non avvia il gioco.</summary>
    public static async Task<Result> PrepareAsync(SaveManager manager, People people, Person player,
        ISimpleQuestions ask)
    {
        Result ready = await PrepareStartAsync(manager);

        if (!ready.Success)
            return ready;

        string favourite = manager.FavouriteOf(player.Id) ?? "";

        if (favourite != "")
            return await ActivateIfNeededAsync(manager, favourite);

        // Nessuna preferita: i salvataggi già nel gioco, se nessuna partita li ha, vanno a qualcuno.
        if (await Task.Run(() => manager.GetProfiles().Count == 0 && !manager.GameSavesAreEmpty()))
        {
            Person? owner = await ask.WhoseSavesAsync(manager.Game.Name, people.All);

            if (owner != null)
            {
                Result adopted = await Task.Run(() => manager.AdoptGameSaves(manager.UniqueName(owner.Name), owner.Id));

                if (!adopted.Success)
                    return adopted;

                // Il proprietario ha ora la sua partita, preferita: se è il giocatore, si gioca quella.
                if (owner.Id == player.Id)
                    return Result.Ok("");
            }
            else
            {
                // Di nessuno: resta del giocatore attuale, ma da parte (non è la sua preferita).
                Result kept = await Task.Run(() => manager.AdoptGameSaves(manager.UniqueName(Strings.SavesBeforeLss), player.Id));

                if (!kept.Success)
                    return kept;
            }
        }

        return await NewPlaythroughAsync(manager, player);
    }

    /// <summary>
    /// Come PrepareAsync, ma con una partita scelta dal menu "Le mie partite", che diventa la preferita.
    /// </summary>
    public static async Task<Result> PrepareWithAsync(SaveManager manager, Person player, string profileName)
    {
        Result ready = await PrepareStartAsync(manager);

        if (!ready.Success)
            return ready;

        Result chosen = await Task.Run(() => manager.SetFavourite(player.Id, profileName));

        if (!chosen.Success)
            return chosen;

        return await ActivateIfNeededAsync(manager, profileName);
    }

    /// <summary>Nuova partita vuota del giocatore, che diventa la preferita, messa al suo posto.</summary>
    public static async Task<Result> PrepareNewAsync(SaveManager manager, Person player)
    {
        Result ready = await PrepareStartAsync(manager);

        if (!ready.Success)
            return ready;

        return await NewPlaythroughAsync(manager, player);
    }

    /// <summary>Gioco fermo e progressi salvati nella partita in uso.</summary>
    private static Task<Result> PrepareStartAsync(SaveManager manager) =>
        Task.Run(() =>
        {
            if (manager.IsGameRunning())
                return Result.Fail(Loc.F(Strings.SimpleCloseGame, manager.Game.Name));

            if (manager.HasActiveProfile && manager.HasUnsavedProgress())
            {
                Result saved = manager.SaveProgress();

                if (!saved.Success)
                    return saved;
            }

            return Result.Ok("");
        });

    private static Task<Result> ActivateIfNeededAsync(SaveManager manager, string profileName)
    {
        if (manager.State.ActiveProfile == profileName)
            return Task.FromResult(Result.Ok(""));

        return Task.Run(() => manager.ActivateProfile(profileName));
    }

    /// <summary>
    /// Nuova partita vuota del giocatore: diventa la sua preferita e si attiva.
    /// </summary>
    private static Task<Result> NewPlaythroughAsync(SaveManager manager, Person player) =>
        Task.Run(() =>
        {
            string name = manager.UniqueName(player.Name);
            Result created = manager.CreateProfile(name, copyCurrentSaves: false, player.Id);

            if (!created.Success)
                return created;

            string profileName = SaveManager.ProfileFolderName(name);
            Result favourite = manager.SetFavourite(player.Id, profileName);

            if (!favourite.Success)
                return favourite;

            return manager.ActivateProfile(profileName);
        });
}
