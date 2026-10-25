using System.Diagnostics;
using System.Text;

namespace Lss;

/// <summary>
/// IPlatform per Linux: cartelle di Steam (installazione normale, Flatpak,
/// Snap), registry.vdf, processi da /proc, xdg-open e istanza unica tramite
/// file di lock. Niente attributo SupportedOSPlatform: le funzioni pure
/// vanno testate su ogni sistema (CA1416 nei test); la classe si crea solo
/// su Linux, in Program.
/// </summary>
public sealed class LinuxPlatform : IPlatform
{
    /// <summary>
    /// Cartelle di Steam trovate nella cartella personale, la principale per
    /// prima. Lista vuota se Steam non è installato.
    /// </summary>
    public IReadOnlyList<string> FindSteamRoots() => FindSteamRoots(Home);

    private static string Home => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    /// <summary>
    /// Cartelle di Steam sotto <paramref name="home"/>, in ordine: normale,
    /// Flatpak, Snap. Solo quelle con userdata, senza doppioni dovuti ai
    /// collegamenti (~/.steam/steam punta a quella normale).
    /// </summary>
    internal static IReadOnlyList<string> FindSteamRoots(string home)
    {
        string[] candidates =
        {
            Path.Combine(home, ".local", "share", "Steam"),
            Path.Combine(home, ".steam", "steam"),
            Path.Combine(home, ".steam", "root"),
            Path.Combine(home, ".var", "app", "com.valvesoftware.Steam", ".local", "share", "Steam"),
            Path.Combine(home, "snap", "steam", "common", ".local", "share", "Steam")
        };

        var roots = new List<string>();

        foreach (string candidate in candidates)
        {
            if (!Directory.Exists(Path.Combine(candidate, "userdata")))
                continue;

            // Il collegamento va risolto fino alla cartella vera, per non contarla due volte.
            string? target = new DirectoryInfo(candidate).ResolveLinkTarget(returnFinalTarget: true)?.FullName;
            string root = Path.GetFullPath(target ?? candidate);

            if (!roots.Contains(root, StringComparer.Ordinal))
                roots.Add(root);
        }

        return roots;
    }

    // Processi già visti: per ognuno si legge /proc una volta sola (il
    // controllo gira ogni 2 secondi). Come processPaths in WindowsPlatform.
    private readonly Dictionary<int, bool> processMatches = [];
    private readonly object processLock = new();

    /// <summary>
    /// true se un registry.vdf di Steam segna il gioco come aperto, o se un
    /// processo gioca con quel gioco (vedi FindGameProcesses).
    /// </summary>
    public bool IsGameRunning(GameInfo game) =>
        RegistryFiles(Home).Any(file => RegistryFileSaysRunning(file, game.Id))
        || FindGameProcesses(game).Count > 0;

    /// <summary>
    /// Processi del gioco: quelli avviati da Steam (SteamAppId o SteamGameId
    /// nell'ambiente, native e Proton) e quelli che girano dalla cartella di
    /// installazione. Con data di avvio se si legge.
    /// </summary>
    private List<(int Id, DateTime? Start)> FindGameProcesses(GameInfo game)
    {
        var found = new List<(int Id, DateTime? Start)>();

        // Con il "/" finale: "Control" non deve trovare "Control Ultimate".
        string installPrefix = game.InstallPath == "" ? "" : Path.GetFullPath(game.InstallPath) + "/";

        lock (processLock)
        {
            var seen = new HashSet<int>();

            foreach (string entry in Directory.GetDirectories("/proc"))
            {
                string name = Path.GetFileName(entry);

                if (!int.TryParse(name, out int pid) || pid <= 0 || pid == Environment.ProcessId)
                    continue;

                seen.Add(pid);

                if (!processMatches.TryGetValue(pid, out bool matches))
                {
                    matches = IsGameProcess(pid, game.Id, installPrefix);
                    processMatches[pid] = matches;
                }

                if (matches)
                    found.Add((pid, TryStartTime(pid)));
            }

            // Processi chiusi nel frattempo: via dalla cache.
            foreach (int pid in processMatches.Keys.Where(pid => !seen.Contains(pid)).ToList())
                processMatches.Remove(pid);
        }

        return found;
    }

