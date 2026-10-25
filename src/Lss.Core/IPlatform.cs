namespace Lss;

/// <summary>
/// Tutto quello che dipende dal sistema operativo: Steam, processi, avvio
/// del gioco, istanza unica. Un'implementazione per sistema
/// (WindowsPlatform, poi LinuxPlatform) e una finta per i test.
/// </summary>
public interface IPlatform
{
    /// <summary>
    /// Cartelle di Steam trovate, la principale per prima. Vuoto se Steam
    /// non è installato.
    /// </summary>
    IReadOnlyList<string> FindSteamRoots();

    /// <summary>
    /// Cartelle dell'utente usate per trovare i salvataggi (vedi KnownFolders).
    /// </summary>
    KnownFolders GetKnownFolders();

    /// <summary>true se il gioco è aperto (nel dubbio true).</summary>
    bool IsGameRunning(GameInfo game);

    /// <summary>Quando è partito il gioco aperto, se si riesce a leggerlo.</summary>
    DateTime? GameStartTime(GameInfo game);

    bool IsSteamRunning();

    /// <summary>Apre un indirizzo steam:// (Steam si apre da solo se è chiuso).</summary>
    void OpenSteamUri(string uri);

    /// <summary>Apre un indirizzo con il programma registrato (steam://, com.epicgames.launcher://, heroic://...).</summary>
    void OpenUri(string uri);

    /// <summary>Processi il cui eseguibile è dentro la cartella, con l'ora di avvio se nota.</summary>
    IReadOnlyList<(int Id, DateTime? Start)> FindProcessesUnder(string folder);

    /// <summary>Avvia un programma. false se non parte.</summary>
    bool StartProgram(string exe, string arguments, string workingDir);

    /// <summary>true se c'è un processo con quel nome di file (senza .exe).</summary>
    bool IsProcessRunning(string name);

    /// <summary>Valore stringa del registro di Windows, null altrove o se manca.</summary>
    string? ReadRegistryString(string key, string name);

    /// <summary>Sottochiavi di una chiave del registro di Windows, vuoto altrove.</summary>
    IReadOnlyList<string> RegistrySubKeys(string key);

    /// <summary>
    /// Prende il diritto di essere l'unica istanza dell'app. null se un'altra
    /// istanza è già aperta; altrimenti un oggetto da tenere fino alla chiusura.
    /// </summary>
    IDisposable? AcquireSingleInstance();
}
