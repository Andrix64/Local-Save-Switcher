using System.Text;

namespace Lss.Tests;

/// <summary>
/// Funzioni di LinuxPlatform. Le funzioni pure girano su ogni sistema; quelle
/// che toccano il disco con collegamenti simbolici girano solo su Linux.
/// </summary>
public sealed class LinuxPlatformTests : IDisposable
{
    private readonly TestFolder folder = new();

    public void Dispose() => folder.Dispose();

    [Fact]
    public void SteamLinkIsNotCountedTwice()
    {
        if (!OperatingSystem.IsLinux()) return;

        string home = Path.Combine(folder.Root, "home");
        string native = Path.Combine(home, ".local", "share", "Steam");
        Directory.CreateDirectory(Path.Combine(native, "userdata"));
        Directory.CreateDirectory(Path.Combine(home, ".steam"));
        Directory.CreateSymbolicLink(Path.Combine(home, ".steam", "steam"), native);

        IReadOnlyList<string> roots = LinuxPlatform.FindSteamRoots(home);

        Assert.Single(roots);
        Assert.Equal(Path.GetFullPath(native), roots[0]);
    }

    [Fact]
    public void NativeComesBeforeFlatpak()
    {
        if (!OperatingSystem.IsLinux()) return;

        string home = Path.Combine(folder.Root, "home");
        string native = Path.Combine(home, ".local", "share", "Steam");
        string flatpak = Path.Combine(home, ".var", "app", "com.valvesoftware.Steam", ".local", "share", "Steam");
        Directory.CreateDirectory(Path.Combine(flatpak, "userdata"));
        Directory.CreateDirectory(Path.Combine(native, "userdata"));

        IReadOnlyList<string> roots = LinuxPlatform.FindSteamRoots(home);

        Assert.Equal(2, roots.Count);
        Assert.Equal(Path.GetFullPath(native), roots[0]);
        Assert.Equal(Path.GetFullPath(flatpak), roots[1]);
    }

    [Fact]
    public void FolderWithoutUserdataIsSkipped()
    {
        if (!OperatingSystem.IsLinux()) return;

        string home = Path.Combine(folder.Root, "home");
        Directory.CreateDirectory(Path.Combine(home, ".local", "share", "Steam"));

        Assert.Empty(LinuxPlatform.FindSteamRoots(home));
    }

    [Fact]
    public void RunningByRunningAppId()
    {
        Assert.True(LinuxPlatform.RegistrySaysRunning(Registry("105600", "0"), "105600"));
    }

    [Fact]
    public void RunningByAppsRunningFlag()
    {
        Assert.True(LinuxPlatform.RegistrySaysRunning(Registry("0", "1"), "105600"));
    }

    [Fact]
    public void NotRunning()
    {
        Assert.False(LinuxPlatform.RegistrySaysRunning(Registry("0", "0"), "105600"));
    }

    [Fact]
    public void OtherGameRunning()
    {
        Assert.False(LinuxPlatform.RegistrySaysRunning(Registry("730", "0"), "105600"));
    }

    [Fact]
    public void BrokenRegistryIsNotRunning()
    {
        Assert.False(LinuxPlatform.RegistrySaysRunning("\"Registry\" { \"HKCU\" {", "105600"));
    }

    [Fact]
    public void RegistryFilesAreTheExistingOnes()
    {
        string home = Path.Combine(folder.Root, "home");
        TestFolder.Write(Path.Combine(home, ".steam", "registry.vdf"), "");
        TestFolder.Write(Path.Combine(home, "snap", "steam", "common", ".steam", "registry.vdf"), "");

        IReadOnlyList<string> files = LinuxPlatform.RegistryFiles(home);

        Assert.Equal(
            new[] { Path.Combine(home, ".steam", "registry.vdf"), Path.Combine(home, "snap", "steam", "common", ".steam", "registry.vdf") },
            files);
    }

    [Fact]
    public void SecondLockFailsUntilTheFirstIsReleased()
    {
        if (!OperatingSystem.IsLinux()) return;

        string path = Path.Combine(folder.Root, "lss.lock");
        IDisposable? first = LinuxPlatform.Lock(path);

        Assert.NotNull(first);
        Assert.Null(LinuxPlatform.Lock(path));

        first.Dispose();

        using IDisposable? third = LinuxPlatform.Lock(path);
        Assert.NotNull(third);
    }

    [Fact]
    public void EnvironWithSteamAppId()
    {
        byte[] environ = Encoding.UTF8.GetBytes("PATH=/usr/bin\0SteamAppId=870780\0HOME=/home/a\0");

        Assert.True(LinuxPlatform.EnvironMatches(environ, "870780"));
    }

    [Fact]
    public void EnvironWithSteamGameId()
    {
        byte[] environ = Encoding.UTF8.GetBytes("SteamGameId=870780\0");

        Assert.True(LinuxPlatform.EnvironMatches(environ, "870780"));
    }

