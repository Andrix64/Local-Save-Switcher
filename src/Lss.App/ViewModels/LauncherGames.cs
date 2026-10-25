namespace Lss.App.ViewModels;

/// <summary>
/// I giochi di un account scelto all'avvio, con il launcher che li gestisce.
/// </summary>
/// <param name="Provider">Launcher dell'account.</param>
/// <param name="Account">Account scelto (o l'unico del launcher).</param>
/// <param name="Games">Giochi trovati per l'account.</param>
internal sealed record LauncherGames(IGameProvider Provider, GameAccount Account, IReadOnlyList<GameInfo> Games);
