namespace Lss.Tests;

/// <summary>
/// IPlatform per i test: niente registro, processi o Steam veri. I campi
/// si impostano dal test.
/// </summary>
public sealed class FakePlatform : IPlatform
{
    public List<string> SteamRoots { get; } = [];
    public bool GameRunning { get; set; }
    public DateTime? StartTime { get; set; }
    public bool SteamRunning { get; set; }
    public List<string> OpenedUris { get; } = [];
    public KnownFolders Folders { get; set; } = new("", "", "", "", "", "", "", "", "", "", "");

    // Processi che "girano": nome senza .exe, e processi con l'ora di avvio per cartella.
    public List<string> ProcessNames { get; } = [];
    public List<(string Folder, int Id, DateTime? Start)> Processes { get; } = [];

    // Valori del registro: chiave completa, nome del valore, valore; chiavi con le sottochiavi.
    public Dictionary<(string Key, string Name), string> RegistryValues { get; } = [];
    public Dictionary<string, List<string>> RegistryKeys { get; } = [];
    public List<string> StartedPrograms { get; } = [];

    public IReadOnlyList<string> FindSteamRoots() => SteamRoots;
    public KnownFolders GetKnownFolders() => Folders;
    public bool IsGameRunning(GameInfo game) => GameRunning;
    public DateTime? GameStartTime(GameInfo game) => StartTime;
    public bool IsSteamRunning() => SteamRunning;
    public void OpenSteamUri(string uri) => OpenUri(uri);
    public void OpenUri(string uri) => OpenedUris.Add(uri);

    public IReadOnlyList<(int Id, DateTime? Start)> FindProcessesUnder(string folder) =>
        Processes.Where(process => process.Folder == folder).Select(process => (process.Id, process.Start)).ToList();

    public bool StartProgram(string exe, string arguments, string workingDir)
    {
        StartedPrograms.Add(exe + " " + arguments + " in " + workingDir);
        return true;
    }

    public bool IsProcessRunning(string name) => ProcessNames.Contains(name);
    public string? ReadRegistryString(string key, string name) => RegistryValues.GetValueOrDefault((key, name));
    public IReadOnlyList<string> RegistrySubKeys(string key) => RegistryKeys.GetValueOrDefault(key) ?? [];
    public IDisposable? AcquireSingleInstance() => new MemoryStream();
}
