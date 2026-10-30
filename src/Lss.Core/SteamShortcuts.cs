using System.Globalization;

namespace Lss;

/// <summary>
/// I giochi non Steam aggiunti a Steam (userdata\ID\config\shortcuts.vdf, VDF binario).
/// Stanno nel gruppo di Steam; i launcher già gestiti (Heroic, Epic, GOG) sono esclusi,
/// perché il gioco è già nella loro lista.
/// </summary>
internal static class SteamShortcuts
{
    /// <summary>Una voce di shortcuts.vdf.</summary>
    /// <param name="AppId">ID Steam del collegamento, senza segno (il file può avere un int32 negativo).</param>
    /// <param name="Name">Nome del gioco.</param>
    /// <param name="StartDir">Cartella di avvio, senza virgolette.</param>
    /// <param name="Exe">Eseguibile, senza virgolette.</param>
    /// <param name="Icon">Icona (un'immagine o un .exe), senza virgolette; "" se non c'è.</param>
    public sealed record Shortcut(uint AppId, string Name, string StartDir, string Exe, string Icon);

    // Eseguibili di launcher che hanno già la loro voce (nome senza .exe, minuscolo).
    private static readonly string[] Launchers = ["heroic", "epicgameslauncher", "galaxyclient"];

    /// <summary>
    /// Le voci di shortcuts.vdf di un account. Vuoto se il file manca o è rotto.
    /// </summary>
    public static IReadOnlyList<Shortcut> Read(string accountFolder)
    {
        string file = Path.Combine(accountFolder, "config", "shortcuts.vdf");
        var shortcuts = new List<Shortcut>();

        if (!File.Exists(file))
            return shortcuts;

        try
        {
            using var reader = new BinaryReader(File.OpenRead(file));
            Vdf.Node root = BinaryVdf.Read(reader, null);

            if (root.GetValueOrDefault("shortcuts") is not Vdf.Node list)
                return shortcuts;

            foreach (object entry in list.Values)
            {
                if (entry is not Vdf.Node node)
                    continue;

                string exe = Text(node, "Exe").Trim('"');

                if (IsLauncher(exe))
                    continue;

                if (!long.TryParse(Text(node, "appid"), NumberStyles.Integer, CultureInfo.InvariantCulture, out long appId))
                    continue;

                shortcuts.Add(new Shortcut(unchecked((uint)appId), Text(node, "AppName"), Text(node, "StartDir").Trim('"'),
                    exe, Text(node, "icon").Trim('"')));
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            Log.Write("File dei collegamenti di Steam non letto: " + file, ex);
            return [];
        }

        return shortcuts;
    }

    /// <summary>
    /// Indirizzo steam:// che avvia il collegamento (il gioco ha l'ID a 64 bit con il
    /// bit 0x02000000 e l'ID del collegamento nella parte alta).
    /// </summary>
    public static string LaunchUri(uint appId) =>
        "steam://rungameid/" + (((ulong)appId << 32) | 0x02000000UL).ToString(CultureInfo.InvariantCulture);

    private static bool IsLauncher(string exe)
    {
        string name = Path.GetFileNameWithoutExtension(exe).ToLowerInvariant();

        return Launchers.Contains(name);
    }

    private static string Text(Vdf.Node node, string key) => node.GetValueOrDefault(key) as string ?? "";
}
