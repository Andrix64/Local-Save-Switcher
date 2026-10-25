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
    /// true se Steam segna il gioco come aperto, o se un processo gira dalla
    /// cartella di installazione. I processi di cui non si legge il percorso
    /// (per esempio avviati come amministratore) sono ignorati: per quelli
    /// Steam segna comunque il gioco come aperto.
    /// </summary>
    public bool IsGameRunning(GameInfo game) =>
        SteamSaysRunning(game.Id) || FindGameProcesses(game).Count > 0;

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
    /// Processi che girano dalla cartella di installazione del gioco, con la
    /// data di avvio se si legge. Nessun processo se il gioco non è installato.
    /// </summary>
    private List<(int Id, DateTime? Start)> FindGameProcesses(GameInfo game)
    {
        var found = new List<(int Id, DateTime? Start)>();

        if (game.InstallPath == "")
            return found;

        string prefix = Path.GetFullPath(game.InstallPath).TrimEnd('\\') + "\\";

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
        FindGameProcesses(game).Select(p => p.Start).Min();

    /// <summary>
    /// Con Steam aperto, Steam Cloud può risincronizzare i salvataggi
    /// e annullare un cambio di profilo.
    /// </summary>
    public bool IsSteamRunning()
    {
        return IsProcessRunning("steam");
    }

    private static bool IsProcessRunning(string name)
    {
        Process[] processes = Process.GetProcessesByName(name);

        foreach (Process process in processes)
            process.Dispose();

        return processes.Length > 0;
    }

    public void OpenSteamUri(string uri)
    {
        Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true })?.Dispose();
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
