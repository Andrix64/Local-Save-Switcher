using System.Globalization;

namespace Lss;

/// <summary>
/// Registro delle operazioni in log.txt nella cartella dei dati dell'app.
/// Non lancia mai eccezioni: un registro non scrivibile non deve bloccare l'app.
/// </summary>
internal static class Log
{
    // Oltre questa dimensione il registro diventa log.old.txt e si riparte.
    private const long MaxSize = 1024 * 1024;

    private static readonly object Lock = new();

    private static string? filePath;

    /// <summary>
    /// Segue AppData.Root, che può cambiare all'avvio. Impostabile per i test.
    /// </summary>
    public static string FilePath
    {
        get => filePath ?? Path.Combine(AppData.Root, "log.txt");
        set => filePath = value;
    }

    public static void Write(string message)
    {
        string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + "  " +
                      message.Replace("\r", "").Replace("\n", " | ");

        lock (Lock)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);

                if (File.Exists(FilePath) && new FileInfo(FilePath).Length > MaxSize)
                    File.Move(FilePath, Path.ChangeExtension(FilePath, ".old.txt"), true);

                File.AppendAllText(FilePath, line + Environment.NewLine);
            }
            catch
            {
                // Registro non scrivibile: l'operazione continua comunque.
            }
        }
    }

    /// <summary>
    /// Scrive l'esito di un'operazione e lo restituisce invariato.
    /// </summary>
    public static Result Write(string operation, Result result)
    {
        string outcome = result.Success ? "OK" : result.Critical ? "ERRORE GRAVE" : "ERRORE";
        string backup = result.BackupPath != null ? "  Backup: " + result.BackupPath : "";

        Write($"{operation}  {outcome}  {result.Message}{backup}");
        return result;
    }

    public static void Write(string context, Exception ex)
    {
        Write(context + ": " + ex);
    }
}
