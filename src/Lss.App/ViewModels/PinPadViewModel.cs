using CommunityToolkit.Mvvm.ComponentModel;

namespace Lss.App.ViewModels;

/// <summary>
/// Il tastierino del PIN di una persona (P4a). Dopo cinque errori di
/// fila si aspetta 30 secondi: gli errori sono per persona (People.Guard), non per
/// finestra, e restano in memoria fino al riavvio.
/// </summary>
public partial class PinPadViewModel : ObservableObject
{
    internal const int MaxWrongTries = PinGuard.MaxWrongTries;
    internal const int WaitSeconds = PinGuard.WaitSeconds;

    private readonly People people;
    private readonly Func<DateTime> now;
    private string digits = "";

    internal PinPadViewModel(People people, Person admin, Func<DateTime>? clock = null)
    {
        this.people = people;
        Admin = admin;
        now = clock ?? (() => DateTime.Now);
        Title = Loc.F(Strings.PinTitle, admin.Name);
        Prompt = Strings.PinPrompt;
        WrongText = Strings.PinWrong;
        Refresh();
    }

    public Person Admin { get; }

    public string Title { get; }

    public string Prompt { get; }

    public string WrongText { get; }

    /// <summary>Cifre inserite come pallini: "●●○○".</summary>
    [ObservableProperty]
    private string dots = "○○○○";

    /// <summary>I quattro pallini del design: true per ogni cifra inserita.</summary>
    [ObservableProperty]
    private IReadOnlyList<bool> dotFilled = [false, false, false, false];

    /// <summary>true dopo un PIN sbagliato, finché non si ricomincia.</summary>
    [ObservableProperty]
    private bool isWrong;

    /// <summary>Testo dell'attesa, o "" se non si attende.</summary>
    [ObservableProperty]
    private string waitText = "";

    /// <summary>true quando il PIN è giusto: la scelta è fatta.</summary>
    public bool Accepted { get; private set; }

    /// <summary>Premuta una cifra "0"…"9". Ignorata durante l'attesa o a PIN già giusto.</summary>
    public void Press(string digit)
    {
        if (Accepted || Waiting || digits.Length >= 4 || digit.Length != 1 || !char.IsAsciiDigit(digit[0]))
            return;

        digits += digit;
        IsWrong = false;
        Refresh();

        if (digits.Length == 4)
            Verify();
    }

    /// <summary>Toglie l'ultima cifra.</summary>
    public void Back()
    {
        if (digits.Length == 0 || Accepted)
            return;

        digits = digits[..^1];
        Refresh();
    }

    /// <summary>Aggiorna l'attesa: l'interfaccia lo chiama ogni secondo.</summary>
    public void Tick() => Refresh();

    /// <summary>true durante l'attesa dopo cinque errori.</summary>
    public bool Waiting => people.Guard.IsWaiting(Admin.Id, now());

    private void Verify()
    {
        if (people.CheckPin(Admin.Id, digits))
        {
            Accepted = true;
            people.Guard.Right(Admin.Id);
            return;
        }

        digits = "";
        IsWrong = true;
        people.Guard.Wrong(Admin.Id, now());

        Refresh();
    }

    private void Refresh()
    {
        Dots = new string('●', digits.Length) + new string('○', 4 - digits.Length);
        DotFilled = Enumerable.Range(0, 4).Select(index => index < digits.Length).ToArray();

        WaitText = Waiting
            ? Loc.F(Strings.PinWait, (int)Math.Ceiling((people.Guard.WaitUntil(Admin.Id) - now()).TotalSeconds))
            : "";
    }
}
