using System.Text;

namespace Lss.Tests;

/// <summary>
/// SteamAppInfo: il file appinfo.vdf è costruito nel test (v28 e v29), mai
/// copiato da una installazione di Steam.
/// </summary>
public sealed class SteamAppInfoTests : IDisposable
{
    private readonly TestFolder folder = new();

    public void Dispose() => folder.Dispose();

    private string SteamRoot => Path.Combine(folder.Root, "Steam");

    /// <summary>
    /// Scrive appinfo.vdf e carica le regole dei giochi indicati.
    /// </summary>
    private SteamAppInfo Load(byte[] file, params string[] appIds)
    {
        Directory.CreateDirectory(Path.Combine(SteamRoot, "appcache"));
        File.WriteAllBytes(Path.Combine(SteamRoot, "appcache", "appinfo.vdf"), file);

        return SteamAppInfo.Load(SteamRoot, new HashSet<string>(appIds));
    }

    /// <summary>
    /// Chiave del VDF: testo terminato da 0 (v28) o indice nella tabella (v29).
    /// </summary>
    private static void Key(BinaryWriter writer, List<string>? table, string key)
    {
        if (table == null)
        {
            Text(writer, key);
            return;
        }

        if (!table.Contains(key))
            table.Add(key);

        writer.Write(table.IndexOf(key));
    }

    private static void Text(BinaryWriter writer, string text)
    {
        writer.Write(Encoding.UTF8.GetBytes(text));
        writer.Write((byte)0);
    }

    /// <summary>
    /// Un blocco VDF binario con il contenuto scritto da write (il blocco
    /// chiude da sé: "08" dopo il contenuto).
    /// </summary>
    private static void Block(BinaryWriter writer, List<string>? table, string key, Action<BinaryWriter> write)
    {
        writer.Write((byte)0x00);
        Key(writer, table, key);
        write(writer);
        writer.Write((byte)0x08);
    }

    private static void Value(BinaryWriter writer, List<string>? table, string key, string value)
    {
        writer.Write((byte)0x01);
        Key(writer, table, key);
        Text(writer, value);
    }

