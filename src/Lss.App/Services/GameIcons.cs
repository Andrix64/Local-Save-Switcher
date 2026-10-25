using Avalonia;
using Avalonia.Media.Imaging;

namespace Lss.App.Services;

/// <summary>
/// Icona di un gioco da file del PC, come PNG 64×64 nella cartella dei dati
/// (icone\provider_id.png). Ordine delle fonti: l'icona data dal launcher (Steam,
/// collegamenti), l'icona dentro l'eseguibile, su Linux il file .desktop e poi
/// un'immagine accanto all'eseguibile. Nessuna rete.
/// </summary>
internal static class GameIcons
{
    private const int Side = 64;

    // Parole nel nome di un eseguibile che non è il gioco (disinstallazione, installer...).
    private static readonly string[] NotTheGame = ["unins", "setup", "crash", "redist", "vc_", "dxsetup", "launcher"];

    // Estensioni di immagini che Avalonia legge.
    private static readonly string[] ImageExtensions = [".png", ".jpg", ".jpeg", ".ico", ".bmp"];

    /// <summary>Un file da cui viene l'icona: un'immagine, o un eseguibile da cui si legge.</summary>
    internal sealed record IconSource(string File, bool IsExe);

    /// <summary>
    /// Cartelle dove stanno i file .desktop dei programmi installati.
    /// </summary>
    public static IReadOnlyList<string> DesktopFolders { get; } =
    [
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share", "applications"),
        "/usr/share/applications",
    ];

    /// <summary>
    /// Percorso del PNG dell'icona del gioco, creato o riusato dalla cache; null se nessuna
    /// fonte dà un'immagine. exe è l'eseguibile dato dal launcher, o "" se non lo dà.
    /// </summary>
    public static string? Ensure(GameInfo game, string exe, string dataRoot) =>
        Ensure(game, exe, dataRoot, DesktopFolders);

    internal static string? Ensure(GameInfo game, string exe, string dataRoot, IReadOnlyList<string> desktopFolders)
    {
        string png = Path.Combine(dataRoot, "icone", CacheName(game) + ".png");

        try
        {
            foreach (IconSource source in Candidates(game, exe, desktopFolders))
            {
                if (IsFresh(png, source.File) || MakePng(source, png))
                    return png;
            }
        }
        catch (Exception ex)
        {
            // Un errore sul disco non ferma la lista: il gioco resta con la lettera.
            Log.Write("Icona del gioco non trovata: " + game.Name, ex);
        }

        return null;
    }

    /// <summary>
    /// Le fonti del gioco nell'ordine di priorità. L'eseguibile si cerca nella
    /// cartella del gioco solo se il launcher non lo dà.
    /// </summary>
    internal static IEnumerable<IconSource> Candidates(GameInfo game, string exe, IReadOnlyList<string> desktopFolders)
    {
        if (game.IconPath != "" && File.Exists(game.IconPath))
            yield return new IconSource(game.IconPath, IsExe(game.IconPath));

        if (exe == "")
            exe = LargestExe(game.InstallPath);

        if (exe == "" || !File.Exists(exe))
            yield break;

        yield return new IconSource(exe, true);

        if (!OperatingSystem.IsLinux())
            yield break;

        if (DesktopIcon(exe, desktopFolders) is { } desktop)
            yield return new IconSource(desktop, false);

        foreach (string image in ImagesNextTo(game, exe))
            yield return new IconSource(image, false);
    }

