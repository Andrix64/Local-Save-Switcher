using System.Globalization;

namespace Lss;

/// <summary>
/// Le operazioni sui salvataggi del gioco, per tutti i posti insieme. Il posto 1
/// sta in cima allo snapshot; il posto n in .lss-posizioni/n. Un posto extra che
/// manca nello snapshot viene lasciato com'è (vedi GameEquals e ReplaceGameFrom):
/// uno snapshot fatto prima del posto non lo perde.
/// I file condivisi (E4) stanno solo nel posto 1: lo gestisce SaveManager.
/// Il cambio di salvataggi (ReplaceGameFrom) scrive un journal (cambio.ini)
/// prima di toccare qualcosa: se il computer si spegne a metà, RecoverInterrupted
/// riporta i posti come erano prima, dal backup.
/// </summary>
internal sealed class SaveSet
{
    // Chiavi del journal (cambio.ini).
    private const string SnapshotKey = "Snapshot";
    private const string BackupKey = "Backup";
    private const string PlannedKey = "Posti";
    private const string DoneKey = "Fatti";

    private readonly IReadOnlyList<SaveSpot> spots;

    public SaveSet(IReadOnlyList<SaveSpot> spots)
    {
        this.spots = spots.OrderBy(spot => spot.Id).ToList();
    }

    /// <summary>
    /// Copia tutti i posti del gioco nello snapshot, e controlla la copia. Un posto
    /// extra la cui cartella non c'è (scheda SD tolta, cartella cancellata) non si
    /// copia: nello snapshot resta assente, quindi "lasciato com'è".
    /// </summary>
    public void CopyGameTo(string snapshot)
    {
        foreach (SaveSpot spot in spots)
        {
            if (IsMissingExtra(spot))
                continue;

            string target = SnapshotFolder(snapshot, spot);

            if (spot.IsWholeFolder)
                FileOps.CopyFolderVerified(spot.Folder, target);
            else
                FileOps.CopyMatchedVerified(spot.Folder, spot.Pattern, spot.Recursive, target);
        }
    }

    /// <summary>
    /// Rende ogni posto del gioco identico allo snapshot. Un posto extra assente
    /// nello snapshot non si tocca. Prima scrive il journal in journal; se un
    /// posto fallisce rimette a posto quelli già fatti dal backup, elimina il
    /// journal e rilancia l'errore. A fine riuscita elimina il journal.
    /// </summary>
    public void ReplaceGameFrom(string snapshot, string backup, string journal)
    {
        // Lo snapshot deve avere il posto 1: senza, non si tocca nulla.
        if (!Directory.Exists(snapshot))
            throw new DirectoryNotFoundException("Snapshot non trovato: " + snapshot);

        IReadOnlyList<SaveSpot> planned = spots
            .Where(spot => Directory.Exists(SnapshotFolder(snapshot, spot)))
            .ToList();

        WriteJournal(journal, snapshot, backup, planned, []);

        var done = new List<SaveSpot>();

        try
        {
            foreach (SaveSpot spot in planned)
            {
                ReplaceSpot(spot, SnapshotFolder(snapshot, spot));
                done.Add(spot);
                WriteJournal(journal, snapshot, backup, planned, done);
            }
        }
        catch
        {
            // Se il ripristino fallisce, il journal resta: il prossimo avvio riprova.
            RestoreFromBackup(done, backup);
            File.Delete(journal);
            throw;
        }

        File.Delete(journal);
    }

    /// <summary>
    /// Rende lo snapshot identico al gioco, posto per posto. Un posto extra la cui
    /// cartella non c'è non tocca lo snapshot: altrimenti salvare i progressi
    /// svuoterebbe la copia della partita.
    /// </summary>
    public void ReplaceSnapshotFromGame(string snapshot)
    {
        foreach (SaveSpot spot in spots)
        {
            if (IsMissingExtra(spot))
                continue;

            string target = SnapshotFolder(snapshot, spot);

            if (spot.IsWholeFolder)
            {
                FileOps.ReplaceFolder(spot.Folder, target);
            }
            else
            {
                Directory.CreateDirectory(target);
                FileOps.ReplaceMatched(spot.Folder, target, spot.Pattern, spot.Recursive);
            }
        }
    }

    /// <summary>
    /// true se ogni posto del gioco è uguale allo snapshot. Un posto extra assente
    /// dallo snapshot conta come uguale.
    /// </summary>
    public bool GameEquals(string snapshot)
    {
        foreach (SaveSpot spot in spots)
        {
            string source = SnapshotFolder(snapshot, spot);

            if (spot.Id != 1 && (!Directory.Exists(source) || !Directory.Exists(spot.Folder)))
                continue;

            bool equal = spot.IsWholeFolder
                ? FileOps.FoldersAreEqual(spot.Folder, source)
                : FileOps.MatchedEqual(spot.Folder, spot.Pattern, spot.Recursive, source);

            if (!equal)
                return false;
        }

        return true;
    }

    /// <summary>true se nessun posto del gioco ha file di salvataggio.</summary>
    public bool GameIsEmpty()
    {
        return spots.All(spot => spot.IsWholeFolder
            ? FileOps.HasNoSaveFiles(spot.Folder)
            : FileOps.MatchedFiles(spot.Folder, spot.Pattern, spot.Recursive).Count == 0);
    }

