using Lss.App.ViewModels;

namespace Lss.Tests;

public sealed class PinPadViewModelTests : IDisposable
{
    private const string Admin = "a1b2c3d4";
    private const string Plain = "c3d4e5f6";

    private readonly TestFolder folder = new();

    // Orologio finto: il test lo fa avanzare.
    private DateTime now = new(2026, 10, 25, 12, 0, 0);

    public PinPadViewModelTests() => TestApp.Ensure();

    public void Dispose() => folder.Dispose();

    /// <summary>Persone: Andrea (admin con PIN 1234), Marco (admin senza PIN).</summary>
    private People People()
    {
        TestFolder.Write(Path.Combine(folder.Data, "persone.ini"),
            $"Ordine={Admin},{Plain}\n" +
            $"{Admin}.Nome=Andrea\n{Admin}.Ruolo=admin\n" +
            $"{Plain}.Nome=Marco\n{Plain}.Ruolo=admin\n");

        var people = new People(folder.Data);
        people.Load();
        people.SetPin(Admin, "1234");
        return people;
    }

    private void Type(PinPadViewModel pad, string digits)
    {
        foreach (char digit in digits)
            pad.Press(digit.ToString());
    }

    [Fact]
    public void TheRightPinIsAccepted()
    {
        People people = People();
        var pad = new PinPadViewModel(people, people.Find(Admin)!, () => now);

        Type(pad, "1234");

        Assert.True(pad.Accepted);
    }

    [Fact]
    public void AWrongPinShowsItAndStartsAgain()
    {
        People people = People();
        var pad = new PinPadViewModel(people, people.Find(Admin)!, () => now);

        Type(pad, "9999");

        Assert.False(pad.Accepted);
        Assert.True(pad.IsWrong);
        Assert.Equal("○○○○", pad.Dots);
    }

    [Fact]
    public void FiveWrongTriesMeanThirtySecondsOfWait()
    {
        People people = People();
        var pad = new PinPadViewModel(people, people.Find(Admin)!, () => now);

        for (int i = 0; i < PinPadViewModel.MaxWrongTries; i++)
            Type(pad, "9999");

        Assert.True(pad.Waiting);
        Assert.Contains("30", pad.WaitText);

        // Durante l'attesa anche il PIN giusto non passa.
        Type(pad, "1234");
        Assert.False(pad.Accepted);

        now = now.AddSeconds(31);
        pad.Tick();
        Assert.False(pad.Waiting);
        Type(pad, "1234");
        Assert.True(pad.Accepted);
    }

    [Fact]
    public void BackRemovesTheLastDigit()
    {
        People people = People();
        var pad = new PinPadViewModel(people, people.Find(Admin)!, () => now);

        Type(pad, "12");
        pad.Back();

        Assert.Equal("●○○○", pad.Dots);
    }

    [Fact]
    public void AnAdminWithPinOpensThePadAndTheRightPinChoosesThem()
    {
        People people = People();
        var picker = new AdminPickerViewModel(people);

        Assert.True(picker.Choose(people.Find(Admin)!));

        Assert.Null(picker.Chosen);
        Assert.True(picker.PadOpen);
        Type(picker.Pad!, "1234");
        Assert.True(picker.CompletePin());
        Assert.Equal(Admin, picker.Chosen!.Id);
    }

    [Fact]
    public void AnAdminWithoutPinIsChosenAtOnce()
    {
        People people = People();
        var picker = new AdminPickerViewModel(people);

        Assert.True(picker.Choose(people.Find(Plain)!));

        Assert.Equal(Plain, picker.Chosen!.Id);
        Assert.False(picker.PadOpen);
    }

    [Fact]
    public void CancelingThePadChoosesNobody()
    {
        People people = People();
        var picker = new AdminPickerViewModel(people);
        picker.Choose(people.Find(Admin)!);

        picker.CancelPin();

        Assert.Null(picker.Chosen);
        Assert.False(picker.PadOpen);
    }
}
