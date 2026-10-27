using Lss.App.ViewModels;

namespace Lss.Tests;

/// <summary>
/// I PIN sbagliati contano per persona e per tutta l'esecuzione: chiudere e riaprire il
/// tastierino non azzera l'attesa.
/// </summary>
public sealed class PinGuardTests : IDisposable
{
    private const string Andrea = "a1b2c3d4";
    private const string Clara = "c3d4e5f6";

    private readonly TestFolder folder = new();

    private DateTime now = new(2026, 10, 27, 12, 0, 0);

    public PinGuardTests() => TestApp.Ensure();

    public void Dispose() => folder.Dispose();

    /// <summary>Andrea (admin) e Clara (utente), tutte e due con il PIN 1234.</summary>
    private People People()
    {
        TestFolder.Write(Path.Combine(folder.Data, "persone.ini"),
            $"Ordine={Andrea},{Clara}\n" +
            $"{Andrea}.Nome=Andrea\n{Andrea}.Ruolo=admin\n" +
            $"{Clara}.Nome=Clara\n{Clara}.Ruolo=utente\n");

        var people = new People(folder.Data);
        people.Load();
        people.SetPin(Andrea, "1234");
        people.SetPin(Clara, "1234");
        return people;
    }

    private static void Wrong(PinPadViewModel pad, int times)
    {
        for (int i = 0; i < times; i++)
        {
            foreach (char digit in "9999")
                pad.Press(digit.ToString());
        }
    }

    [Fact]
    public void FiveWrongTriesWaitEvenWithANewPad()
    {
        People people = People();
        Wrong(new PinPadViewModel(people, people.Find(Clara)!, () => now), 5);

        var again = new PinPadViewModel(people, people.Find(Clara)!, () => now);

        Assert.True(again.Waiting);
        Assert.NotEqual("", again.WaitText);

        // Anche il PIN giusto, durante l'attesa, non conta.
        foreach (char digit in "1234")
            again.Press(digit.ToString());

        Assert.False(again.Accepted);
    }

    [Fact]
    public void AnotherPlayerDoesNotWait()
    {
        People people = People();
        Wrong(new PinPadViewModel(people, people.Find(Clara)!, () => now), 5);

        var other = new PinPadViewModel(people, people.Find(Andrea)!, () => now);

        Assert.False(other.Waiting);
    }

    [Fact]
    public void TheWaitEnds()
    {
        People people = People();
        Wrong(new PinPadViewModel(people, people.Find(Clara)!, () => now), 5);

        now = now.AddSeconds(PinGuard.WaitSeconds + 1);

        Assert.False(new PinPadViewModel(people, people.Find(Clara)!, () => now).Waiting);
    }

    [Fact]
    public void TheRightPinForgetsTheWrongOnes()
    {
        var guard = new PinGuard();
        guard.Wrong(Clara, now);
        guard.Wrong(Clara, now);
        guard.Right(Clara);

        for (int i = 0; i < PinGuard.MaxWrongTries - 1; i++)
            guard.Wrong(Clara, now);

        Assert.False(guard.IsWaiting(Clara, now));
    }
}
