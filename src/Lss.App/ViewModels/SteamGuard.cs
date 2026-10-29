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
    /// true se si può cambiare i file: Steam è chiuso o l'utente l'ha fatto chiudere a LSS. false se annulla
    /// o se Steam non si chiude. Non chiede nulla per i giochi di altri
    /// launcher o con Steam chiuso.
    /// </summary>
    public static async Task<bool> EnsureClosedAsync(IDialogService dialogs, SaveManager manager)
    {
        if (manager.Game.Provider != "steam" || !manager.IsLauncherRunning())
            return true;

        string game = manager.Game.Name;

        // Nella Gaming Mode Steam è la schermata stessa: si chiude lo stesso (steam -shutdown) e la sessione lo
        // riavvia da sola. Il testo avverte che LSS può chiudersi insieme a Steam.
        string text = Platform.Current.IsSteamGamingMode
            ? Loc.F(Strings.SteamOpenText, game) + "\n\n" + Strings.SteamDeckText
            : Loc.F(Strings.SteamOpenText, game);

        int choice = await dialogs.ChooseAsync(Strings.SteamOpenHeading, text,
            [Strings.SteamCloseAndContinue]);

        if (choice != 0)
            return false;

        bool closed = await Task.Run(() => Platform.Current.CloseSteam());

        if (!closed)
            await dialogs.InformAsync(Strings.SteamOpenHeading, Strings.SteamCloseFailed, InfoKind.Warning);

        return closed;
    }
}
