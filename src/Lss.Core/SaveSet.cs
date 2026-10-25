using System.Globalization;

namespace Lss;

/// <summary>
/// Le operazioni sui salvataggi del gioco, per tutti i posti insieme. Il posto 1
/// sta in cima allo snapshot; il posto n in .lss-posizioni/n. Un posto extra che
/// manca nello snapshot viene lasciato com'è (vedi GameEquals e ReplaceGameFrom):
/// uno snapshot fatto prima del posto non lo perde.
/// I file condivisi (E4) stanno solo nel posto 1: lo gestisce SaveManager.
/// </summary>
internal sealed class SaveSet
{
    private readonly IReadOnlyList<SaveSpot> spots;

    public SaveSet(IReadOnlyList<SaveSpot> spots)
    {
        this.spots = spots.OrderBy(spot => spot.Id).ToList();
    }

    /// <summary>Copia tutti i posti del gioco nello snapshot, e controlla la copia.</summary>
    public void CopyGameTo(string snapshot)
    {
        foreach (SaveSpot spot in spots)
        {
            string target = SnapshotFolder(snapshot, spot);

            if (spot.IsWholeFolder)
                FileOps.CopyFolderVerified(spot.Folder, target);
            else
                FileOps.CopyMatchedVerified(spot.Folder, spot.Pattern, spot.Recursive, target);
        }
    }

    /// <summary>
    /// Rende ogni posto del gioco identico allo snapshot. Un posto extra assente
    /// nello snapshot non si tocca.
    /// </summary>
    public void ReplaceGameFrom(string snapshot, string backup, string journal)
    {
        foreach (SaveSpot spot in spots)
        {
            string source = SnapshotFolder(snapshot, spot);

            if (!Directory.Exists(source))
                continue;

            if (spot.IsWholeFolder)
                FileOps.ReplaceFolder(source, spot.Folder);
            else
                FileOps.ReplaceMatched(source, spot.Folder, spot.Pattern, spot.Recursive);
        }
    }

    /// <summary>Rende lo snapshot identico al gioco, posto per posto.</summary>
    public void ReplaceSnapshotFromGame(string snapshot)
    {
        foreach (SaveSpot spot in spots)
        {
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

            if (spot.Id != 1 && !Directory.Exists(source))
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
    /// Rimette a posto i posti dopo uno scambio interrotto. Ancora vuoto: lo
    /// scambio con il journal arriva con N5b.
    /// </summary>
    public void RecoverInterrupted(string journal, string? backup)
    {
    }

    /// <summary>
    /// La cartella di un posto nello snapshot: il posto 1 è la cima, gli altri
    /// .lss-posizioni/&lt;id&gt;.
    /// </summary>
    private static string SnapshotFolder(string snapshot, SaveSpot spot) =>
        spot.Id == 1
            ? snapshot
            : Path.Combine(snapshot, FileOps.ExtraSpotsFolder, spot.Id.ToString(CultureInfo.InvariantCulture));
}
