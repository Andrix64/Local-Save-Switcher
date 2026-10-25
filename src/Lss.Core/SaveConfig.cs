using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Lss;

/// <summary>Un posto di una configurazione: radice (come nelle regole di L6), percorso relativo, maschera.</summary>
public sealed record SaveConfigSpot(string Root, string Path, string Pattern, bool Recursive);

/// <summary>Un gioco di una configurazione dei salvataggi.</summary>
public sealed record SaveConfigGame(string Provider, string Id, string Name, string InstallDir,
    bool Proton, IReadOnlyList<SaveConfigSpot> Spots, string Notes);

/// <summary>Il file di configurazione: il sistema in cui è stato fatto e i giochi.</summary>
public sealed record SaveConfigFile(string Os, string Distro, bool SteamDeck, string CreatedWith,
    string Created, IReadOnlyList<SaveConfigGame> Games);

/// <summary>
/// Configurazione dei salvataggi di uno o più giochi, da condividere a mano
/// (formato lss-save-config, JSON). Scritta in modo che funzioni su un altro computer:
/// radici come {WinAppDataLocal}, nessun nome utente, nessun ID Steam, nessun salvataggio.
/// Il file che arriva da altri si controlla (SaveConfig.Read): è dato non fidato.
/// </summary>
internal static class SaveConfig
{
    public const string FormatName = "lss-save-config";
    public const int FormatVersion = 1;

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>
    /// Configurazione di un gioco dai suoi posti. Un posto che non sta sotto una
    /// radice standard (per esempio D:\Giochi\Saves) non si esporta: va nell'elenco.
    /// </summary>
    public static (SaveConfigGame Game, IReadOnlyList<string> Skipped) FromGame(GameInfo game, SaveContext context,
        string notes = "")
    {
        var spots = new List<SaveConfigSpot>();
        var skipped = new List<string>();

        foreach (SaveSpot spot in game.Spots)
        {
            if (TrySplit(spot.PortablePath, out string root, out string path))
                spots.Add(new SaveConfigSpot(root, path, spot.Pattern, spot.Recursive));
            else
                skipped.Add(spot.Folder);
        }

        var configGame = new SaveConfigGame(game.Provider, game.Id, game.Name, game.InstallDir,
            context.PrefixPath != "", spots, notes);

        return (configGame, skipped);
    }

    /// <summary>
    /// Il file per questo computer e questi giochi: sistema, distribuzione, data, versione.
    /// </summary>
    public static SaveConfigFile CurrentFile(IReadOnlyList<SaveConfigGame> games)
    {
        bool linux = OperatingSystem.IsLinux();
        IReadOnlyList<string> release = linux ? ReadOsRelease() : [];
        string distro = linux ? OsReleaseValue(release, "PRETTY_NAME") : RuntimeInformation.OSDescription;
        bool steamDeck = linux && OsReleaseValue(release, "ID") == "steamos";
        string version = typeof(SaveConfig).Assembly.GetName().Version?.ToString() ?? "";

        return new SaveConfigFile(
            linux ? "linux" : "windows",
            distro,
            steamDeck,
            "LSS " + version,
            DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            games);
    }

    /// <summary>JSON indentato, con format e version davanti alle altre proprietà.</summary>
    public static string Write(SaveConfigFile file)
    {
        JsonObject body = JsonSerializer.SerializeToNode(file, Options)!.AsObject();

        var result = new JsonObject
        {
            ["format"] = FormatName,
            ["version"] = FormatVersion,
        };

        foreach (KeyValuePair<string, JsonNode?> pair in body)
            result[pair.Key] = JsonNode.Parse(pair.Value!.ToJsonString());

        return result.ToJsonString(Options);
    }

    /// <summary>
    /// Legge un file. Mai eccezioni: gli errori stanno nel Result.
    /// </summary>
    public static Result<SaveConfigFile> Read(string json)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("format", out JsonElement format)
                || format.ValueKind != JsonValueKind.String
                || format.GetString() != FormatName)
            {
                return Result<SaveConfigFile>.Fail(Loc.F(Strings.ConfigInvalid, "formato non riconosciuto"));
            }

            if (!root.TryGetProperty("version", out JsonElement version) || !version.TryGetInt32(out int number))
                return Result<SaveConfigFile>.Fail(Loc.F(Strings.ConfigInvalid, "versione mancante"));

            if (number > FormatVersion)
                return Result<SaveConfigFile>.Fail(Strings.ConfigTooNew);

            SaveConfigFile? file = JsonSerializer.Deserialize<SaveConfigFile>(json, Options);

            return file == null
                ? Result<SaveConfigFile>.Fail(Loc.F(Strings.ConfigInvalid, "file vuoto"))
                : Result<SaveConfigFile>.Ok(file);
        }
        catch (JsonException ex)
        {
            return Result<SaveConfigFile>.Fail(Loc.F(Strings.ConfigInvalid, ex.Message));
        }
    }

    /// <summary>
    /// Radice e percorso di un percorso portabile ({WinAppDataLocal}/Discord). false
    /// se non inizia con una radice standard.
    /// </summary>
    private static bool TrySplit(string portable, out string root, out string path)
    {
        root = "";
        path = "";

        if (!portable.StartsWith('{'))
            return false;

        int close = portable.IndexOf('}');

        if (close < 0 || !SavePathResolver.IsKnownRoot(portable[1..close]))
            return false;

        root = portable[1..close];
        path = portable[(close + 1)..].TrimStart('/', '\\').Replace('\\', '/');

        return true;
    }

    private static IReadOnlyList<string> ReadOsRelease()
    {
        try
        {
            return File.Exists("/etc/os-release") ? File.ReadAllLines("/etc/os-release") : [];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static string OsReleaseValue(IReadOnlyList<string> lines, string key)
    {
        foreach (string line in lines)
        {
            if (line.StartsWith(key + "=", StringComparison.Ordinal))
                return line[(key.Length + 1)..].Trim('"');
        }

        return "";
    }
}
