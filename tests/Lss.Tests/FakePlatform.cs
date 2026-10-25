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

    public IReadOnlyList<string> FindSteamRoots() => SteamRoots;
    public KnownFolders GetKnownFolders() => Folders;
    public bool IsGameRunning(GameInfo game) => GameRunning;
    public DateTime? GameStartTime(GameInfo game) => StartTime;
    public bool IsSteamRunning() => SteamRunning;
    public void OpenSteamUri(string uri) => OpenUri(uri);
    public void OpenUri(string uri) => OpenedUris.Add(uri);
    public IReadOnlyList<(int Id, DateTime? Start)> FindProcessesUnder(string folder) => [];
    public bool StartProgram(string exe, string arguments, string workingDir) => true;
    public bool IsProcessRunning(string name) => false;
    public string? ReadRegistryString(string key, string name) => null;
    public IReadOnlyList<string> RegistrySubKeys(string key) => [];
    public IDisposable? AcquireSingleInstance() => new MemoryStream();
}