    /// <summary>
    /// L'eseguibile più grande della cartella del gioco, senza disinstallazioni e
    /// installer. Solo i file direttamente nella cartella, mai le sottocartelle.
    /// </summary>
    internal static string LargestExe(string folder)
    {
        if (!Directory.Exists(folder))
            return "";

        return Directory.EnumerateFiles(folder)
            .Where(IsExe)
            .Where(file => !NotTheGame.Any(word => Path.GetFileName(file).Contains(word, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(file => new FileInfo(file).Length)
            .FirstOrDefault() ?? "";
    }

    /// <summary>
    /// Icona del primo file .desktop la cui Exec= nomina l'eseguibile, o null.
    /// </summary>
    internal static string? DesktopIcon(string exe, IEnumerable<string> folders)
    {
        string name = Path.GetFileName(exe);

        foreach (string folder in folders.Where(Directory.Exists))
        {
            foreach (string file in Directory.EnumerateFiles(folder, "*.desktop"))
            {
                string[] lines;

                try
                {
                    lines = File.ReadAllLines(file);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    Log.Write("File .desktop non letto: " + file, ex);
                    continue;
                }

                string? exec = Value(lines, "Exec=");
                string? icon = Value(lines, "Icon=");

                if (exec is null || icon is null || !NamesExe(exec, exe, name))
                    continue;

                if (IconFile(icon) is { } found)
                    return found;
            }
        }

        return null;
    }

    // Le immagini accanto all'eseguibile: icon.png, il nome dell'eseguibile, il nome della cartella del gioco.
    private static IEnumerable<string> ImagesNextTo(GameInfo game, string exe)
    {
        string folder = Path.GetDirectoryName(exe) ?? "";
        string gameFolder = Path.GetFileName(game.InstallPath != "" ? game.InstallPath : folder);

        string[] images =
        [
            Path.Combine(folder, "icon.png"),
            Path.Combine(folder, Path.GetFileNameWithoutExtension(exe) + ".png"),
            Path.Combine(folder, gameFolder + ".png"),
        ];

        return images.Where(File.Exists);
    }

    // Il valore di una riga "chiave=valore" del file .desktop, o null se manca.
    private static string? Value(string[] lines, string key) =>
        lines.FirstOrDefault(line => line.StartsWith(key, StringComparison.Ordinal))?[key.Length..];

    // L'Exec= nomina l'eseguibile: il percorso intero, o un token con lo stesso nome del file.
    private static bool NamesExe(string exec, string exe, string name) =>
        exec.Contains(exe, StringComparison.Ordinal)
        || exec.Split(' ').Any(token => token.Trim('"') is var part && (part == exe || Path.GetFileName(part) == name));

    // File dell'icona: un percorso assoluto, o un nome di tema cercato nelle cartelle di icone (il più grande).
    private static string? IconFile(string icon)
    {
        if (Path.IsPathRooted(icon))
            return IsImage(icon) && File.Exists(icon) ? icon : null;

        return ThemeFolders
            .Where(Directory.Exists)
            .SelectMany(folder => Directory.EnumerateFiles(folder, icon + ".png", SearchOption.AllDirectories))
            .OrderByDescending(file => new FileInfo(file).Length)
            .FirstOrDefault();
    }

    private static IReadOnlyList<string> ThemeFolders { get; } =
    [
        "/usr/share/icons/hicolor",
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share", "icons", "hicolor"),
        "/usr/share/pixmaps",
    ];

    // Legge la fonte e la scrive come PNG 64×64. false se la fonte non dà un'immagine.
    private static bool MakePng(IconSource source, string png)
    {
        try
        {
            if (!source.IsExe)
            {
                using FileStream file = File.OpenRead(source.File);

                return Save(file, png);
            }

            byte[]? icon = ExeIcon.Extract(source.File);

            if (icon is null)
                return false;

            using var stream = new MemoryStream(icon);

            return Save(stream, png);
        }
        catch (Exception ex)
        {
            // Qualunque formato che Avalonia non legge: l'icona manca, la lista non si ferma.
            Log.Write("Icona non creata: " + source.File, ex);
            return false;
        }
    }

    // Immagine letta da un flusso, ridotta a 64×64 e salvata come PNG.
    private static bool Save(Stream image, string png)
    {
        using var source = new Bitmap(image);
        using Bitmap small = source.CreateScaledBitmap(new PixelSize(Side, Side), BitmapInterpolationMode.HighQuality);

        Directory.CreateDirectory(Path.GetDirectoryName(png)!);
        small.Save(png, new PngBitmapEncoderOptions());

        return true;
    }

    // La cache è buona se il PNG non è più vecchio della sua fonte.
    private static bool IsFresh(string png, string source) =>
        File.Exists(png) && File.GetLastWriteTimeUtc(png) >= File.GetLastWriteTimeUtc(source);

    // Nome del file nella cache: provider_id, con i caratteri non validi sostituiti.
    private static string CacheName(GameInfo game)
    {
        char[] invalid = Path.GetInvalidFileNameChars();

        return new string((game.Provider + "_" + game.Id).Select(c => invalid.Contains(c) ? '_' : c).ToArray());
    }

    private static bool IsExe(string path) => Path.GetExtension(path).Equals(".exe", StringComparison.OrdinalIgnoreCase);

    private static bool IsImage(string path) => ImageExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);
}
