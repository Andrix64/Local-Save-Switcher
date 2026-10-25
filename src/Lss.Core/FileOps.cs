namespace Lss;

/// <summary>
/// Copia e sincronizzazione di cartelle.
/// I metodi di copia lanciano un'eccezione al primo errore, anche nelle
/// sottocartelle, così un backup incompleto non viene mai considerato riuscito.
/// L'unica eccezione è il file di Steam Cloud al primo livello della cartella.
/// </summary>
internal static class FileOps
{
    // File di Steam Cloud, al primo livello della cartella del gioco: non va mai
    // copiato né cancellato. Un file con lo stesso nome più in basso è un file
    // come gli altri.
    private const string SteamCacheFile = "remotecache.vdf";

    // Cartella al primo livello di uno snapshot con i posti extra del gioco
    // (.lss-posizioni/<id>): non è mai parte della partita, va solo tenuta.
    public const string ExtraSpotsFolder = ".lss-posizioni";

    // Cartelle accanto alla destinazione durante ReplaceFolder.
    public const string TempSuffix = ".nuovo";
    public const string OldSuffix = ".vecchio";

    /// <summary>
    /// Copia tutta la cartella, posti extra compresi (da uno snapshot a un altro).
    /// </summary>
    public static void CopyFolder(string source, string destination)
    {
        if (!Directory.Exists(source))
            throw new DirectoryNotFoundException("Cartella non trovata: " + source);

        CopyFolder(source, destination, topLevel: true, carryExtraSpots: true);
    }

    /// <param name="carryExtraSpots">
    /// false: i posti extra di source non si copiano (la copia va nel gioco).
    /// </param>
    private static void CopyFolder(string source, string destination, bool topLevel, bool carryExtraSpots)
    {
        Directory.CreateDirectory(destination);

        foreach (string file in Directory.GetFiles(source))
        {
            string name = Path.GetFileName(file);

            if (IsSteamCache(name, topLevel))
                continue;

            File.Copy(file, Path.Combine(destination, name), true);
        }

        foreach (string dir in Directory.GetDirectories(source))
        {
            string name = Path.GetFileName(dir);

            if (!carryExtraSpots && IsExtraSpots(name, topLevel))
                continue;

            CopyFolder(dir, Path.Combine(destination, name), topLevel: false, carryExtraSpots);
        }
    }

    /// <summary>
    /// Rende destination identica a source, cancellando ciò che manca in source.
    /// I posti extra di destination non si toccano.
    /// </summary>
    public static void MirrorFolder(string source, string destination)
    {
        if (!Directory.Exists(source))
            throw new DirectoryNotFoundException("Cartella non trovata: " + source);

        MirrorFolder(source, destination, topLevel: true, carryExtraSpots: false);
    }

    /// <param name="carryExtraSpots">
    /// false: i posti extra non si copiano né si cancellano (sono di destination).
    /// </param>
    private static void MirrorFolder(string source, string destination, bool topLevel, bool carryExtraSpots)
    {
        Directory.CreateDirectory(destination);

        // Elimina i file che non esistono più nella sorgente.
        foreach (string file in Directory.GetFiles(destination))
        {
            string name = Path.GetFileName(file);

            if (IsSteamCache(name, topLevel))
                continue;

            if (!File.Exists(Path.Combine(source, name)))
                File.Delete(file);
        }

        // Elimina le cartelle non presenti nella sorgente (i posti extra no).
        foreach (string dir in Directory.GetDirectories(destination))
        {
            string name = Path.GetFileName(dir);

            if (!carryExtraSpots && IsExtraSpots(name, topLevel))
                continue;

            if (!Directory.Exists(Path.Combine(source, name)))
                Directory.Delete(dir, true);
        }

        // Copia i file aggiornati.
        foreach (string file in Directory.GetFiles(source))
        {
            string name = Path.GetFileName(file);

            if (IsSteamCache(name, topLevel))
                continue;

            File.Copy(file, Path.Combine(destination, name), true);
        }

        // Sincronizza le sottocartelle.
        foreach (string dir in Directory.GetDirectories(source))
        {
            string name = Path.GetFileName(dir);

            if (!carryExtraSpots && IsExtraSpots(name, topLevel))
                continue;

            MirrorFolder(dir, Path.Combine(destination, name), topLevel: false, carryExtraSpots);
        }
    }