    [Fact]
    public void EnvironWithSimilarIdDoesNotMatch()
    {
        byte[] environ = Encoding.UTF8.GetBytes("SteamAppId=1056001\0SteamGameId=1056001\0");

        Assert.False(LinuxPlatform.EnvironMatches(environ, "105600"));
    }

    [Fact]
    public void EmptyEnvironDoesNotMatch()
    {
        Assert.False(LinuxPlatform.EnvironMatches(ReadOnlySpan<byte>.Empty, "870780"));
    }

    /// <summary>registry.vdf con il gioco 105600 e un RunningAppID dato.</summary>
    [Fact]
    public void CmdlineMatchesLinuxPath()
    {
        byte[] cmdline = Encoding.UTF8.GetBytes("wine\0/home/a/Games/Control/Control.exe\0-nointro\0");

        Assert.True(LinuxPlatform.CmdlineMatches(cmdline, "/home/a/Games/Control"));
    }

    [Fact]
    public void CmdlineMatchesWineWindowsPath()
    {
        byte[] cmdline = Encoding.UTF8.GetBytes("wine\0Z:\\home\\a\\Games\\Control\\Control.exe\0");

        Assert.True(LinuxPlatform.CmdlineMatches(cmdline, "/home/a/Games/Control"));
    }

    [Fact]
    public void CmdlineDoesNotMatchAnotherFolder()
    {
        // "Control" non deve trovare "Control Ultimate": manca il "/" dopo il nome.
        byte[] cmdline = Encoding.UTF8.GetBytes("wine\0Z:\\home\\a\\Games\\Control Ultimate\\Control.exe\0");

        Assert.False(LinuxPlatform.CmdlineMatches(cmdline, "/home/a/Games/Control"));
        Assert.False(LinuxPlatform.CmdlineMatches(cmdline, ""));
    }

    [Fact]
    public void FindProcessesUnderEmptyFolderFindsNothing()
    {
        if (!OperatingSystem.IsLinux()) return;

        Assert.Empty(new LinuxPlatform().FindProcessesUnder(folder.Root));
        Assert.Empty(new LinuxPlatform().FindProcessesUnder(""));
    }

    [Fact]
    public void RegistryIsNullOnLinux()
    {
        if (!OperatingSystem.IsLinux()) return;

        var platform = new LinuxPlatform();

        Assert.Null(platform.ReadRegistryString(@"HKEY_LOCAL_MACHINE\SOFTWARE\Valve\Steam", "SteamPath"));
        Assert.Empty(platform.RegistrySubKeys(@"HKEY_LOCAL_MACHINE\SOFTWARE\Valve"));
    }

    [Fact]
    public void KnownFoldersDefaultXdgWithoutVariables()
    {
        string home = Path.Combine(folder.Root, "home");

        KnownFolders folders = LinuxPlatform.KnownFoldersFor(home, null, null, "andrix");

        Assert.Equal(home, folders.Home);
        Assert.Equal(Path.Combine(home, ".local", "share"), folders.XdgData);
        Assert.Equal(Path.Combine(home, ".config"), folders.XdgConfig);
        Assert.Equal("andrix", folders.UserName);
        Assert.Equal("", folders.Documents);
        Assert.Equal("", folders.AppData);
    }

    [Fact]
    public void KnownFoldersUseAbsoluteXdgVariables()
    {
        string home = Path.Combine(folder.Root, "home");
        string data = Path.Combine(folder.Root, "dati");
        string config = Path.Combine(folder.Root, "config");

        KnownFolders folders = LinuxPlatform.KnownFoldersFor(home, data, config, "andrix");

        Assert.Equal(data, folders.XdgData);
        Assert.Equal(config, folders.XdgConfig);
    }

    [Fact]
    public void KnownFoldersIgnoreRelativeAndEmptyXdgVariables()
    {
        string home = Path.Combine(folder.Root, "home");

        KnownFolders folders = LinuxPlatform.KnownFoldersFor(home, "dati/lss", "", "andrix");

        Assert.Equal(Path.Combine(home, ".local", "share"), folders.XdgData);
        Assert.Equal(Path.Combine(home, ".config"), folders.XdgConfig);
    }

    [Fact]
    public void DocumentsOnlyWhenTheFolderExists()
    {
        string home = Path.Combine(folder.Root, "home");
        Directory.CreateDirectory(home);

        Assert.Equal("", LinuxPlatform.KnownFoldersFor(home, null, null, "andrix").Documents);

        Directory.CreateDirectory(Path.Combine(home, "Documents"));

        Assert.Equal(Path.Combine(home, "Documents"), LinuxPlatform.KnownFoldersFor(home, null, null, "andrix").Documents);
    }

    private static string Registry(string runningAppId, string running) => $$"""
        "Registry"
        {
            "HKCU"
            {
                "Software"
                {
                    "Valve"
                    {
                        "Steam"
                        {
                            "RunningAppID"		"{{runningAppId}}"
                            "Apps"
                            {
                                "105600"
                                {
                                    "Running"		"{{running}}"
                                }
                            }
                        }
                    }
                }
            }
        }
        """;
}
