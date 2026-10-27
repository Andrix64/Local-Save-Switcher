namespace Lss.Tests;

public sealed class PinTests : IDisposable
{
    private const string Admin = "a1b2c3d4";
    private const string OtherAdmin = "c3d4e5f6";
    private const string User = "b2c3d4e5";

    private readonly TestFolder folder = new();

    public void Dispose() => folder.Dispose();

    private string IniPath => Path.Combine(folder.Data, "persone.ini");

    /// <summary>Persone: due amministratori e un utente.</summary>
    private People Load()
    {
        TestFolder.Write(IniPath,
            $"Ordine={Admin},{OtherAdmin},{User}\n" +
            $"{Admin}.Nome=Andrea\n{Admin}.Ruolo=admin\n" +
            $"{OtherAdmin}.Nome=Marco\n{OtherAdmin}.Ruolo=admin\n" +
            $"{User}.Nome=Clara\n{User}.Ruolo=utente\n");

        var people = new People(folder.Data);
        people.Load();
        return people;
    }

    /// <summary>Rilegge persone.ini senza riscriverlo (a differenza di Load).</summary>
    private People Reread()
    {
        var people = new People(folder.Data);
        people.Load();
        return people;
    }

    [Fact]
    public void SetPinThenCheckRightAndWrong()
    {
        People people = Load();

        Assert.True(people.SetPin(Admin, "4821").Success);

        Assert.True(people.CheckPin(Admin, "4821"));
        Assert.False(people.CheckPin(Admin, "4822"));
        Assert.True(people.Find(Admin)!.HasPin);
        Assert.True(Reread().Find(Admin)!.HasPin);
        Assert.True(Reread().CheckPin(Admin, "4821"));
    }

    [Theory]
    [InlineData("123")]
    [InlineData("12345")]
    [InlineData("12a4")]
    [InlineData("")]
    public void WrongFormatIsRefused(string pin)
    {
        People people = Load();

        Result result = people.SetPin(Admin, pin);

        Assert.False(result.Success);
        Assert.False(people.Find(Admin)!.HasPin);
    }

    [Fact]
    public void SameDigitsGiveDifferentHashesForTwoAdmins()
    {
        People people = Load();
        people.SetPin(Admin, "1234");
        people.SetPin(OtherAdmin, "1234");

        string text = File.ReadAllText(IniPath);
        string first = text.Split('\n').Single(line => line.StartsWith(Admin + ".Pin="));
        string second = text.Split('\n').Single(line => line.StartsWith(OtherAdmin + ".Pin="));

        Assert.NotEqual(first.Split('=')[1], second.Split('=')[1]);
    }

    [Fact]
    public void ThePinIsNotWrittenInClear()
    {
        People people = Load();

        people.SetPin(Admin, "4821");

        Assert.DoesNotContain("4821", File.ReadAllText(IniPath));
        Assert.Contains(Admin + ".Pin=", File.ReadAllText(IniPath));
    }

    [Fact]
    public void RemovePinRemovesIt()
    {
        People people = Load();
        people.SetPin(Admin, "4821");

        Assert.True(people.RemovePin(Admin).Success);

        Assert.False(people.CheckPin(Admin, "4821"));
        Assert.False(Reread().Find(Admin)!.HasPin);
    }

    [Fact]
    public void AnotherAdminCanRemoveAPinWithoutKnowingIt()
    {
        People people = Load();
        people.SetPin(Admin, "4821");

        Assert.True(people.RemovePin(Admin).Success);
        Assert.False(Reread().Find(Admin)!.HasPin);
    }

    [Fact]
    public void AUserCanHaveAPin()
    {
        People people = Load();

        Assert.True(people.SetPin(User, "4821").Success);

        Assert.True(people.Find(User)!.HasPin);
        Assert.True(Reread().Find(User)!.HasPin);
        Assert.True(Reread().CheckPin(User, "4821"));
        Assert.False(Reread().CheckPin(User, "0000"));
    }

    [Fact]
    public void DemotingKeepsThePin()
    {
        People people = Load();
        people.SetPin(OtherAdmin, "4821");

        Assert.True(people.SetAdmin(OtherAdmin, false).Success);

        Assert.True(people.Find(OtherAdmin)!.HasPin);
        Assert.True(Reread().Find(OtherAdmin)!.HasPin);
        Assert.True(Reread().CheckPin(OtherAdmin, "4821"));
    }
}
