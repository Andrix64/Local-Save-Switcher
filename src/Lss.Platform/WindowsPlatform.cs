using Microsoft.Win32;
using System.Diagnostics;

namespace Lss;

/// <summary>
/// IPlatform per Windows: cartella di Steam dal registro, processi e istanza
/// unica tramite mutex.
/// </summary>
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public sealed class WindowsPlatform : IPlatform
{
    // Percorso dell'eseguibile per ogni processo già visto: leggerlo è lento e
    // il controllo gira ogni 2 secondi. null = percorso non leggibile.
    private readonly Dictionary<int, string?> processPaths = [];
    private readonly object processLock = new();

    /// <summary>
    /// Cartella di Steam dal registro o dai percorsi standard. Lista vuota se
    /// Steam non è installato.
    /// </summary>
    public IReadOnlyList<string> FindSteamRoots()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");

            string? path = key?.GetValue("SteamPath") as string;

            if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
                return [path];
        }
        catch
        {
            // Se il registro non è disponibile, prova i percorsi standard.
        }

        string[] paths =
        {
            @"C:\Program Files (x86)\Steam",
            @"C:\Program Files\Steam"
        };

        return paths.Where(Directory.Exists).Take(1).ToList();
    }

    /// <summary>
    /// Cartelle dell'utente di Windows. Le cartelle che Windows non ha (quelle
    /// XDG) sono "".
    /// </summary>
    public KnownFolders GetKnownFolders()
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        return new KnownFolders(
            Home: home,
            AppData: Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            LocalAppData: Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            LocalAppDataLow: Path.Combine(home, "AppData", "LocalLow"),
            Documents: Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            SavedGames: Path.Combine(home, "Saved Games"),
            Public: Environment.GetEnvironmentVariable("PUBLIC") ?? "",
            ProgramData: Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            XdgData: "",
            XdgConfig: "",
            UserName: Environment.UserName);
    }

    /// <summary>
    /// true se Steam segna il gioco come aperto, o se un processo gira dalla
    /// cartella di installazione. I processi di cui non si legge il percorso
    /// (per esempio avviati come amministratore) sono ignorati: per quelli
    /// Steam segna comunque il gioco come aperto.
    /// </summary>
    public bool IsGameRunning(GameInfo game) =>
        SteamSaysRunning(game.Id) || FindProcessesUnder(game.InstallPath).Count > 0;

    /// <summary>
    /// Steam scrive nel registro quale gioco è in esecuzione: RunningAppID per
    /// quello in primo piano, Apps\ID\Running per tutti. Un errore di lettura
    /// vuol dire "non aperto".
    /// </summary>
    private static bool SteamSaysRunning(string appId)
    {
        try
        {
            using RegistryKey? steam = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");

            if (steam?.GetValue("RunningAppID") is int running && running.ToString() == appId)
                return true;

            using RegistryKey? app = steam?.OpenSubKey(@"Apps\" + appId);

            return app?.GetValue("Running") is int value && value == 1;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Processi che girano dalla cartella indicata, con la data di avvio se si
    /// legge. Nessun processo se la cartella è "".
    /// </summary>
    public IReadOnlyList<(int Id, DateTime? Start)> FindProcessesUnder(string folder)
    {
        var found = new List<(int Id, DateTime? Start)>();

        if (folder == "")
            return found;

        string prefix = Path.GetFullPath(folder).TrimEnd('\\') + "\\";

        lock (processLock)
        {
            Process[] all = Process.GetProcesses();
            var seen = new HashSet<int>();

            foreach (Process process in all)
            {
                try
                {
                    seen.Add(process.Id);

                    if (!processPaths.TryGetValue(process.Id, out string? path))
                    {
                        path = ReadProcessPath(process);
                        processPaths[process.Id] = path;
                    }

                    if (path != null && path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                        found.Add((process.Id, TryStartTime(process)));
                }
                finally
                {
                    process.Dispose();
                }
            }

            // Processi chiusi nel frattempo: via dalla cache.
            foreach (int id in processPaths.Keys.Where(id => !seen.Contains(id)).ToList())
                processPaths.Remove(id);
        }

        return found;
    }

    private static string? ReadProcessPath(Process process)
    {
        try
        {
            return process.MainModule?.FileName;
        }
        catch
        {
            // Percorso non leggibile: il processo non viene contato.
            return null;
        }
    }

    private static DateTime? TryStartTime(Process process)
    {
        try
        {
            return process.StartTime;
        }
        catch
        {
            // Processo già chiuso o non leggibile.
            return null;
        }
    }

    /// <summary>
    /// Quando è partito il gioco in esecuzione, se si riesce a leggerlo.
    /// </summary>
    public DateTime? GameStartTime(GameInfo game) =>
        FindProcessesUnder(game.InstallPath).Select(p => p.Start).Min();

    /// <summary>
    /// Con Steam aperto, Steam Cloud può risincronizzare i salvataggi
    /// e annullare un cambio di profilo.
    /// </summary>
    public bool IsSteamRunning()
    {
        return IsProcessRunning("steam");
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

    public void OpenSteamUri(string uri) => OpenUri(uri);

    public void OpenUri(string uri)
    {
        Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true })?.Dispose();
    }

    /// <summary>
    /// Avvia un programma. false se non parte (file mancante, non eseguibile).
    /// </summary>
    public bool StartProgram(string exe, string arguments, string workingDir)
    {
        try
        {
            Process.Start(new ProcessStartInfo(exe, arguments) { WorkingDirectory = workingDir, UseShellExecute = true })?.Dispose();
            return true;
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return false;
        }
    }

    /// <summary>
    /// Valore stringa del registro, o null se la chiave o il valore non ci sono.
    /// </summary>
    public string? ReadRegistryString(string key, string name)
    {
        try
        {
            using RegistryKey? found = OpenRegistryKey(key);

            return found?.GetValue(name) as string;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Nomi delle sottochiavi di una chiave del registro, o lista vuota.
    /// </summary>
    public IReadOnlyList<string> RegistrySubKeys(string key)
    {
        try
        {
            using RegistryKey? found = OpenRegistryKey(key);

            return found?.GetSubKeyNames() ?? [];
        }
        catch
        {
            return [];
        }
    }

    /// <summary>
    /// Apre una chiave da un percorso completo, come
    /// HKEY_LOCAL_MACHINE\SOFTWARE\...; null se la radice non è riconosciuta.
    /// </summary>
    private static RegistryKey? OpenRegistryKey(string key)
    {
        int slash = key.IndexOf('\\');

        if (slash < 0)
            return null;

        RegistryKey? root = key[..slash] switch
        {
            "HKEY_LOCAL_MACHINE" => Registry.LocalMachine,
            "HKEY_CURRENT_USER" => Registry.CurrentUser,
            "HKEY_CLASSES_ROOT" => Registry.ClassesRoot,
            "HKEY_USERS" => Registry.Users,
            "HKEY_CURRENT_CONFIG" => Registry.CurrentConfig,
            _ => null
        };

        return root?.OpenSubKey(key[(slash + 1)..]);
    }

    public IDisposable? AcquireSingleInstance()
    {
        // Il mutex di Control Save Manager impedisce di aprire la sua vecchia
        // versione mentre LSS è aperto (vedi LegacyMigration); quello di LSS
        // impedisce due LSS insieme.
        var legacy = new Mutex(true, LegacyMigration.MutexName, out bool legacyFree);

        if (!legacyFree)
        {
            legacy.Dispose();
            return null;
        }

        var mutex = new Mutex(true, @"Local\LSS", out bool free);

        if (free)
            return new InstanceLock([legacy, mutex]);

        legacy.Dispose();
        mutex.Dispose();
        return null;
    }

    /// <summary>
    /// I mutex tenuti da AcquireSingleInstance: si rilasciano insieme.
    /// </summary>
    private sealed class InstanceLock(List<Mutex> mutexes) : IDisposable
    {
        public void Dispose()
        {
            foreach (Mutex mutex in mutexes)
                mutex.Dispose();
        }
    }
}
