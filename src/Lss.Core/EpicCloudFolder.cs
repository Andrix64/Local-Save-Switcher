using System.Text.Json;
using System.Text.RegularExpressions;

namespace Lss;

/// <summary>
/// Cartella di salvataggio cloud di un gioco Epic (CloudSaveFolder del catalogo):
/// la traduce in una regola dei salvataggi. Usata da EpicProvider e da HeroicProvider.
/// </summary>
internal static class EpicCloudFolder
{
    // Radici di Epic (token tra graffe, senza distinguere le maiuscole) e radici di LSS.
    private static readonly Dictionary<string, string> Roots = new(StringComparer.OrdinalIgnoreCase)
    {
        ["AppData"] = "WinAppDataLocal",
        ["UserDir"] = "WinMyDocuments",
        ["UserProfile"] = "WinUserProfile",
        ["UserSavedGames"] = "WinSavedGames",
        ["InstallDir"] = "gameinstall",
    };

    // Cartelle vicine a {AppData} dentro AppData: ../locallow e ../roaming.
    private static readonly Dictionary<string, string> AppDataSiblings = new(StringComparer.OrdinalIgnoreCase)
    {
        ["locallow"] = "WinAppDataLocalLow",
        ["roaming"] = "WinAppDataRoaming",
    };

    // Identificatore del gioco: una cartella qualsiasi, con un nome che non si conosce.
    private static readonly Regex EpicIdToken = new(@"\{EpicI[dD]\}", RegexOptions.IgnoreCase);

    /// <summary>
    /// Regola per un valore CloudSaveFolder, o null se il token non si conosce
    /// (in quel caso il gioco passa dalla ricerca sul disco).
    /// Esempi: {AppData}/Gioco/Saves/{EpicID}/ → WinAppDataLocal, Gioco/Saves/*;
    /// {appdata}/../locallow/Autore/Gioco/ → WinAppDataLocalLow, Autore/Gioco.
    /// </summary>
    public static SaveRule? ToRule(string value)
    {
        string text = value.Replace('\\', '/').Trim();

        if (!text.StartsWith('{') || !text.Contains('}'))
            return Unknown(value);

        int close = text.IndexOf('}');
        string token = text[1..close];
        string rest = text[(close + 1)..].Trim('/');

        // {AppData}/../locallow e {AppData}/../roaming: le cartelle sorelle di AppData\Local.
        if (token.Equals("AppData", StringComparison.OrdinalIgnoreCase) && rest.StartsWith("../", StringComparison.Ordinal))
        {
            string[] segments = rest.Split('/');

            if (segments.Length < 2 || !AppDataSiblings.TryGetValue(segments[1], out string? sibling))
                return Unknown(value);

            return Rule(sibling, string.Join('/', segments[2..]), value);
        }

        if (!Roots.TryGetValue(token, out string? root))
            return Unknown(value);

        return Rule(root, rest, value);
    }

    private static SaveRule? Rule(string root, string rest, string value)
    {
        string path = EpicIdToken.Replace(rest, "*");

        if (path.Contains('{') || path.Contains('}') || path.Split('/').Any(part => part == ".."))
            return Unknown(value);

        return new SaveRule(root, path, "*", true, []);
    }

    private static SaveRule? Unknown(string value)
    {
        Log.Write("Cartella di salvataggio Epic non riconosciuta: " + value);
        return null;
    }

    /// <summary>
    /// CloudSaveFolder di ogni voce del catalogo di Epic, per CatalogItemId.
    /// Il file è base64 di un array JSON; senza file o se è rotto, vuoto.
    /// </summary>
    public static Dictionary<string, string> ReadCatalog(string catalogFile)
    {
        var folders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (!File.Exists(catalogFile))
            return folders;

        try
        {
            byte[] json = Convert.FromBase64String(File.ReadAllText(catalogFile).Trim());

            using JsonDocument document = JsonDocument.Parse(json);

            foreach (JsonElement item in document.RootElement.EnumerateArray())
            {
                if (!item.TryGetProperty("id", out JsonElement id) || id.ValueKind != JsonValueKind.String)
                    continue;

                if (item.TryGetProperty("customAttributes", out JsonElement attributes)
                    && attributes.TryGetProperty("CloudSaveFolder", out JsonElement folder)
                    && folder.TryGetProperty("value", out JsonElement value)
                    && value.ValueKind == JsonValueKind.String
                    && value.GetString() is { Length: > 0 } text)
                {
                    folders[id.GetString()!] = text;
                }
            }
        }
        catch (Exception ex) when (ex is FormatException or JsonException or InvalidOperationException)
        {
            Log.Write("Catalogo di Epic non letto: " + catalogFile, ex);
        }

        return folders;
    }
}
