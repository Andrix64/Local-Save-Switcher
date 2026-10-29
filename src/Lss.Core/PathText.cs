namespace Lss;

/// <summary>
/// Percorsi di cartelle e file scritti in modi diversi (separatori, doppi, finale)
/// che vanno confrontati: si portano tutti alla stessa forma.
/// </summary>
internal static class PathText
{
    /// <summary>
    /// Ogni \ e / diventa il separatore del sistema, i separatori doppi uno solo (non l'inizio
    /// \\server\share di un percorso di rete), niente separatore finale (tranne una radice
    /// come C:\ o /). Solo per percorsi di cartelle e file, non per le maschere dei file.
    /// </summary>
    public static string Normalize(string path)
    {
        if (path == "")
            return path;

        char sep = Path.DirectorySeparatorChar;
        string text = path.Replace('\\', sep).Replace('/', sep);

        // Su Windows \server\share resta di rete; su Linux // iniziale vale come /.
        bool network = OperatingSystem.IsWindows() && text.StartsWith("" + sep + sep, StringComparison.Ordinal);
        string rest = network ? text[2..] : text;

        var parts = rest.Split(sep);
        var kept = new List<string>();

        for (int i = 0; i < parts.Length; i++)
        {
            // Una parte vuota in testa è la radice di un percorso assoluto; le altre sono doppi separatori.
            if (parts[i] == "" && i > 0)
                continue;

            kept.Add(parts[i]);
        }

        string result = string.Join(sep, kept);

        if (network)
            result = "" + sep + sep + result;

        // C: e C:\ sono la radice; "" + sep (da un percorso fatto solo di separatori) è la radice di Linux.
        if (result.Length >= 2 && result[1] == ':' && result.Length == 2 && text.Length > 2)
            return result + sep;

        if (result == "")
            return text.Length > 0 ? "" + sep : "";

        return result;
    }

    /// <summary>true se i due percorsi sono la stessa cartella o lo stesso file (senza maiuscole su Windows).</summary>
    public static bool Same(string first, string second) =>
        string.Equals(Normalize(first), Normalize(second),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
}