    /// <summary>
    /// Copia source in destination senza i posti extra che source ha in .lss-posizioni,
    /// e controlla la copia. Serve per portare un posto da uno zip o da un altro snapshot.
    /// </summary>
    public static void CopyWithoutExtraSpotsVerified(string source, string destination)
    {
        if (!Directory.Exists(source))
            throw new DirectoryNotFoundException("Cartella non trovata: " + source);

        CopyFolder(source, destination, topLevel: true, carryExtraSpots: false);
        Verify(source, destination);
    }

    /// <summary>
    /// Copia source in destination e controlla che la copia coincida.
    /// </summary>
    public static void CopyFolderVerified(string source, string destination)
    {
        CopyFolder(source, destination);
        Verify(source, destination, carryExtraSpots: true);
    }

    /// <summary>
    /// Rende destination identica a source in due passaggi: copia source in una
    /// cartella temporanea accanto a destination, la verifica e poi scambia le
    /// cartelle. Se qualcosa va storto prima dello scambio, destination resta
    /// com'era. Il file di Steam Cloud di destination viene conservato.
    /// </summary>
    /// <param name="carryExtraSpots">
    /// false (il caso del gioco): i posti extra di source non entrano in destination,
    /// e quelli di destination restano. true (da uno snapshot a un altro): i posti
    /// extra di source sostituiscono quelli di destination.
    /// </param>
    public static void ReplaceFolder(string source, string destination, bool carryExtraSpots = false)
    {
        string temp = destination + TempSuffix;
        string old = destination + OldSuffix;

        RecoverInterruptedReplace(destination);

        DeleteIfExists(temp);
        DeleteIfExists(old);

        try
        {
            CopyFolder(source, temp, topLevel: true, carryExtraSpots);

            if (Directory.Exists(destination))
                CopyKeptFromDestination(destination, temp, carryExtraSpots);

            Verify(source, temp, carryExtraSpots);

            if (!Directory.Exists(destination))
            {
                Directory.Move(temp, destination);
                return;
            }

            try
            {
                Directory.Move(destination, old);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Cartella in uso (per esempio da Steam): copia diretta dalla
                // copia già verificata.
                MirrorFolder(temp, destination, topLevel: true, carryExtraSpots);
                Verify(source, destination, carryExtraSpots);
                return;
            }

            try
            {
                Directory.Move(temp, destination);
            }
            catch
            {
                Directory.Move(old, destination);
                throw;
            }

            TryDelete(old);
        }
        finally
        {
            TryDelete(temp);
        }
    }

    /// <summary>
    /// Rimette a posto destination dopo una ReplaceFolder interrotta (per
    /// esempio da uno spegnimento). Se manca la cartella ma c'è la copia
    /// .vecchio, la riporta al suo posto: è lo stato di prima dell'operazione,
    /// di cui esiste anche il backup. Se la cartella c'è, elimina gli avanzi
    /// .nuovo e .vecchio.
    /// </summary>
    /// <returns>true se ha trovato e sistemato qualcosa.</returns>
    public static bool RecoverInterruptedReplace(string destination)
    {
        string temp = destination + TempSuffix;
        string old = destination + OldSuffix;
        bool recovered = false;

        // Un'interruzione tra i due spostamenti lascia solo la cartella vecchia.
        if (!Directory.Exists(destination) && Directory.Exists(old))
        {
            Directory.Move(old, destination);
            recovered = true;
        }

        // Senza la cartella, un .nuovo da solo è l'unica copia: si lascia stare.
        if (Directory.Exists(destination))
        {
            foreach (string leftover in new[] { temp, old })
            {
                if (Directory.Exists(leftover))
                {
                    TryDelete(leftover);
                    recovered = true;
                }
            }
        }

        return recovered;
    }