    /// <summary>
    /// File appinfo.vdf con i giochi dati. v29: intestazione con l'offset della
    /// tabella delle chiavi in fondo; v28: chiavi come testo.
    /// </summary>
    private static byte[] AppInfo(bool v29, params (uint Id, Action<BinaryWriter, List<string>?> Vdf)[] apps)
    {
        var table = v29 ? new List<string>() : null;

        using var body = new MemoryStream();

        using (var writer = new BinaryWriter(body, Encoding.UTF8, leaveOpen: true))
        {
            foreach ((uint id, Action<BinaryWriter, List<string>?> vdf) in apps)
            {
                byte[] data = Build(w => vdf(w, table));

                writer.Write(id);
                writer.Write((uint)(60 + data.Length));
                writer.Write(new byte[60]);
                writer.Write(data);
            }

            writer.Write(0u);
        }

        using var file = new MemoryStream();

        using (var writer = new BinaryWriter(file, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(v29 ? 0x07564429u : 0x07564428u);
            writer.Write(1u);

            if (v29)
                writer.Write((long)(16 + body.Length));

            writer.Write(body.ToArray());

            if (table != null)
            {
                writer.Write((uint)table.Count);

                foreach (string key in table)
                    Text(writer, key);
            }
        }

        return file.ToArray();
    }

    private static byte[] Build(Action<BinaryWriter> write)
    {
        using var stream = new MemoryStream();

        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
            write(writer);

        return stream.ToArray();
    }

    /// <summary>
    /// Gioco 105600: due savefiles e un rootoverrides. Gioco 123450: senza ufs.
    /// </summary>
    private static (uint, Action<BinaryWriter, List<string>?>)[] TwoApps() =>
    [
        (105600u, (w, t) => Block(w, t, "appinfo", app =>
        {
            Block(w, t, "common", common => Value(w, t, "name", "Sandfall"));
            Block(w, t, "ufs", ufs =>
            {
                Block(w, t, "savefiles", files =>
                {
                    Block(w, t, "0", file =>
                    {
                        Value(w, t, "root", "WinAppDataLocal");
                        Value(w, t, "path", "Sandfall/Saved/SaveGames/{64BitSteamID}");
                        Value(w, t, "pattern", "*.sav");
                        Value(w, t, "recursive", "1");
                        Block(w, t, "platforms", platforms => Value(w, t, "1", "Windows"));
                    });
                    Block(w, t, "1", file =>
                    {
                        Value(w, t, "root", "WinMyDocuments");
                        Value(w, t, "path", "Sandfall\\Settings");
                    });
                });
                Block(w, t, "rootoverrides", overrides =>
                {
                    Block(w, t, "0", over =>
                    {
                        Value(w, t, "root", "WinAppDataLocalLow");
                        Value(w, t, "os", "Linux");
                        Value(w, t, "oscompare", "=");
                        Value(w, t, "useinstead", "LinuxXdgDataHome");
                        Value(w, t, "addpath", "Games");
                        Block(w, t, "pathtransforms", transforms =>
                            Block(w, t, "0", tr =>
                            {
                                Value(w, t, "find", "ZAUM Studio/Disco");
                                Value(w, t, "replace", "Disco");
                            }));
                    });
                });
            });
        })),
        (123450u, (w, t) => Block(w, t, "appinfo", app =>
            Block(w, t, "common", common => Value(w, t, "name", "Senza regole")))),
    ];

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ReadsRulesOfBothFormats(bool v29)
    {
        SteamAppInfo info = Load(AppInfo(v29, TwoApps()), "105600", "123450");

        IReadOnlyList<SaveRule> rules = info.RulesOf("105600");

        Assert.Equal(2, rules.Count);
        Assert.Equal("WinAppDataLocal", rules[0].Root);
        Assert.Equal("Sandfall/Saved/SaveGames/{64BitSteamID}", rules[0].Path);
        Assert.Equal("*.sav", rules[0].Pattern);
        Assert.True(rules[0].Recursive);
        Assert.Equal(new[] { "Windows" }, rules[0].Platforms);
        Assert.Equal("WinMyDocuments", rules[1].Root);
        Assert.Equal("Sandfall/Settings", rules[1].Path);
        Assert.Equal("*", rules[1].Pattern);
        Assert.False(rules[1].Recursive);
        Assert.Empty(rules[1].Platforms);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ReadsRootOverrides(bool v29)
    {
        SteamAppInfo info = Load(AppInfo(v29, TwoApps()), "105600");

        RootOverride over = Assert.Single(info.OverridesOf("105600"));

        Assert.Equal("WinAppDataLocalLow", over.Root);
        Assert.Equal("Linux", over.Os);
        Assert.Equal("=", over.OsCompare);
        Assert.Equal("LinuxXdgDataHome", over.UseInstead);
        Assert.Equal("Games", over.AddPath);
        Assert.Equal(new[] { ("ZAUM Studio/Disco", "Disco") }, over.Transforms);
    }

    [Fact]
    public void AppWithoutUfsHasNoRules()
    {
        SteamAppInfo info = Load(AppInfo(true, TwoApps()), "123450");

        Assert.Empty(info.RulesOf("123450"));
        Assert.Empty(info.OverridesOf("123450"));
    }

    [Fact]
    public void AppNotRequestedIsNotRead()
    {
        SteamAppInfo info = Load(AppInfo(true, TwoApps()), "105600");

        Assert.Equal(2, info.RulesOf("105600").Count);
        Assert.Empty(info.RulesOf("123450"));
    }

    [Fact]
    public void UnknownMagicGivesNoRules()
    {
        byte[] file = AppInfo(true, TwoApps());
        file[0] = 0x11;

        SteamAppInfo info = Load(file, "105600");

        Assert.Empty(info.RulesOf("105600"));
    }

    [Fact]
    public void MissingFileGivesNoRules()
    {
        SteamAppInfo info = SteamAppInfo.Load(SteamRoot, new HashSet<string> { "105600" });

        Assert.Empty(info.RulesOf("105600"));
    }

    [Fact]
    public void TruncatedFileKeepsWhatWasReadBeforeTheError()
    {
        // v28 non ha la tabella in fondo: si tronca dentro il secondo gioco.
        byte[] full = AppInfo(false, TwoApps());
        byte[] cut = full[..(full.Length - 20)];

        SteamAppInfo info = Load(cut, "105600", "123450");

        Assert.Equal(2, info.RulesOf("105600").Count);
        Assert.Empty(info.RulesOf("123450"));
    }

    [Fact]
    public void RulesWithoutRootAreSkipped()
    {
        byte[] file = AppInfo(true, (105600u, (w, t) => Block(w, t, "appinfo",
            app => Block(w, t, "ufs", ufs => Block(w, t, "savefiles", files =>
                Block(w, t, "0", file => Value(w, t, "path", "Sandfall")))))));

        SteamAppInfo info = Load(file, "105600");

        Assert.Empty(info.RulesOf("105600"));
    }
}