    /// <summary>
    /// Rimette a posto i posti dopo un cambio interrotto (per esempio da uno
    /// spegnimento). Senza journal non fa nulla. Altrimenti sistema gli avanzi di
    /// ogni posto, riporta dal backup i posti scritti nel journal ed elimina il
    /// journal. Ritorna true se ha ripristinato o sistemato qualcosa.
    /// </summary>
    public bool RecoverInterrupted(string journal, string? backup)
    {
        if (!File.Exists(journal))
            return false;

        Dictionary<string, string> values = IniFile.Read(journal);
        string recordedBackup = backup ?? values.GetValueOrDefault(BackupKey) ?? "";
        bool recovered = false;

        foreach (SaveSpot spot in spots)
        {
            recovered |= spot.IsWholeFolder
                ? FileOps.RecoverInterruptedReplace(spot.Folder)
                : FileOps.RecoverInterruptedMatched(spot.Folder, spot.Pattern, spot.Recursive);
        }

        IReadOnlyList<int> planned = IdsOf(values.GetValueOrDefault(PlannedKey) ?? "")
            .Union(IdsOf(values.GetValueOrDefault(DoneKey) ?? "")).ToList();
        List<SaveSpot> toRestore = spots.Where(spot => planned.Contains(spot.Id)).ToList();

        // Un posto del journal che il gioco non ha più (per esempio tolto): il
        // journal resta, così non si perde l'informazione.
        if (planned.Any(id => toRestore.All(spot => spot.Id != id)))
        {
            Log.Write("Cambio di salvataggi interrotto con posti non più noti: il journal resta " + journal);
            return recovered;
        }

        RestoreFromBackup(toRestore, recordedBackup);
        File.Delete(journal);

        Log.Write("Cambio di salvataggi interrotto: posti rimessi come prima da " + recordedBackup);

        return recovered || toRestore.Count > 0;
    }

    /// <summary>
    /// Sostituisce un posto del gioco con quello dello snapshot (come ReplaceGameFrom).
    /// </summary>
    private static void ReplaceSpot(SaveSpot spot, string source)
    {
        if (spot.IsWholeFolder)
            FileOps.ReplaceFolder(source, spot.Folder);
        else
            FileOps.ReplaceMatched(source, spot.Folder, spot.Pattern, spot.Recursive);
    }

    /// <summary>
    /// Rimette i posti dati dal backup. Se manca il backup del posto 1, lancia
    /// un'eccezione: il journal deve restare. Un posto extra assente dal backup
    /// non c'era nel gioco quando il backup è stato fatto: si lascia com'è.
    /// </summary>
    private void RestoreFromBackup(IEnumerable<SaveSpot> restore, string backup)
    {
        foreach (SaveSpot spot in restore)
        {
            string source = backup == "" ? "" : SnapshotFolder(backup, spot);

            if (spot.Id != 1 && backup != "" && Directory.Exists(backup) && !Directory.Exists(source))
            {
                Log.Write("Posto " + spot.Id + " assente dal backup, lasciato com'è: " + spot.Folder);
                continue;
            }

            if (source == "" || !Directory.Exists(source))
                throw new IOException("Backup mancante per il posto " + spot.Id + ": " + source);

            ReplaceSpot(spot, source);
        }
    }

    /// <summary>
    /// Scrive il journal: lo snapshot, il backup, i posti previsti e quelli già fatti.
    /// </summary>
    private static void WriteJournal(string journal, string snapshot, string backup,
        IEnumerable<SaveSpot> planned, IEnumerable<SaveSpot> done)
    {
        IniFile.Write(journal,
        [
            new(SnapshotKey, snapshot),
            new(BackupKey, backup),
            new(PlannedKey, IdList(planned)),
            new(DoneKey, IdList(done)),
        ]);
    }

    /// <summary>
    /// true per un posto extra la cui cartella nel gioco non c'è: si registra nel log.
    /// </summary>
    private static bool IsMissingExtra(SaveSpot spot)
    {
        if (spot.Id == 1 || Directory.Exists(spot.Folder))
            return false;

        Log.Write("Posto " + spot.Id + " non trovato sul disco, lasciato com'è: " + spot.Folder);
        return true;
    }

    private static string IdList(IEnumerable<SaveSpot> spots) =>
        string.Join(",", spots.Select(spot => spot.Id.ToString(CultureInfo.InvariantCulture)));

    private static IEnumerable<int> IdsOf(string list) =>
        list.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(text => int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int id) ? id : -1)
            .Where(id => id > 0);

    /// <summary>
    /// La cartella di un posto nello snapshot: il posto 1 è la cima, gli altri
    /// .lss-posizioni/&lt;id&gt;.
    /// </summary>
    private static string SnapshotFolder(string snapshot, SaveSpot spot) =>
        spot.Id == 1
            ? snapshot
            : Path.Combine(snapshot, FileOps.ExtraSpotsFolder, spot.Id.ToString(CultureInfo.InvariantCulture));
}