    /// <summary>
    /// File di folder che corrispondono al pattern (maschere separate da ";"): solo
    /// il primo livello, o tutti i livelli se recursive. Il pattern vale sul nome
    /// del file. Il file di Steam Cloud al primo livello e i file temporanei
    /// (.nuovo, .vecchio) non contano. Un pattern vuoto non corrisponde a niente.
    /// </summary>
    public static IReadOnlyList<string> MatchedFiles(string folder, string pattern, bool recursive)
    {
        if (pattern == "" || !Directory.Exists(folder))
            return [];

        SearchOption option = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var result = new List<string>();

        foreach (string file in Directory.GetFiles(folder, "*", option))
        {
            string relative = Path.GetRelativePath(folder, file);
            string name = Path.GetFileName(file);

            if (IsSkipped(relative) || IsTemporary(name))
                continue;

            if (Matches(pattern, name))
                result.Add(file);
        }

        return result.Order(StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// Copia in destination solo i file corrispondenti (con la loro posizione
    /// nelle sottocartelle) e controlla byte per byte ogni copia.
    /// </summary>
    public static void CopyMatchedVerified(string folder, string pattern, bool recursive, string destination)
    {
        IReadOnlyList<string> files = MatchedFiles(folder, pattern, recursive);
        Directory.CreateDirectory(destination);

        foreach (string file in files)
        {
            string copy = Path.Combine(destination, Path.GetRelativePath(folder, file));
            Directory.CreateDirectory(Path.GetDirectoryName(copy)!);
            File.Copy(file, copy, true);
        }

        foreach (string file in files)
        {
            string copy = Path.Combine(destination, Path.GetRelativePath(folder, file));

            if (!File.Exists(copy) || !FilesAreEqual(file, copy))
                throw new IOException("La copia non coincide con l'originale: " + copy);
        }
    }

    /// <summary>
    /// Rende i file corrispondenti di folder identici a quelli di source: copia
    /// ognuno come .nuovo accanto all'originale, verifica tutte le copie, poi le
    /// sostituisce e infine elimina i corrispondenti che source non ha. Gli altri
    /// file di folder non vengono toccati.
    /// </summary>
    public static void ReplaceMatched(string source, string folder, string pattern, bool recursive)
    {
        Dictionary<string, string> wanted = MatchedByName(source, pattern, recursive);
        IReadOnlyList<string> existing = MatchedFiles(folder, pattern, recursive);
        var temps = new List<(string Temp, string Target)>();

        try
        {
            foreach ((string name, string file) in wanted)
            {
                string target = Path.Combine(folder, name);
                string temp = target + TempSuffix;

                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(file, temp, true);
                temps.Add((temp, target));

                if (!FilesAreEqual(file, temp))
                    throw new IOException("La copia non coincide con l'originale: " + temp);
            }
        }
        catch
        {
            foreach ((string temp, _) in temps)
                TryDeleteFile(temp);

            throw;
        }

        foreach ((string temp, string target) in temps)
            File.Move(temp, target, true);

        foreach (string file in existing)
        {
            string relative = Path.GetRelativePath(folder, file);

            if (!wanted.ContainsKey(relative))
                File.Delete(file);
        }
    }

    /// <summary>
    /// true se i file corrispondenti di folder sono gli stessi (nomi e contenuto)
    /// di quelli di snapshot.
    /// </summary>
    public static bool MatchedEqual(string folder, string pattern, bool recursive, string snapshot)
    {
        Dictionary<string, string> mine = MatchedByName(folder, pattern, recursive);
        Dictionary<string, string> theirs = MatchedByName(snapshot, pattern, recursive);

        if (mine.Count != theirs.Count)
            return false;

        foreach ((string name, string file) in mine)
        {
            if (!theirs.TryGetValue(name, out string? other) || !FilesAreEqual(file, other))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Rimette a posto i file .nuovo lasciati da una ReplaceMatched interrotta:
    /// l'originale non viene mai tolto prima della sostituzione, quindi basta
    /// eliminare la copia. Se ne trova, ritorna true.
    /// </summary>
    public static bool RecoverInterruptedMatched(string folder, string pattern, bool recursive)
    {
        if (!Directory.Exists(folder))
            return false;

        SearchOption option = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        bool recovered = false;

        foreach (string temp in Directory.GetFiles(folder, "*" + TempSuffix, option))
        {
            string original = temp[..^TempSuffix.Length];

            if (!Matches(pattern, Path.GetFileName(original)))
                continue;

            File.Delete(temp);
            recovered = true;
        }

        return recovered;
    }

    /// <summary>
    /// I file corrispondenti di folder, con il nome relativo (chiave) e il percorso.
    /// </summary>
    private static Dictionary<string, string> MatchedByName(string folder, string pattern, bool recursive)
    {
        var result = new Dictionary<string, string>(NameComparer());

        foreach (string file in MatchedFiles(folder, pattern, recursive))
            result[Path.GetRelativePath(folder, file)] = file;

        return result;
    }

    /// <summary>
    /// Confronto dei nomi relativi: senza maiuscole su Windows.
    /// </summary>
    private static StringComparer NameComparer() =>
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    /// <summary>
    /// true se il nome corrisponde a una delle maschere del pattern. Senza maiuscole su Windows.
    /// </summary>
    private static bool Matches(string pattern, string name) =>
        pattern.Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Any(mask => Glob.IsMatch(mask, name, OperatingSystem.IsWindows()));

    /// <summary>
    /// true se il percorso relativo non è un file da confrontare: il file di Steam
    /// Cloud al primo livello, o qualcosa dentro i posti extra.
    /// </summary>
    private static bool IsSkipped(string relative) =>
        string.Equals(relative, SteamCacheFile, StringComparison.OrdinalIgnoreCase)
        || IsExtraSpots(relative.Split(Path.DirectorySeparatorChar)[0], topLevel: true);

    /// <summary>
    /// true se il nome è la cartella dei posti extra, al primo livello.
    /// </summary>
    private static bool IsExtraSpots(string name, bool topLevel) =>
        topLevel && string.Equals(name, ExtraSpotsFolder, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// true se il nome è un file temporaneo di una sostituzione (.nuovo o .vecchio).
    /// </summary>
    private static bool IsTemporary(string name) =>
        name.EndsWith(TempSuffix, StringComparison.Ordinal) || name.EndsWith(OldSuffix, StringComparison.Ordinal);

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (IOException)
        {
            // Resta un file .nuovo: RecoverInterruptedMatched lo elimina alla prossima operazione.
        }
    }

    private static void Verify(string source, string destination, bool carryExtraSpots = false)
    {
        if (!FoldersAreEqual(source, destination))
            throw new IOException("La copia non coincide con l'originale: " + destination);

        string extra = Path.Combine(source, ExtraSpotsFolder);

        if (carryExtraSpots && Directory.Exists(extra)
            && !FoldersAreEqual(extra, Path.Combine(destination, ExtraSpotsFolder)))
            throw new IOException("La copia dei posti extra non coincide: " + destination);
    }

    /// <summary>
    /// Copia nella destinazione ciò che la sostituzione non tocca: il file di
    /// Steam Cloud di source e, se i posti extra non si portano, la cartella dei
    /// posti extra di source.
    /// </summary>
    private static void CopyKeptFromDestination(string source, string destination, bool carryExtraSpots)
    {
        string cache = Path.Combine(source, SteamCacheFile);

        if (File.Exists(cache))
            File.Copy(cache, Path.Combine(destination, SteamCacheFile), true);

        string extra = Path.Combine(source, ExtraSpotsFolder);

        if (!carryExtraSpots && Directory.Exists(extra))
            CopyAll(extra, Path.Combine(destination, ExtraSpotsFolder));
    }

    /// <summary>
    /// Copia tutto, senza esclusioni, e controlla byte per byte ogni file copiato.
    /// </summary>
    public static void CopyAllVerified(string source, string destination)
    {
        CopyAll(source, destination);

        foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            string copy = Path.Combine(destination, Path.GetRelativePath(source, file));

            if (!File.Exists(copy) || !FilesAreEqual(file, copy))
                throw new IOException("La copia non coincide con l'originale: " + copy);
        }
    }

    /// <summary>
    /// Copia tutto, senza esclusioni.
    /// </summary>
    private static void CopyAll(string source, string destination)
    {
        Directory.CreateDirectory(destination);

        foreach (string file in Directory.GetFiles(source))
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), true);

        foreach (string dir in Directory.GetDirectories(source))
            CopyAll(dir, Path.Combine(destination, Path.GetFileName(dir)));
    }

    private static void DeleteIfExists(string path)
    {
        if (Directory.Exists(path))
            Directory.Delete(path, true);
    }

    private static void TryDelete(string path)
    {
        try
        {
            DeleteIfExists(path);
        }
        catch
        {
            // Resta una cartella temporanea: viene eliminata alla prossima operazione.
        }
    }

    /// <summary>
    /// true se la cartella non contiene file di salvataggio (a qualsiasi livello).
    /// </summary>
    public static bool HasNoSaveFiles(string path)
    {
        if (!Directory.Exists(path))
            return true;

        string cache = Path.Combine(path, SteamCacheFile);

        return !Directory
            .EnumerateFiles(path, "*", SearchOption.AllDirectories)
            .Any(file => !string.Equals(file, cache, StringComparison.OrdinalIgnoreCase)
                         && !IsSkipped(Path.GetRelativePath(path, file)));
    }

    /// <summary>
    /// true se le due cartelle contengono gli stessi file con lo stesso contenuto,
    /// a parte il file di Steam Cloud al primo livello, come CopyFolder e MirrorFolder.
    /// </summary>
    public static bool FoldersAreEqual(string first, string second)
    {
        if (!Directory.Exists(first) || !Directory.Exists(second))
            return Directory.Exists(first) == Directory.Exists(second);

        return FoldersAreEqual(first, second, topLevel: true);
    }

    private static bool FoldersAreEqual(string first, string second, bool topLevel)
    {
        List<string> files = ListNames(Directory.GetFiles(first), name => !IsSteamCache(name, topLevel));

        if (!files.SequenceEqual(
                ListNames(Directory.GetFiles(second), name => !IsSteamCache(name, topLevel)),
                StringComparer.OrdinalIgnoreCase))
            return false;

        foreach (string name in files)
        {
            if (!FilesAreEqual(Path.Combine(first, name), Path.Combine(second, name)))
                return false;
        }

        List<string> dirs = ListNames(Directory.GetDirectories(first), name => !IsExtraSpots(name, topLevel));

        if (!dirs.SequenceEqual(
                ListNames(Directory.GetDirectories(second), name => !IsExtraSpots(name, topLevel)),
                StringComparer.OrdinalIgnoreCase))
            return false;

        return dirs.All(name =>
            FoldersAreEqual(Path.Combine(first, name), Path.Combine(second, name), topLevel: false));
    }

    private static List<string> ListNames(string[] paths, Func<string, bool> include)
    {
        return paths
            .Select(Path.GetFileName)
            .OfType<string>()
            .Where(include)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool FilesAreEqual(string first, string second)
    {
        if (new FileInfo(first).Length != new FileInfo(second).Length)
            return false;

        using FileStream a = File.OpenRead(first);
        using FileStream b = File.OpenRead(second);

        byte[] bufferA = new byte[81920];
        byte[] bufferB = new byte[81920];

        while (true)
        {
            int readA = a.ReadAtLeast(bufferA, bufferA.Length, throwOnEndOfStream: false);
            int readB = b.ReadAtLeast(bufferB, bufferB.Length, throwOnEndOfStream: false);

            if (readA != readB ||
                !bufferA.AsSpan(0, readA).SequenceEqual(bufferB.AsSpan(0, readB)))
                return false;

            if (readA == 0)
                return true;
        }
    }

    private static bool IsSteamCache(string name, bool topLevel)
    {
        return topLevel && string.Equals(name, SteamCacheFile, StringComparison.OrdinalIgnoreCase);
    }
}
