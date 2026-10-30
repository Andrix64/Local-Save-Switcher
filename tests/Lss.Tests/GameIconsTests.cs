using System.Buffers.Binary;
using Lss.App.Services;

namespace Lss.Tests;

/// <summary>
/// GameIcons: da quale file viene l'icona, l'eseguibile di riserva, la cache in
/// icone\ e il file .desktop. Tutto in TestFolder: nessun file vero.
/// </summary>
public sealed class GameIconsTests : IDisposable
{
    private readonly TestFolder folder = new();

    // Un PNG 1×1 valido, perché Avalonia lo deve poter leggere davvero.
    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");

    private string Game => Path.Combine(folder.Root, "Giochi", "Gioco");
    private string Data => folder.Data;
    private string Desktop => Path.Combine(folder.Root, "applications");

    public GameIconsTests()
    {
        TestApp.Ensure();
    }

    public void Dispose() => folder.Dispose();

    private static string Save(string path, byte[] bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);

        return path;
    }

    private static string Save(string path, string text) => Save(path, System.Text.Encoding.UTF8.GetBytes(text));

    private static GameInfo GameIn(string install, string icon = "") =>
        new("manual", "abc", "Gioco", "", "", install, [], SupportState.NoSaves, IconPath: icon, Source: SaveSource.None);

    private static byte[] ExeWithIcon() => PeBuilder.Build(false, true, (16, 16, Png));

    [Fact]
    public void TheLauncherIconComesBeforeTheExe()
    {
        string icon = Save(Path.Combine(folder.Root, "icona.png"), Png);
        string exe = Save(Path.Combine(Game, "gioco.exe"), ExeWithIcon());

        List<GameIcons.IconSource> sources = GameIcons.Candidates(GameIn(Game, icon), "", []).ToList();

        Assert.Equal(icon, sources[0].File);
        Assert.False(sources[0].IsExe);
        Assert.Equal(exe, sources[1].File);
        Assert.True(sources[1].IsExe);
    }

    [Fact]
    public void AnExeIconInTheLauncherIconIsReadAsAnExe()
    {
        string exe = Save(Path.Combine(folder.Root, "icona.exe"), ExeWithIcon());

        GameIcons.IconSource first = GameIcons.Candidates(GameIn(Game, exe), "", []).First();

        Assert.Equal(exe, first.File);
        Assert.True(first.IsExe);
    }

    [Fact]
    public void ExeOfTheLauncherIsUsedBeforeTheFolderSearch()
    {
        string given = Save(Path.Combine(Game, "bin", "avvio.exe"), ExeWithIcon());
        Save(Path.Combine(Game, "gioco.exe"), new byte[900]);

        List<GameIcons.IconSource> sources = GameIcons.Candidates(GameIn(Game), given, []).ToList();

        Assert.Equal(given, sources.Single(source => source.IsExe).File);
    }

    [Fact]
    public void ExeFallbackIsTheLargestExeOfTheFolderWithoutInstallers()
    {
        Save(Path.Combine(Game, "unins000.exe"), new byte[900]);
        Save(Path.Combine(Game, "UnityCrashHandler64.exe"), new byte[800]);
        string game = Save(Path.Combine(Game, "gioco.exe"), new byte[100]);
        Save(Path.Combine(Game, "sottocartella", "grande.exe"), new byte[5000]);

        Assert.Equal(game, GameIcons.LargestExe(Game));
    }

    [Fact]
    public void ExeFallbackIsEmptyWithoutGameFolder()
    {
        Assert.Equal("", GameIcons.LargestExe(Path.Combine(folder.Root, "non-c-e")));
    }

    [Fact]
    public void ALauncherImageBecomesA64PixelPng()
    {
        string icon = Save(Path.Combine(folder.Root, "icona.png"), Png);

        string? png = GameIcons.Ensure(GameIn(Game, icon), "", Data, []);

        Assert.Equal(Path.Combine(Data, "icone", "manual_abc.png"), png);
        Assert.Equal((byte)0x89, File.ReadAllBytes(png!)[0]);
    }

    [Fact]
    public void TheIconOfTheExeBecomesAPng()
    {
        string exe = Save(Path.Combine(Game, "gioco.exe"), ExeWithIcon());

        string? png = GameIcons.Ensure(GameIn(Game), exe, Data, []);

        Assert.NotNull(png);
        Assert.Equal((byte)0x89, File.ReadAllBytes(png)[0]);
    }

    [Fact]
    public void TheCacheIsReusedAndRefreshedWhenTheExeIsNewer()
    {
        string exe = Save(Path.Combine(Game, "gioco.exe"), ExeWithIcon());
        File.SetLastWriteTimeUtc(exe, DateTime.UtcNow.AddDays(-1));

        string png = GameIcons.Ensure(GameIn(Game), exe, Data, [])!;

        // Il PNG più recente dell'eseguibile si riusa: non viene riscritto.
        File.WriteAllText(png, "sentinella");
        Assert.Equal(png, GameIcons.Ensure(GameIn(Game), exe, Data, []));
        Assert.Equal("sentinella", File.ReadAllText(png));

        // Un eseguibile più recente rifà il PNG.
        File.SetLastWriteTimeUtc(exe, DateTime.UtcNow.AddMinutes(10));
        Assert.Equal(png, GameIcons.Ensure(GameIn(Game), exe, Data, []));
        Assert.Equal((byte)0x89, File.ReadAllBytes(png)[0]);
    }

    [Fact]
    public void WithoutAnyIconThereIsNothing()
    {
        Save(Path.Combine(Game, "gioco.exe"), "non è un eseguibile");

        Assert.Null(GameIcons.Ensure(GameIn(Game), "", Data, []));
        Assert.False(Directory.Exists(Path.Combine(Data, "icone")));
    }

    [Fact]
    public void DesktopFileNamingTheExeGivesItsIcon()
    {
        string exe = Path.Combine(Game, "gioco");
        string icon = Save(Path.Combine(folder.Root, "gioco.png"), Png);
        Save(Path.Combine(Desktop, "altro.desktop"), "[Desktop Entry]\nExec=/altro/altro\nIcon=" + icon + "\n");
        Save(Path.Combine(Desktop, "gioco.desktop"), "[Desktop Entry]\nName=Gioco\nExec=\"" + exe + "\" %U\nIcon=" + icon + "\n");

        Assert.Equal(icon, GameIcons.DesktopIcon(exe, [Desktop]));
        Assert.Null(GameIcons.DesktopIcon(Path.Combine(Game, "nessuno"), [Desktop]));
    }

    [Fact]
    public void AProgramOfWindowsBecomesA64PixelPng()
    {
        if (!OperatingSystem.IsWindows())
            return;

        // File vero in sola lettura (explorer.exe); il PNG finisce in TestFolder.
        string exe = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");

        string? png = GameIcons.Ensure(GameIn(Game), exe, Data, []);

        Assert.NotNull(png);
        byte[] bytes = File.ReadAllBytes(png);

        // Intestazione IHDR del PNG: larghezza e altezza a 32 bit, big-endian.
        Assert.Equal(64, BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16)));
        Assert.Equal(64, BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20)));
    }

    [Fact]
    public void OnLinuxTheExeComesBeforeTheDesktopFileAndTheImageNextToIt()
    {
        if (!OperatingSystem.IsLinux())
            return;

        string exe = Save(Path.Combine(Game, "gioco"), ExeWithIcon());
        string icon = Save(Path.Combine(folder.Root, "gioco.png"), Png);
        Save(Path.Combine(Desktop, "gioco.desktop"), "[Desktop Entry]\nExec=" + exe + "\nIcon=" + icon + "\n");
        string next = Save(Path.Combine(Game, "icon.png"), Png);

        List<GameIcons.IconSource> sources = GameIcons.Candidates(GameIn(Game), exe, [Desktop]).ToList();

        Assert.Equal(new[] { exe, icon, next }, sources.Select(source => source.File));
    }
}
