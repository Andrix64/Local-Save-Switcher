using System.Text;

namespace Lss.Tests;

/// <summary>
/// I giochi non Steam in shortcuts.vdf (VDF binario) e il loro avvio con
/// steam://rungameid/ e l'ID del collegamento.
/// </summary>
public sealed class SteamShortcutsTests : IDisposable
{
    private readonly TestFolder folder = new();
    private readonly FakePlatform platform = new();

    private string SteamRoot => Path.Combine(folder.Root, "Steam");
    private string Account => Path.Combine(SteamRoot, "userdata", "111");

    public SteamShortcutsTests()
    {
        platform.SteamRoots.Add(SteamRoot);
    }

    public void Dispose() => folder.Dispose();

    // Scrive shortcuts.vdf come lo scrive Steam: due voci, la seconda è Heroic (da escludere).
    private void WriteShortcuts(int appId = -1, string exe = "\"/games/gioco.sh\"")
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream))
        {
            writer.Write((byte)0x00);
            Text(writer, "shortcuts");
            writer.Write((byte)0x00);
            Text(writer, "0");
            Int(writer, "appid", appId);
            Str(writer, "AppName", "Gioco");
            Str(writer, "Exe", exe);
            Str(writer, "StartDir", "\"/games/\"");
            writer.Write((byte)0x08);

            writer.Write((byte)0x00);
            Text(writer, "1");
            Int(writer, "appid", 42);
            Str(writer, "AppName", "Heroic");
            Str(writer, "Exe", "/usr/bin/heroic");
            writer.Write((byte)0x08);

            writer.Write((byte)0x08);
        }

        Directory.CreateDirectory(Path.Combine(Account, "config"));
        File.WriteAllBytes(Path.Combine(Account, "config", "shortcuts.vdf"), stream.ToArray());
    }

    private static void Text(BinaryWriter writer, string text)
    {
        writer.Write(Encoding.UTF8.GetBytes(text));
        writer.Write((byte)0x00);
    }

    private static void Str(BinaryWriter writer, string key, string value)
    {
        writer.Write((byte)0x01);
        Text(writer, key);
        Text(writer, value);
    }

    private static void Int(BinaryWriter writer, string key, int value)
    {
        writer.Write((byte)0x02);
        Text(writer, key);
        writer.Write(value);
    }

    [Fact]
    public void ReadsTheShortcutsButNotTheLaunchers()
    {
        WriteShortcuts();

        var shortcut = SteamShortcuts.Read(Account).Single();

        Assert.Equal(uint.MaxValue, shortcut.AppId);
        Assert.Equal("Gioco", shortcut.Name);
        Assert.Equal("/games/", shortcut.StartDir);
    }

    [Fact]
    public void MissingOrBrokenFileGivesNoShortcuts()
    {
        Assert.Empty(SteamShortcuts.Read(Account));

        TestFolder.Write(Path.Combine(Account, "config", "shortcuts.vdf"), "non è un file VDF");
        Assert.Empty(SteamShortcuts.Read(Account));
    }

    [Fact]
    public void LaunchAddressUsesTheShortcutIdInTheHighBits()
    {
        Assert.Equal("steam://rungameid/" + ((4294967295UL << 32) | 0x02000000UL), SteamShortcuts.LaunchUri(uint.MaxValue));
    }

    [Fact]
    public void ShortcutIsAGameOfSteamThatLaunchesWithItsAddress()
    {
        WriteShortcuts();

        var provider = new SteamProvider(platform);
        var account = new GameAccount("steam", "111", "", Account, null);
        GameInfo game = provider.FindGames(account).Single(g => g.Name == "Gioco");

        Assert.Equal("steam", game.Provider);
        Assert.Equal("4294967295", game.Id);
        Assert.Equal("/games/", game.InstallPath);
        Assert.True(provider.Launch(game).Success);
        Assert.Contains(SteamShortcuts.LaunchUri(uint.MaxValue), platform.OpenedUris);
    }
}