    /// <summary>
    /// true se il processo ha SteamAppId/SteamGameId del gioco, o se il suo
    /// eseguibile sta nella cartella del gioco. Ogni lettura che fallisce
    /// (processo finito, altro utente) vuol dire "non è del gioco".
    /// </summary>
    private static bool IsGameProcess(int pid, string appId, string installPrefix)
    {
        try
        {
            if (EnvironMatches(File.ReadAllBytes($"/proc/{pid}/environ"), appId))
                return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Nessun ambiente leggibile: si prova con l'eseguibile.
        }

        if (installPrefix == "")
            return false;

        try
        {
            string? exe = new FileInfo($"/proc/{pid}/exe").LinkTarget;

            return exe != null && exe.StartsWith(installPrefix, StringComparison.Ordinal);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>
    /// true se l'ambiente del processo (contenuto di /proc/PID/environ) è di quel gioco.
    /// </summary>
    internal static bool EnvironMatches(ReadOnlySpan<byte> environ, string appId)
    {
        // Le variabili sono separate da un byte 0. Il confronto è esatto:
        // SteamAppId=1056001 non è SteamAppId=105600.
        string[] wanted = [$"SteamAppId={appId}", $"SteamGameId={appId}"];

        foreach (Range range in environ.Split((byte)0))
        {
            string entry = Encoding.UTF8.GetString(environ[range]);

            if (wanted.Contains(entry, StringComparer.Ordinal))
                return true;
        }

        return false;
    }

    private static DateTime? TryStartTime(int pid)
    {
        try
        {
            using Process process = Process.GetProcessById(pid);
            return process.StartTime;
        }
        catch
        {
            // Processo già chiuso o non leggibile.
            return null;
        }
    }

    /// <summary>
    /// registry.vdf delle installazioni di Steam sotto <paramref name="home"/>
    /// (normale, Flatpak, Snap) che esistono.
    /// </summary>
    internal static IReadOnlyList<string> RegistryFiles(string home)
    {
        string[] paths =
        {
            Path.Combine(home, ".steam", "registry.vdf"),
            Path.Combine(home, ".var", "app", "com.valvesoftware.Steam", ".steam", "registry.vdf"),
            Path.Combine(home, "snap", "steam", "common", ".steam", "registry.vdf")
        };

        return paths.Where(File.Exists).ToList();
    }

    /// <summary>
    /// true se il registry.vdf dice che il gioco è aperto: RunningAppID per
    /// quello in primo piano, Apps\ID\Running per tutti. Steam lo scrive
    /// mentre gira, quindi un errore di lettura vuol dire "non aperto".
    /// </summary>
    internal static bool RegistrySaysRunning(string registryText, string appId)
    {
        try
        {
            Vdf.Node node = Vdf.Parse(registryText);

            return Vdf.Find(node, "Registry", "HKCU", "Software", "Valve", "Steam", "RunningAppID") == appId
                || Vdf.Find(node, "Registry", "HKCU", "Software", "Valve", "Steam", "Apps", appId, "Running") == "1";
        }
        catch
        {
            return false;
        }
    }

    private static bool RegistryFileSaysRunning(string file, string appId)
    {
        try
        {
            // Steam può starlo scrivendo nello stesso momento.
            return RegistrySaysRunning(File.ReadAllText(file), appId);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>
    /// Quando è partito il gioco in esecuzione, se si riesce a leggerlo.
    /// </summary>
    public DateTime? GameStartTime(GameInfo game) =>
        FindGameProcesses(game).Select(p => p.Start).Min();

    /// <summary>
    /// Con Steam aperto, Steam Cloud può risincronizzare i salvataggi.
    /// </summary>
    public bool IsSteamRunning() =>
        IsProcessRunning("steam") || IsProcessRunning("steamwebhelper");

    private static bool IsProcessRunning(string name)
    {
        Process[] processes = Process.GetProcessesByName(name);

        foreach (Process process in processes)
            process.Dispose();

        return processes.Length > 0;
    }

    /// <summary>
    /// xdg-open passa i link steam:// al Steam nativo o Flatpak. Steam si apre
    /// da solo se è chiuso.
    /// </summary>
    public void OpenSteamUri(string uri)
    {
        Process.Start(new ProcessStartInfo("xdg-open", uri) { UseShellExecute = false })?.Dispose();
    }

    /// <summary>
    /// Un file di lock nella cartella di runtime dell'utente: il sistema lo
    /// libera quando il processo finisce, anche se si chiude male.
    /// </summary>
    public IDisposable? AcquireSingleInstance()
    {
        string folder = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR") is { Length: > 0 } runtime
            ? runtime
            : Path.GetTempPath();

        return Lock(Path.Combine(folder, "lss.lock"));
    }

    /// <summary>
    /// Prende un lock esclusivo sul file. null se un altro processo lo tiene;
    /// altrimenti il lock, da tenere fino alla chiusura.
    /// </summary>
    internal static IDisposable? Lock(string path)
    {
        try
        {
            // FileShare.None su Linux prende un lock esclusivo (flock): una
            // seconda istanza non riesce ad aprirlo.
            return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
        catch (IOException)
        {
            return null;
        }
    }
}
