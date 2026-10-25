namespace Lss;

/// <summary>
/// Un launcher (Steam, Epic, GOG, Heroic...) o i giochi aggiunti a mano:
/// trova account e giochi, dice se un gioco è aperto e lo avvia.
/// </summary>
public interface IGameProvider
{
    /// <summary>Identificatore del launcher: "steam", "epic", "gog", "heroic", "manual".</summary>
    string Id { get; }

    /// <summary>Nome del launcher (marchio: non tradotto).</summary>
    string DisplayName { get; }

    /// <summary>Account del launcher con giochi da gestire.</summary>
    IReadOnlyList<GameAccount> FindAccounts();

    /// <summary>Giochi di un account.</summary>
    IReadOnlyList<GameInfo> FindGames(GameAccount account);

    /// <summary>false se il provider non sa dire se il gioco è aperto.</summary>
    bool CanDetectRunning(GameInfo game);

    /// <summary>true se il gioco è aperto (nel dubbio true).</summary>
    bool IsGameRunning(GameInfo game);

    /// <summary>Quando è partito il gioco aperto, se si riesce a leggerlo.</summary>
    DateTime? GameStartTime(GameInfo game);

    /// <summary>true se il provider sa avviare il gioco.</summary>
    bool CanLaunch(GameInfo game);

    /// <summary>Avvia il gioco tramite il launcher.</summary>
    Result Launch(GameInfo game);

    /// <summary>true se il launcher è aperto.</summary>
    bool IsLauncherRunning();

    /// <summary>Stato del cloud di un gioco per un account.</summary>
    CloudState GetCloudState(GameAccount account, GameInfo game);

    /// <summary>
    /// Regole dei salvataggi che il launcher conosce per il gioco. Vuoto se
    /// non ne sa niente: il gioco passa dalla ricerca sul disco.
    /// </summary>
    IReadOnlyList<SaveRule> SaveRules(GameAccount account, GameInfo game);

    /// <summary>
    /// Sostituzioni di radici (rootoverrides) per il gioco. Vuoto se non ce ne sono.
    /// </summary>
    IReadOnlyList<RootOverride> RootOverrides(GameAccount account, GameInfo game);

    /// <summary>
    /// Prefisso Proton o Wine del gioco, o "" se non ne ha.
    /// </summary>
    string PrefixOf(GameInfo game);

    /// <summary>
    /// Cartelle del launcher che una cartella dei salvataggi non può essere
    /// (installazione, librerie, dati): vedi SaveFolderSafety.
    /// </summary>
    IReadOnlyList<string> LauncherFolders(GameAccount account);
}
