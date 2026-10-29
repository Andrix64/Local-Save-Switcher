using Lss.App.Services;

namespace Lss.App.ViewModels;

/// <summary>
/// Cambiare i file di un gioco Steam mentre Steam è aperto lo confonde: tiene l'elenco dei salvataggi
/// (remotecache.vdf) in memoria e lo riscrive, e il gioco non trova più la partita. Prima di cambiare
/// i file si chiede di chiudere Steam: lo riavvia il gioco quando parte.
/// </summary>
internal static class SteamGuard
{
    /// <summary>
    /// true se si può cambiare i file: Steam è chiuso, l'utente l'ha fatto chiudere a LSS o ha scelto di
    /// continuare comunque. false se annulla o Steam non si chiude. Non chiede nulla per i giochi di altri
    /// launcher o con Steam chiuso.
    /// </summary>
    public static async Task<bool> EnsureClosedAsync(IDialogService dialogs, SaveManager manager)
    {
        if (manager.Game.Provider != "steam" || !manager.IsLauncherRunning())
            return true;

        string game = manager.Game.Name;

        // Nella Gaming Mode Steam è la schermata stessa: non si chiude da qui. Si spiega come fare.
        if (Platform.Current.IsSteamGamingMode)
        {
            return await dialogs.ConfirmAsync(Strings.SteamDeckHeading, Strings.SteamDeckText,
                Strings.ContinueAnyway, kind: InfoKind.Warning);
        }

        int choice = await dialogs.ChooseAsync(Strings.SteamOpenHeading, Loc.F(Strings.SteamOpenText, game),
            [Strings.SteamCloseAndContinue, Strings.ContinueAnyway]);

        if (choice == 1)
            return true;

        if (choice != 0)
            return false;

        bool closed = await Task.Run(() => Platform.Current.CloseSteam());

        if (!closed)
            await dialogs.InformAsync(Strings.SteamOpenHeading, Strings.SteamCloseFailed, InfoKind.Warning);

        return closed;
    }
}
