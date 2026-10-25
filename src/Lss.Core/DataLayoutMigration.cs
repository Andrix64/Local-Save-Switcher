namespace Lss;

/// <summary>
/// Porta le cartelle degli account dal vecchio formato, dati\IDAccount\AppID,
/// al nuovo, dati\steam\IDAccount\AppID (una cartella per launcher). Gira a
/// ogni avvio, prima di leggere i dati, e non tocca ciò che è già a posto.
/// Riguarda le beta di LSS: per Control Save Manager c'è LegacyMigration.
/// </summary>
internal static class DataLayoutMigration
{
    /// <summary>
    /// Sposta sotto steam\ ogni cartella con nome di sole cifre della cartella
    /// dei dati. Finora erano tutte di Steam.
    /// </summary>
    public static void Run(string root)
    {
        if (!Directory.Exists(root))
            return;

        foreach (string dir in Directory.GetDirectories(root)
                     .Where(dir => SaveManager.IsNumeric(Path.GetFileName(dir))))
        {
            try
            {
                MoveAccount(dir, Path.Combine(root, "steam", Path.GetFileName(dir)));
            }
            catch (Exception ex)
            {
                Log.Write("Spostamento di " + dir, ex);
            }
        }
    }

    /// <summary>
    /// Sposta la cartella di un account. Se a destinazione c'è già una copia
    /// identica (uno spostamento interrotto dopo la copia) elimina l'originale;
    /// se la destinazione ha dati diversi non tocca niente.
    /// </summary>
    internal static void MoveAccount(string source, string destination)
    {
        if (Directory.Exists(destination))
        {
            if (FileOps.FoldersAreEqual(source, destination))
            {
                Directory.Delete(source, true);
                Log.Write("Completato uno spostamento interrotto: " + source);
            }
            else
            {
                Log.Write("Non spostato, esiste già: " + destination);
            }

            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

        try
        {
            // Stesso disco: un solo spostamento.
            Directory.Move(source, destination);
        }
        catch (IOException)
        {
            // Disco diverso: si copia e si verifica, e solo dopo si elimina l'originale.
            string temp = destination + ".spostamento";

            if (Directory.Exists(temp))
                Directory.Delete(temp, true);

            FileOps.CopyAllVerified(source, temp);
            Directory.Move(temp, destination);
            Directory.Delete(source, true);
        }

        Log.Write("Dati dell'account spostati in " + destination);
    }
}
