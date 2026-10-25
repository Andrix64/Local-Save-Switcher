namespace Lss;

/// <summary>
/// Se LSS può gestire i salvataggi di un gioco.
/// </summary>
public enum SupportState
{
    /// <summary>Ci sono salvataggi in userdata: LSS li gestisce.</summary>
    Supported,

    /// <summary>Solo remotecache.vdf: il gioco salva altrove (Steam Auto-Cloud).</summary>
    SavesElsewhere,

    /// <summary>Cartella vuota: nessun salvataggio.</summary>
    NoSaves,

    /// <summary>Il launcher conosce la cartella, ma non esiste ancora: va avviato il gioco.</summary>
    NotFound,

    /// <summary>Più cartelle o solo file sparsi: LSS non li cambia ancora.</summary>
    Unsure,

    /// <summary>La cartella trovata o scelta non passa il controllo di sicurezza.</summary>
    Unsafe
}

/// <summary>
/// Da dove viene la cartella dei salvataggi.
/// </summary>
public enum SaveSource
{
    /// <summary>Cartella userdata/IDAccount/AppID di Steam.</summary>
    Userdata,

    /// <summary>Regola del launcher (per Steam, Auto-Cloud in appinfo.vdf).</summary>
    Launcher,

    /// <summary>Cartella scelta a mano dall'utente.</summary>
    Chosen,

    /// <summary>Cartella trovata da LSS guardando il disco.</summary>
    Learned,

    /// <summary>Nessuna cartella trovata.</summary>
    None
}

/// <summary>
/// Gioco di un launcher con una cartella di salvataggi.
/// </summary>
/// <param name="Provider">Launcher del gioco: "steam" per Steam.</param>
/// <param name="Id">Numero del gioco nel launcher (per Steam l'AppID, es. "870780").</param>
/// <param name="Name">Nome dal manifest, o quello noto, o "App 870780".</param>
/// <param name="InstallDir">Cartella in steamapps/common, o "" se non è installato.</param>
/// <param name="LibraryPath">Libreria Steam che lo contiene, o "" se non è installato.</param>
/// <param name="InstallPath">Cartella del gioco installato, o "" se non è installato.</param>
/// <param name="SaveFolder">Cartella userdata/IDAccount/AppID, o "" se non c'è ancora.</param>
/// <param name="Support">Se LSS può gestirlo.</param>
/// <param name="IconPath">Icona 32×32, o "" se non c'è.</param>
/// <param name="CoverPath">Copertina 300×450, o "" se non c'è.</param>
/// <param name="Source">Da dove viene la cartella dei salvataggi.</param>
/// <param name="Reason">Motivo per il log, in italiano; non si mostra così com'è.</param>
/// <param name="HasOtherSaves">true se il gioco ha salvataggi anche in altre cartelle.</param>
public sealed record GameInfo(
    string Provider,
    string Id,
    string Name,
    string InstallDir,
    string LibraryPath,
    string InstallPath,
    string SaveFolder,
    SupportState Support,
    string IconPath = "",
    string CoverPath = "",
    SaveSource Source = SaveSource.Userdata,
    string Reason = "",
    bool HasOtherSaves = false)
{
    public const string ControlAppId = "870780";

    /// <summary>true se il gioco è installato in una libreria trovata.</summary>
    public bool IsInstalled => InstallDir != "" && LibraryPath != "";

    /// <summary>Chiave salvata in lss.ini: provider/id.</summary>
    public string Key => Provider + "/" + Id;

    /// <summary>
    /// Control con la cartella dei salvataggi data: usato finché l'interfaccia
    /// WinForms apre solo Control, e dai test.
    /// </summary>
    public static GameInfo Control(string saveFolder) =>
        new("steam", ControlAppId, "Control", "", "", "", saveFolder, SupportState.Supported);
}
