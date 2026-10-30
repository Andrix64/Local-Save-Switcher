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
        dialogs.ChoosePersonAsync(Loc.F(Strings.SimpleWhoseSaves, gameName), Strings.SimpleWhoseSavesHint, people, Strings.SimpleDontKnow);
}

/// <summary>
/// Il giro di una partita dalla modalità semplice: copertura cloud (solo un admin
/// la consente), preparazione (SimplePlay), avvio, attesa della fine, salvataggio.
/// </summary>
internal sealed class SimplePlayFlow(IDialogService dialogs, People people, Func<bool>? playerUnlocked = null)
{
    /// <summary>Controlli e avvio. Null se non si gioca (annullato, errore già mostrato).</summary>
    internal async Task<PlaySession?> StartAsync(SaveManager manager, GameItemViewModel item, Person player,
        Func<Task<Result>> prepare)
    {
        if (!await CloudAllowedAsync(manager, item, player))
            return null;

        // Steam aperto confonde il cambio dei file: si chiude prima (lo riavvia il gioco).
        if (!await SteamGuard.EnsureClosedAsync(dialogs, manager))
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
    internal async Task WaitForEndAsync(PlaySession session, Task finishedByButton, Action? onStartOverdue = null)
    {
        if (session.Launched && session.CanDetect)
        {
            using var cancel = new CancellationTokenSource();

            // Chi arriva prima: gioco chiuso o pulsante. Poi l'attesa si ferma.
            await Task.WhenAny(WaitWhileRunningAsync(session.Manager, onStartOverdue, cancel.Token), finishedByButton);
            cancel.Cancel();
        }
        else
        {
            await finishedByButton;
        }
    }

    /// <summary>Salva i progressi nella partita giocata e registra il tempo di gioco.</summary>
    internal async Task<Result> FinishAsync(PlaySession session)
    {
        // Nella modalità semplice si salva sempre alla chiusura, qualunque sia l'impostazione avanzata.
        Result saved = await Task.Run(() => session.Manager.SaveProgress(onGameExit: true));

        if (!saved.Success)
        {
            // Cartella dei salvataggi vuota alla chiusura: di solito non si è salvato nel gioco, non è un guasto.
            if (saved.Message == Strings.GameSavesEmptyWouldClear)
            {
                // Non si è salvato niente, ma la persona ha giocato: il tempo conta.
                DateTime stop = DateTime.Now;
                await Task.Run(() => session.Manager.AddPlayTime(session.Manager.State.ActiveProfile, session.Started, stop));

                await dialogs.InformAsync(session.Item.Name,
                    Strings.SimpleNothingSaved + "\n\n" + Strings.SimpleNothingSavedWarning, InfoKind.Warning);
                return saved;
            }

            await dialogs.InformAsync(session.Item.Name, saved.Message + "\n" + Strings.SimpleAskForHelp, InfoKind.Error);
            return saved;
        }

        DateTime end = DateTime.Now;
        await Task.Run(() => session.Manager.AddPlayTime(session.Manager.State.ActiveProfile, session.Started, end));

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

            bool askAdmin = await dialogs.InformAsync(Loc.F(Strings.SimpleCloudBlockedHeading, item.Name),
                Loc.F(Strings.SimpleCloudBlocked, item.Name, admins), InfoKind.Warning, Strings.SimpleAdminHere);

            if (!askAdmin)
                return false;
        }

        // Chi gioca, se è admin, mette solo il suo PIN (se ce l'ha); altrimenti si sceglie un admin.
        if (await dialogs.ChooseAdminAsync(people, player, playerUnlocked?.Invoke() ?? false) == null)
            return false;

        // Un admin decide, in una domanda sola: consentire il gioco, o giocarlo solo questa volta.
        int choice = await dialogs.ChooseAsync(Strings.SimpleCloudAdminHeading,
            Loc.F(Strings.SimpleCloudAdminText, item.Name), [Strings.SimpleCloudAllow, Strings.SimpleCloudOnce]);

        if (choice == 0)
        {
            Result allowed = manager.SetCloudAllowedInSimple(true);

            if (!allowed.Success)
            {
                await dialogs.InformAsync(item.Name, allowed.Message, InfoKind.Warning);
                return false;
            }

            return true;
        }

        return choice == 1;
    }

    /// <summary>Dopo tanto senza vedere il gioco si mostra "Ho finito di giocare".</summary>
    internal static readonly TimeSpan StartOverdueAfter = TimeSpan.FromMinutes(2);

    // Aspetta che il gioco parta (senza limite: un aggiornamento o un altro launcher possono
    // metterci molto), poi che si chiuda.
    private static Task WaitWhileRunningAsync(SaveManager manager, Action? onOverdue, CancellationToken cancel) =>
        WaitWhileRunningAsync(manager.IsGameRunning, TimeSpan.FromSeconds(2), StartOverdueAfter, onOverdue, cancel);

    /// <summary>
    /// Aspetta che il gioco si veda in esecuzione, poi che si chiuda. Se dopo overdueAfter
    /// non si è ancora visto chiama onOverdue (una volta) e continua ad aspettare.
    /// </summary>
    internal static Task WaitWhileRunningAsync(Func<bool> isRunning, TimeSpan delay, TimeSpan overdueAfter,
        Action? onOverdue, CancellationToken cancel) =>
        Task.Run(async () =>
        {
            TimeSpan waited = TimeSpan.Zero;
            bool overdue = false;

            while (!isRunning())
            {
                await Task.Delay(delay, cancel);
                waited += delay;

                if (!overdue && waited >= overdueAfter)
                {
                    overdue = true;
                    onOverdue?.Invoke();
                }
            }

            while (isRunning())
                await Task.Delay(delay, cancel);
        }, cancel);
}
