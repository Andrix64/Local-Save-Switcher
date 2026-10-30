using System.Text;
using System.Text.Json;

namespace Lss;

/// <summary>
/// File JSON tipizzato (impostazioni, persone, stato dei giochi...). La scrittura passa da un file
/// temporaneo, così un'interruzione non lo lascia a metà. System.Text.Json fa parte di .NET.
/// </summary>
internal static class JsonFile
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    // UTF-8 senza BOM.
    private static readonly UTF8Encoding Utf8 = new(false);

    /// <summary>
    /// Il contenuto del file, o null se non c'è. Un file che non si legge (JSON rotto) si registra,
    /// si tiene come &lt;file&gt;.rotto e vale come se non ci fosse: chi lo chiama usa i valori di partenza.
    /// Le proprietà che non conosce si ignorano.
    /// </summary>
    public static T? Read<T>(string path) where T : class
    {
        if (!File.Exists(path))
            return null;

        try
        {
            return JsonSerializer.Deserialize<T>(File.ReadAllText(path, Utf8), Options)
                   ?? throw new JsonException("Il file non contiene un oggetto.");
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            Log.Write("File JSON non leggibile: " + path, ex);
            KeepBroken(path);

            return null;
        }
    }

    /// <summary>Scrive il file (indentato). Lancia un'eccezione in caso di errore.</summary>
    public static void Write<T>(string path, T value) where T : class
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        string temp = path + ".tmp";

        using (FileStream stream = new(temp, FileMode.Create, FileAccess.Write))
        using (StreamWriter writer = new(stream, Utf8))
        {
            writer.Write(JsonSerializer.Serialize(value, Options));
            writer.Write('\n');
            writer.Flush();
            stream.Flush(flushToDisk: true);
        }

        File.Move(temp, path, overwrite: true);
    }

    // Il file rotto resta accanto, come <file>.rotto: non si perde e non si legge più.
    private static void KeepBroken(string path)
    {
        try
        {
            File.Move(path, path + ".rotto", overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Write("File JSON rotto non messo da parte: " + path, ex);
        }
    }
}
