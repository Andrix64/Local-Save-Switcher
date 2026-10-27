using Lss.App.ViewModels;

namespace Lss.Tests;

/// <summary>"PIN di …" dalla finestra Giocatori (P4a): nuovo PIN due volte, e quello attuale per cambiarlo.</summary>
public sealed class SetPinViewModelTests : IDisposable
{
    private const string Admin = "a1b2c3d4";

    private readonly TestFolder folder = new();

    public SetPinViewModelTests() => TestApp.Ensure();

    public void Dispose() => folder.Dispose();

    private People People()
    {
        TestFolder.WritePeople(folder.Data,
            $"Ordine={Admin}\n{Admin}.Nome=Andrea\n{Admin}.Ruolo=admin\n");

        var people = new People(folder.Data);
        people.Load();
        return people;
    }

    [Fact]
    public void SaveNeedsTheSamePinTwice()
    {
        People people = People();
        var model = new SetPinViewModel(people, people.Find(Admin)!) { NewPin = "4821", Again = "4822" };

        Assert.True(model.Mismatch);
        Assert.False(model.CanSave);
        Assert.False(model.Save());

        model.Again = "4821";
        Assert.True(model.Save());
        Assert.True(people.CheckPin(Admin, "4821"));
    }

    [Fact]
    public void ChangingAPinAsksTheCurrentOne()
    {
        People people = People();
        people.SetPin(Admin, "1111");
        var model = new SetPinViewModel(people, people.Find(Admin)!) { NewPin = "2222", Again = "2222" };

        Assert.True(model.NeedsCurrent);
        Assert.False(model.CanSave);

        model.Current = "9999";
        Assert.False(model.Save());
        Assert.Equal(Strings.PinWrong, model.Message);
        Assert.True(people.CheckPin(Admin, "1111"));

        model.Current = "1111";
        Assert.True(model.Save());
        Assert.True(people.CheckPin(Admin, "2222"));
    }
}
