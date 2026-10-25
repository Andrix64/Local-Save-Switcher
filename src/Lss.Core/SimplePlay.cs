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
    /// <summary>Mette al loro posto i salvataggi preferiti del giocatore. Non avvia il gioco.</summary>
    public static async Task<Result> PrepareAsync(SaveManager manager, People people, Person player,
        ISimpleQuestions ask)
    {
        Result ready = PrepareStart(manager);

        if (!ready.Success)
            return ready;

        string favourite = manager.FavouriteOf(player.Id) ?? "";

        if (favourite != "")
            return ActivateIfNeeded(manager, favourite);

        // Nessuna preferita: i salvataggi già nel gioco, se nessuna partita li ha, vanno a qualcuno.
        if (manager.GetProfiles().Count == 0 && !manager.GameSavesAreEmpty())
        {
            Person? owner = await ask.WhoseSavesAsync(manager.Game.Name, people.All);

            if (owner != null)
            {
                Result adopted = manager.AdoptGameSaves(manager.UniqueName(owner.Name), owner.Id);

                if (!adopted.Success)
                    return adopted;

                // Il proprietario ha ora la sua partita, preferita: se è il giocatore, si gioca quella.
                if (owner.Id == player.Id)
                    return Result.Ok("");
            }
            else
            {
                // Di nessuno: resta del giocatore attuale, ma da parte (non è la sua preferita).
                Result kept = manager.AdoptGameSaves(manager.UniqueName(Strings.SavesBeforeLss), player.Id);

                if (!kept.Success)
                    return kept;
            }
        }

        return await NewPlaythroughAsync(manager, player);
    }

    /// <summary>
    /// Come PrepareAsync, ma con una partita scelta dal menu "Le mie partite", che diventa la preferita.
    /// </summary>
    public static Task<Result> PrepareWithAsync(SaveManager manager, Person player, string profileName)
    {
        Result ready = PrepareStart(manager);

        if (!ready.Success)
            return Task.FromResult(ready);

        Result chosen = manager.SetFavourite(player.Id, profileName);

        if (!chosen.Success)
            return Task.FromResult(chosen);

        return Task.FromResult(ActivateIfNeeded(manager, profileName));
    }

    /// <summary>Nuova partita vuota del giocatore, che diventa la preferita, messa al suo posto.</summary>
    public static Task<Result> PrepareNewAsync(SaveManager manager, Person player)
    {
        Result ready = PrepareStart(manager);

        if (!ready.Success)
            return Task.FromResult(ready);

        return NewPlaythroughAsync(manager, player);
    }

    /// <summary>Gioco fermo e progressi salvati nella partita in uso.</summary>
    private static Result PrepareStart(SaveManager manager)
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
    }

    private static Result ActivateIfNeeded(SaveManager manager, string profileName)
    {
        if (manager.State.ActiveProfile == profileName)
            return Result.Ok("");

        return manager.ActivateProfile(profileName);
    }

    /// <summary>
    /// Nuova partita vuota del giocatore: diventa la sua preferita e si attiva.
    /// </summary>
    private static Task<Result> NewPlaythroughAsync(SaveManager manager, Person player)
    {
        string name = manager.UniqueName(player.Name);
        Result created = manager.CreateProfile(name, copyCurrentSaves: false, player.Id);

        if (!created.Success)
            return Task.FromResult(created);

        string profileName = SaveManager.ProfileFolderName(name);
        Result favourite = manager.SetFavourite(player.Id, profileName);

        if (!favourite.Success)
            return Task.FromResult(favourite);

        return Task.FromResult(manager.ActivateProfile(profileName));
    }
}
