namespace Lss.Tests;

public sealed class PeopleTests : IDisposable
{
    private readonly TestFolder folder = new();

    public void Dispose() => folder.Dispose();

    /// <summary>Persone caricate dalla cartella di test.</summary>
    private People Load()
    {
        People people = new(folder.Data);
        people.Load();
        return people;
    }

    [Fact]
    public void FirstPlayerIsAdminWhenThereIsNoFile()
    {
        People people = Load();

        Person person = Assert.Single(people.All);
        Assert.True(person.IsAdmin);
        Assert.False(string.IsNullOrWhiteSpace(person.Name));
        Assert.True(File.Exists(Path.Combine(folder.Data, "persone.json")));
    }

    [Fact]
    public void AddCreatesAUser()
    {
        People people = Load();

        Result<Person> added = people.Add("Clara");

        Assert.True(added.Success, added.Message);
        Assert.Equal("Clara", added.Value!.Name);
        Assert.False(added.Value.IsAdmin);
        Assert.Equal(2, people.All.Count);
    }

    [Fact]
    public void RenameRefusesADuplicateNameIgnoringCase()
    {
        People people = Load();
        string other = people.Add("Clara").Value!.Id;

        Result result = people.Rename(other, people.All[0].Name.ToUpperInvariant());

        Assert.False(result.Success);
        Assert.Equal("Clara", people.Find(other)!.Name);
    }

    [Fact]
    public void RenameChangesTheName()
    {
        People people = Load();
        string id = people.All[0].Id;

        Result result = people.Rename(id, "Andrea");

        Assert.True(result.Success, result.Message);
        Assert.Equal("Andrea", Load().Find(id)!.Name);
    }

    [Fact]
    public void RemoveRefusesTheLastPlayer()
    {
        People people = Load();

        Result result = people.Remove(people.All[0].Id);

        Assert.False(result.Success);
        Assert.Single(people.All);
    }

    [Fact]
    public void RemoveTakesAUserAway()
    {
        People people = Load();
        string user = people.Add("Clara").Value!.Id;

        Result result = people.Remove(user);

        Assert.True(result.Success, result.Message);
        Assert.Null(Load().Find(user));
    }

    [Fact]
    public void TheLastAdminCannotBeRemovedOrMadeAUser()
    {
        People people = Load();
        string admin = people.All[0].Id;
        string user = people.Add("Clara").Value!.Id;

        Assert.False(people.Remove(admin).Success);
        Assert.False(people.SetAdmin(admin, false).Success);
        Assert.True(people.Find(admin)!.IsAdmin);

        // Con un secondo amministratore il primo si può declassare.
        people.SetPin(user, "4821");
        Assert.True(people.SetAdmin(user, true).Success);
        Assert.True(people.SetAdmin(admin, false).Success);
        Assert.False(people.Find(admin)!.IsAdmin);
    }

    [Fact]
    public void FileWithoutAdminMakesTheFirstPlayerAdmin()
    {
        TestFolder.WritePeople(
            folder.Data,
            "Ordine=aaaaaaaa,bbbbbbbb\n" +
            "aaaaaaaa.Nome=Andrea\n" +
            "aaaaaaaa.Ruolo=utente\n" +
            "bbbbbbbb.Nome=Clara\n" +
            "bbbbbbbb.Ruolo=utente\n");

        People people = Load();

        Assert.Equal(2, people.All.Count);
        Assert.True(people.Find("aaaaaaaa")!.IsAdmin);
        Assert.False(people.Find("bbbbbbbb")!.IsAdmin);
    }

    [Fact]
    public void MoveChangesTheOrder()
    {
        People people = Load();
        string first = people.All[0].Id;
        string second = people.Add("Clara").Value!.Id;

        Assert.True(people.Move(second, 0).Success);

        Assert.Equal(second, Load().All[0].Id);
        Assert.Equal(first, Load().All[1].Id);
    }

    [Fact]
    public void ColoursComeFromThePaletteAndCycle()
    {
        People people = Load();

        for (int i = 0; i < People.Palette.Count + 1; i++)
            people.Add("Giocatore" + i);

        List<string> colours = people.All.Select(person => person.Color).ToList();

        // I primi otto sono tutti diversi, il nono ricomincia dalla tavolozza.
        Assert.Equal(People.Palette.Count, colours.Take(People.Palette.Count).Distinct().Count());
        Assert.Equal(People.Palette[0], colours[People.Palette.Count]);
        Assert.All(colours, colour => Assert.Contains(colour, People.Palette));
    }

    [Fact]
    public void EveryPaletteColourHasEnoughContrastWithWhite()
    {
        foreach (string colour in People.Palette)
        {
            double contrast = 1.05 / (Luminance(colour) + 0.05);
            Assert.True(contrast >= 4.5, $"{colour}: {contrast:F2}");
        }
    }

    [Fact]
    public void BrokenFileBecomesTheFirstPlayerAgain()
    {
        TestFolder.Write(Path.Combine(folder.Data, "persone.json"), "questo non è un file di persone");

        People people = Load();

        Person person = Assert.Single(people.All);
        Assert.True(person.IsAdmin);
        Assert.True(File.Exists(Path.Combine(folder.Data, "persone.json.rotto")));
    }

    [Fact]
    public void PlayersAreKeptBetweenLoads()
    {
        People people = Load();
        string id = people.Add("Clara").Value!.Id;
        Assert.True(people.SetColor(id, People.Palette[5]).Success);

        Person saved = Load().Find(id)!;

        Assert.Equal("Clara", saved.Name);
        Assert.Equal(People.Palette[5], saved.Color);
    }

    /// <summary>Luminanza relativa di un colore #RRGGBB (formula WCAG).</summary>
    private static double Luminance(string colour)
    {
        double Channel(int start)
        {
            double value = Convert.ToInt32(colour.Substring(start, 2), 16) / 255.0;
            return value <= 0.03928 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Channel(1) + 0.7152 * Channel(3) + 0.0722 * Channel(5);
    }
}
