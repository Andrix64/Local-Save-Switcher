using Lss.App.Services;

namespace Lss.App.ViewModels;

/// <summary>Una partita avviata dalla modalità semplice: chi gioca, quando, se il gioco è partito.</summary>
public sealed record PlaySession(
    SaveManager Manager,
    GameItemViewModel Item,
    Person Player,
    DateTime Started,
    bool Launched,
    bool CanDetect);

/// <summary>Le domande di SimplePlay, con i dialoghi: di chi sono i salvataggi già nel gioco.</summary>
internal sealed class SimpleQuestions(IDialogService dialogs) : ISimpleQuestions
{
    public Task<Person?> WhoseSavesAsync(string gameName, IReadOnlyList<Person> people) =>
        dialogs.ChoosePersonAsync(Loc.F(Strings.SimpleWhoseSaves, gameName), "", people, Strings.SimpleDontKnow);
}

/// <summary>
/// Il giro di una partita dalla modalità semplice: copertura cloud (solo un admin
/// la consente), preparazione (SimplePlay), avvio, attesa della fine, salvataggio.
/// </summary>
internal sealed class SimplePlayFlow(IDialogService dialogs, People people)
{
    /// <summary>Controlli e avvio. Null se non si gioca (annullato, errore già mostrato).</summary>
    internal async Task<PlaySession?> StartAsync(SaveManager manager, GameItemViewModel item, Person player,
        Func<Task<Result>> prepare)
    {
        if (!await CloudAllowedAsync(manager, item, player))
            return null;

        Result prepared = await prepare();

        if (!prepared.Success)
        {
            await dialogs.InformAsync(item.Name, prepared.Message, InfoKind.Warning);
            return null;
        }

        bool launched = item.Provider.CanLaunch(item.Info);

        if (launched)
        {
            Result started = manager.LaunchGame();

            if (!started.Success)
            {
                await dialogs.InformAsync(item.Name, started.Message, InfoKind.Warning);
                return null;
            }
        }

        return new PlaySession(manager, item, player, DateTime.Now, launched, item.Provider.CanDetectRunning(item.Info));
    }

    /// <summary>
    /// Finisce quando il gioco si chiude (se il launcher lo sa dire) o quando l'utente
    /// preme "Ho finito di giocare".
    /// </summary>
    internal async Task WaitForEndAsync(PlaySession session, Task finishedByButton)
    {
        if (session.Launched && session.CanDetect)
            await Task.WhenAny(WaitWhileRunningAsync(session.Manager), finishedByButton);
        else
            await finishedByButton;
    }

    /// <summary>Salva i progressi nella partita giocata e registra il tempo di gioco.</summary>
    internal async Task<Result> FinishAsync(PlaySession session)
    {
        // Nella modalità semplice si salva sempre alla chiusura, qualunque sia l'impostazione avanzata.
        Result saved = session.Manager.SaveProgress(onGameExit: true);

        if (!saved.Success)
        {
            await dialogs.InformAsync(session.Item.Name, saved.Message + "\n" + Strings.SimpleAskForHelp, InfoKind.Error);
            return saved;
        }

        session.Manager.AddPlayTime(session.Manager.State.ActiveProfile, session.Started, DateTime.Now);

        return Result.Ok(Loc.F(Strings.SimpleSaved, session.Item.Name, session.Player.Name));
    }

    // Un gioco con le copie online attive, senza consenso: solo un admin può farlo partire.
    private async Task<bool> CloudAllowedAsync(SaveManager manager, GameItemViewModel item, Person player)
    {
        if (manager.GetCloudState() != CloudState.Enabled || manager.State.CloudAllowedInSimple)
            return true;

        if (!player.IsAdmin)
        {
            string admins = string.Join(", ", people.Admins.Select(admin => admin.Name));

            bool askAdmin = await dialogs.InformAsync(item.Name,
                Loc.F(Strings.SimpleCloudBlocked, item.Name, admins), InfoKind.Warning, Strings.SimpleAdminHere);

            if (!askAdmin || await dialogs.ChooseAdminAsync(people) == null)
                return false;
        }

        // Un admin decide: consentire il gioco, o giocarlo solo questa volta.
        string text = Loc.F(Strings.SimpleCloudAdminText, item.Name);

        if (await dialogs.ConfirmAsync(item.Name, text, Strings.SimpleCloudAllow, kind: InfoKind.Warning))
        {
            Result allowed = manager.SetCloudAllowedInSimple(true);

            if (!allowed.Success)
            {
                await dialogs.InformAsync(item.Name, allowed.Message, InfoKind.Warning);
                return false;
            }

            return true;
        }

        return await dialogs.ConfirmAsync(item.Name, text, Strings.SimpleCloudOnce, kind: InfoKind.Warning);
    }

    // Aspetta che il gioco parta (al massimo due minuti), poi che si chiuda.
    private static Task WaitWhileRunningAsync(SaveManager manager) =>
        Task.Run(async () =>
        {
            for (int i = 0; i < 60 && !manager.IsGameRunning(); i++)
                await Task.Delay(TimeSpan.FromSeconds(2));

            while (manager.IsGameRunning())
                await Task.Delay(TimeSpan.FromSeconds(2));
        });
}
