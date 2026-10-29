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
    /// Cartelle dell'utente di Linux, da variabili XDG e cartella personale.
    /// </summary>
    public KnownFolders GetKnownFolders() => KnownFoldersFor(
        Home,
        Environment.GetEnvironmentVariable("XDG_DATA_HOME"),
        Environment.GetEnvironmentVariable("XDG_CONFIG_HOME"),
        Environment.UserName);

    /// <summary>
    /// Cartelle dell'utente sotto <paramref name="home"/>. Documents c'è solo se
    /// la cartella esiste. Un valore XDG vuoto o relativo vale come non
    /// impostato (lo dice la specifica XDG), quindi si usa il predefinito.
    /// </summary>
    internal static KnownFolders KnownFoldersFor(string home, string? xdgData, string? xdgConfig, string userName)
    {
        string documents = Path.Combine(home, "Documents");

        return new KnownFolders(
            Home: home,
            AppData: "",
            LocalAppData: "",
            LocalAppDataLow: "",
            Documents: Directory.Exists(documents) ? documents : "",
            SavedGames: "",
            Public: "",
            ProgramData: "",
            XdgData: AbsoluteOr(xdgData, Path.Combine(home, ".local", "share")),
            XdgConfig: AbsoluteOr(xdgConfig, Path.Combine(home, ".config")),
            UserName: userName);
    }

    private static string AbsoluteOr(string? value, string fallback) =>
        value is { Length: > 0 } && Path.IsPathRooted(value) ? value : fallback;

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
        var found = FindProcessesUnder(game.InstallPath).ToList();

        foreach (int pid in ProcessIds())
        {
            if (found.Any(p => p.Id == pid) || !EnvironMatches(ReadEnviron(pid), game.Id))
                continue;

            found.Add((pid, TryStartTime(pid)));
        }

        return found;
    }

    /// <summary>
    /// Processi che girano dalla cartella indicata: il loro eseguibile sta lì,
    /// o un argomento della riga di comando ci punta (programmi Windows con
    /// Wine). Con data di avvio se si legge. Nessun processo se la cartella è "".
    /// </summary>
    public IReadOnlyList<(int Id, DateTime? Start)> FindProcessesUnder(string folder)
    {
        var found = new List<(int Id, DateTime? Start)>();

        if (folder == "")
            return found;

        // Con il "/" finale: "Control" non deve trovare "Control Ultimate".
        string fullFolder = Path.GetFullPath(folder);
        string prefix = fullFolder.TrimEnd('/') + "/";

        foreach (int pid in ProcessIds())
        {
            if (ExecutableIsUnder(pid, prefix) || CmdlineIsUnder(pid, fullFolder))
                found.Add((pid, TryStartTime(pid)));
        }

        return found;
    }

    /// <summary>
    /// Pid dei processi in /proc, escluso questo.
    /// </summary>
    private static IEnumerable<int> ProcessIds()
    {
        foreach (string entry in Directory.GetDirectories("/proc"))
        {
            if (int.TryParse(Path.GetFileName(entry), out int pid) && pid > 0 && pid != Environment.ProcessId)
                yield return pid;
        }
    }

    /// <summary>
    /// true se l'eseguibile del processo sta sotto il prefisso. Ogni lettura
    /// che fallisce (processo finito, altro utente) vuol dire "no".
    /// </summary>
    private static bool ExecutableIsUnder(int pid, string prefix)
    {
        try
        {
            string? exe = new FileInfo($"/proc/{pid}/exe").LinkTarget;

            return exe != null && exe.StartsWith(prefix, StringComparison.Ordinal);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>
    /// true se un argomento della riga di comando del processo sta nella
    /// cartella. Ogni lettura che fallisce vuol dire "no".
    /// </summary>
    private static bool CmdlineIsUnder(int pid, string folder)
    {
        try
        {
            return CmdlineMatches(File.ReadAllBytes($"/proc/{pid}/cmdline"), folder);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>
    /// true se un argomento della riga di comando (contenuto di
    /// /proc/PID/cmdline, separato da byte 0) sta nella cartella. Un programma
    /// Windows avviato con Wine ha argomenti come Z:\home\...: i "\" diventano
    /// "/" e la "Z:" iniziale va via. Il confronto ignora le maiuscole.
    /// </summary>
    internal static bool CmdlineMatches(ReadOnlySpan<byte> cmdline, string folder)
    {
        string trimmed = folder.Replace('\\', '/').TrimEnd('/');

        if (trimmed == "")
            return false;

        string prefix = trimmed + "/";

        foreach (Range range in cmdline.Split((byte)0))
        {
            string arg = Encoding.UTF8.GetString(cmdline[range]).Replace('\\', '/');

            if (arg.StartsWith("Z:", StringComparison.OrdinalIgnoreCase))
                arg = arg[2..];

            if (arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Ambiente di un processo (contenuto di /proc/PID/environ), o lista vuota
    /// se non si legge.
    /// </summary>
    private static byte[] ReadEnviron(int pid)
    {
        try
        {
            return File.ReadAllBytes($"/proc/{pid}/environ");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return [];
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

    /// <summary>
    /// Gaming Mode del Steam Deck: Steam è la schermata e la sessione lo riavvia se si chiude.
    /// </summary>
    public bool IsSteamGamingMode =>
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SteamGamepadUI"))
        || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GAMESCOPE_WAYLAND_DISPLAY"));

    /// <summary>
    /// "steam -shutdown" chiude Steam. Poi si aspetta che non ci sia più. false anche se il comando
    /// steam non c'è (Flatpak, Snap).
    /// </summary>
    public bool CloseSteam()
    {
        if (!IsSteamRunning())
            return true;

        if (!StartProgram("steam", "-shutdown", ""))
            return false;

        // Ogni decimo di secondo, fino a 30 secondi: in Gaming Mode la sessione riavvia Steam subito, e
        // il momento in cui non c'è è breve.
        for (int i = 0; i < 300; i++)
        {
            if (!IsSteamRunning())
                return true;

            Thread.Sleep(100);
        }

        return false;
    }

    /// <summary>
    /// true se c'è un processo con quel nome, anche di un altro utente:
    /// Process.GetProcessesByName non filtra per utente.
    /// </summary>
    public bool IsProcessRunning(string name)
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
    public void OpenSteamUri(string uri) => OpenUri(uri);

    /// <summary>
    /// Apre un indirizzo con xdg-open, che lo passa al programma registrato.
    /// </summary>
    public void OpenUri(string uri)
    {
        // Un indirizzo steam:// va al Steam che gira, anche in Gaming Mode sul Steam Deck dove xdg-open può non
        // avere il programma registrato. Senza il comando steam (Flatpak, Snap) si torna a xdg-open.
        if (uri.StartsWith("steam://", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                Process.Start(new ProcessStartInfo("steam", uri) { UseShellExecute = false })?.Dispose();
                return;
            }
            catch (System.ComponentModel.Win32Exception)
            {
                // Il comando steam non c'è: sotto, xdg-open.
            }
        }

        Process.Start(new ProcessStartInfo("xdg-open", uri) { UseShellExecute = false })?.Dispose();
    }

    /// <summary>
    /// Avvia un programma. false se non parte (per esempio un .exe senza Wine).
    /// </summary>
    public bool StartProgram(string exe, string arguments, string workingDir)
    {
        try
        {
            Process.Start(new ProcessStartInfo(exe, arguments) { WorkingDirectory = workingDir, UseShellExecute = false })?.Dispose();
            return true;
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return false;
        }
    }

    /// <summary>
    /// Il registro di Windows non esiste qui: sempre null.
    /// </summary>
    public string? ReadRegistryString(string key, string name) => null;

    /// <summary>
    /// Il registro di Windows non esiste qui: sempre lista vuota.
    /// </summary>
    public IReadOnlyList<string> RegistrySubKeys(string key) => [];

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
